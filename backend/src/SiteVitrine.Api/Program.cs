using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Console;
using Scalar.AspNetCore;
using SiteVitrine.Api.Admin;
using SiteVitrine.Api.Auth;
using SiteVitrine.Api.Data;
using SiteVitrine.Api.Images;
using SiteVitrine.Api.Projects;
using SiteVitrine.Api.Resources;

// ─────────────────────────────────────────────────────────────────────────────
// Ce programme a deux modes :
//   • sans argument  → serveur HTTP (le mode normal) ;
//   • avec un verbe  → tâche d'administration one-shot, puis sortie
//                      (migrate, import, export).
// Le second est ce qu'exécute le conteneur `api-init` du compose : même image, même configuration,
// seule la commande change. C'est ce qui permet de migrer le schéma sans mêler cette opération au
// démarrage de l'API — et de charger le contenu sans la mêler à la migration.
//
// Une application ASP.NET Core se construit ensuite en deux temps :
//   1. le *builder* : on enregistre les services dans le conteneur d'injection de dépendances ;
//   2. l'*app* : on assemble le pipeline de middlewares, puis on mappe les routes.
// ─────────────────────────────────────────────────────────────────────────────

// La configuration .NET n'accepte que des arguments de la forme --clé=valeur et rejetterait un
// verbe nu : on ne les lui transmet donc que lorsqu'il n'y a pas de commande à exécuter.
var command = args.FirstOrDefault(arg => !arg.StartsWith('-'));

var builder = WebApplication.CreateBuilder(command is null ? args : []);

if (command is not null)
{
    // Convention Unix : les diagnostics sur stderr, les données sur stdout. Sans ça, les logs EF se
    // mêleraient au YAML produit par `export`, dont la sortie est faite pour être redirigée vers un
    // fichier. On reconfigure le fournisseur console déjà enregistré plutôt que d'en ajouter un
    // second, qui doublerait chaque ligne.
    builder.Services.Configure<ConsoleLoggerOptions>(
        options => options.LogToStandardErrorThreshold = LogLevel.Trace);
}

// ── 1. Services ─────────────────────────────────────────────────────────────

// La configuration est empilée par défaut dans cet ordre (le dernier gagne) :
// appsettings.json → appsettings.{Environment}.json → variables d'environnement → args.
// D'où la convention Docker : ConnectionStrings__Default écrase la valeur du fichier,
// le double underscore remplaçant le « : » de la hiérarchie JSON.
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Chaîne de connexion absente. Définir ConnectionStrings__Default (voir .env.example).");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
    {
        // Rejoue automatiquement les commandes qui échouent pour une raison transitoire
        // (base pas encore prête au démarrage de la stack, coupure réseau brève…).
        npgsql.EnableRetryOnFailure();

        // Toutes les migrations vivent dans ce schéma plutôt qu'en vrac dans public.
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory");
    }));

// Le dossier des données que l'API écrit elle-même : images déposées, clés de session. À ne pas
// confondre avec content/, que l'API ne fait que lire. En conteneur, c'est le volume `storage`.
// Surtout pas « data » : sous Windows, dont le système de fichiers ignore la casse, ce serait le
// dossier de code Data/ (DbContext, migrations).
var storagePath = builder.Configuration["Storage:Path"]
    ?? throw new InvalidOperationException("Storage:Path absent de la configuration (cf. appsettings.json).");

builder.Services.AddSingleton(new ImageStore(Path.Combine(storagePath, "images")));

// Session du back-office : connexion avec Google, cookie de session, clés persistées.
builder.Services.AddAdminAuthentication(builder.Configuration, storagePath);

// Pas de configuration CORS : le navigateur n'appelle jamais cette API depuis une autre origine.
// En production le Nginx du frontend relaie /api vers ce conteneur, en développement c'est le
// proxy de `ng serve` — dans les deux cas la requête part vers l'origine de la page.

// Document OpenAPI (JSON) décrivant l'API. Sert de source à l'interface d'administration (Scalar).
builder.Services.AddOpenApi();

// /health interroge réellement la base : c'est ce que sonde le healthcheck Docker, donc la stack
// ne considère l'API « saine » que si elle peut effectivement répondre.
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

