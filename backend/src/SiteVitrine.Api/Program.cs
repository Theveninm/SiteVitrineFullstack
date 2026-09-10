using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Console;
using Scalar.AspNetCore;
using SiteVitrine.Api.Admin;
using SiteVitrine.Api.Data;
using SiteVitrine.Api.Resources;

// ─────────────────────────────────────────────────────────────────────────────
// Ce programme a deux modes :
//   • sans argument  → serveur HTTP (le mode normal) ;
//   • avec un verbe  → tâche d'administration one-shot, puis sortie (migrate, import, export).
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

// Pas de configuration CORS : le navigateur n'appelle jamais cette API depuis une autre origine.
// En production le Nginx du frontend relaie /api vers ce conteneur, en développement c'est le
// proxy de `ng serve` — dans les deux cas la requête part vers l'origine de la page.

// Document OpenAPI (JSON) décrivant l'API. Sert de source à l'interface du back-office (Scalar).
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

// Juste après : le back-office n'existe que sur son port. Placée avant le routage, la règle
// s'applique quoi qu'il arrive plus bas.
var adminPort = builder.Configuration.GetValue("Admin:Port", 8081);
app.UseAdminPortIsolation(adminPort);

// Derrière Nginx Proxy Manager, l'IP et le schéma vus par Kestrel sont ceux du proxy.
// Ce middleware réécrit ces informations à partir des en-têtes X-Forwarded-* transmis par NPM,
// pour que les logs et les URL générées reflètent la vraie requête cliente.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    // Le proxy est sur le réseau Docker : sa plage d'IP n'est pas connue à l'avance, on lève
    // donc la restriction par défaut. Sans danger tant que l'API n'est pas exposée directement.
    KnownIPNetworks = { },
    KnownProxies = { },
});

app.MapHealthChecks("/health");

// Le document OpenAPI et son interface. Tous deux filtrés sur le port d'administration par
// UseAdminPortIsolation ci-dessus : http://localhost:8081/scalar une fois le tunnel SSH ouvert.
app.MapOpenApi();
app.MapScalarApiReference(options => options.WithTitle("Site vitrine — administration"));

// Une ligne par feature. C'est le seul point de Program.cs à toucher quand on en ajoute une
// (authentification, messages, réservations…).
app.MapResourcesEndpoints();
app.MapAdminEndpoints();

await app.RunAsync();
return 0;