// Traduit les exceptions non gérées en réponse ProblemDetails (RFC 7807) au lieu d'une page HTML.
builder.Services.AddProblemDetails();

var app = builder.Build();

// ── 2. Mode commande ────────────────────────────────────────────────────────

if (command is not null)
{
    // Le code de sortie est ce que Docker observe pour décider si l'étape d'init a réussi.
    return await AdminCommands.RunAsync(app, command, args);
}

// ── 3. Pipeline HTTP ────────────────────────────────────────────────────────

// En premier : capture les exceptions levées plus bas dans le pipeline.
app.UseExceptionHandler();

// Juste après : l'administration technique n'existe que sur son port. Placée avant le routage, la
// règle s'applique quoi qu'il arrive plus bas.
var adminPort = builder.Configuration.GetValue("Admin:Port", 8081);
app.UseAdminPortIsolation(adminPort);

// La chaîne en production : visiteur → Nginx Proxy Manager (TLS) → Nginx du conteneur `web` → API.
// Kestrel ne voit que le dernier maillon : l'IP du conteneur `web`, et du HTTP. Ce middleware
// reconstitue la requête d'origine (Connection.RemoteIpAddress, Request.Scheme) à partir des en-têtes
// X-Forwarded-*, pour que les logs, les URL générées et tout ce qui s'appuie sur l'IP ou le schéma du
// visiteur voient le vrai client. Il doit donc précéder tout middleware qui les lit.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,

    // Chaque proxy ajoute à DROITE de X-Forwarded-For l'adresse qui l'a contacté : l'API reçoit
    // « visiteur, NPM ». Le middleware lit la liste de droite à gauche, sur ForwardLimit entrées —
    // autant que de proxys de confiance. Avec la valeur par défaut (1), il s'arrêterait sur l'IP de NPM.
    // Pas plus de 2 non plus : ce qui est à gauche du visiteur, c'est le visiteur qui l'a écrit (un
    // « X-Forwarded-For: 1.2.3.4 » envoyé à NPM y est conservé), donc falsifiable.
    // ⚠ À ajuster si un maillon apparaît ou disparaît (un CDN devant NPM, par exemple).
    ForwardLimit = 2,
};

// Par défaut, le middleware ne croit que les proxys en boucle locale (127.0.0.0/8 et ::1) : venus
// d'ailleurs, les en-têtes sont ignorés. Les nôtres ont des IP Docker non connues à l'avance : on vide
// les deux listes, et tout expéditeur est alors cru, dans la limite de ForwardLimit.
//
// ⚠ Clear(), surtout pas « KnownIPNetworks = { } » dans l'initialiseur : sur une propriété sans setter,
// cette syntaxe (initialiseur de collection) AJOUTE les éléments listés — aucun ici — sans rien
// retirer. La boucle locale restait alors seule autorisée, et les en-têtes venus du conteneur `web`
// étaient ignorés sans la moindre erreur.
//
// Hypothèse de confiance, garantie par docker-compose.yml : le port 8080 n'est joignable que par `web`
// (réseau `internal`, rien de publié), et `web` que par ce qui partage le réseau de NPM. Un autre
// conteneur branché sur ce réseau pourrait donc s'attribuer une IP arbitraire ; le port 8081 aussi,
// mais il n'est joignable que depuis l'hôte.
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();

app.UseForwardedHeaders(forwardedHeadersOptions);

// Qui fait la requête (lecture du cookie), puis a-t-il le droit (RequireAuthorization des endpoints).
// L'ordre est imposé : l'autorisation s'appuie sur l'identité établie par l'authentification.
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

// Le document OpenAPI et son interface. Tous deux filtrés sur le port d'administration par
// UseAdminPortIsolation ci-dessus : http://localhost:8081/scalar une fois le tunnel SSH ouvert.
app.MapOpenApi();
app.MapScalarApiReference(options => options.WithTitle("Site vitrine — administration"));

// Une ligne par feature. C'est le seul point de Program.cs à toucher quand on en ajoute une.
app.MapResourcesEndpoints();
app.MapProjectsEndpoints();
app.MapImagesEndpoints();
app.MapAuthEndpoints();
app.MapAdminEndpoints();

await app.RunAsync();
return 0;
