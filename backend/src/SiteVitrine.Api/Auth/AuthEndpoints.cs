using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.HttpResults;

namespace SiteVitrine.Api.Auth;

/// <summary>
/// Connexion au back-office avec un compte Google.
///
/// Le déroulé (OAuth 2.0), entièrement pris en charge par le handler Google d'ASP.NET :
/// <list type="number">
///   <item>le navigateur quitte l'application pour <c>/api/auth/login</c>, qui le redirige vers Google ;</item>
///   <item>l'utilisateur s'y connecte — aucun mot de passe ne transite par notre site ;</item>
///   <item>Google le renvoie sur <c>/api/auth/signin-google</c> avec un code, que le handler échange
///   contre l'identité du compte, de serveur à serveur, grâce au secret client ;</item>
///   <item>si l'adresse est autorisée, le handler émet notre propre cookie de session et renvoie sur
///   <c>/admin</c>.</item>
/// </list>
///
/// Google prouve l'identité ; la décision d'autoriser reste ici (<c>Auth:AllowedEmails</c>).
/// Les écritures de chaque feature sont protégées chez elles, par <c>RequireAuthorization()</c>.
/// </summary>
public static class AuthEndpoints
{
    /// <summary>Page Angular de connexion, où l'on revient avec un code d'erreur si la connexion échoue.</summary>
    private const string LoginPage = "/admin/connexion";

    /// <summary>
    /// Adresse de retour de Google, à déclarer à l'identique dans la console Google Cloud. Sous /api
    /// (et non /signin-google, la valeur par défaut) : c'est le seul préfixe que le Nginx de production
    /// et le proxy de `ng serve` relaient vers l'API.
    /// </summary>
    private const string GoogleCallbackPath = "/api/auth/signin-google";

    /// <summary>
    /// Google n'est utilisable que si ses identifiants et au moins une adresse autorisée sont configurés.
    /// </summary>
    public static bool IsConfigured(IConfiguration config) =>
        !string.IsNullOrWhiteSpace(config["Auth:Google:ClientId"])
        && !string.IsNullOrWhiteSpace(config["Auth:Google:ClientSecret"])
        && AllowedEmails(config).Count > 0;

    /// <summary>Comptes autorisés, séparés par des virgules (ex. « moi@gmail.com,autre@gmail.com »).</summary>
    public static HashSet<string> AllowedEmails(IConfiguration config) =>
        (config["Auth:AllowedEmails"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static IServiceCollection AddAdminAuthentication(
        this IServiceCollection services, IConfiguration config, string storagePath)
    {
        var authentication = services
            // Schéma par défaut : notre cookie. C'est lui qui lit la session à chaque requête et qui
            // répond 401 aux requêtes non authentifiées. Google n'intervient que lorsqu'on le sollicite
            // explicitement (Login) : s'il était le schéma de challenge par défaut, un 401 sur un appel
            // XHR deviendrait une redirection vers Google, que le navigateur bloquerait.
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "sitevitrine_admin";
                // Illisible depuis JavaScript : une faille XSS ne permettrait pas de voler la session.
                options.Cookie.HttpOnly = true;
                // Jamais envoyé en clair. « Always » plutôt que « SameAsRequest » : derrière NPM puis
                // Nginx, l'API peut recevoir du HTTP même quand le visiteur est en HTTPS. Les navigateurs
                // acceptent tout de même ce cookie sur http://localhost, le développement fonctionne.
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                // Protection CSRF : un cookie Lax n'accompagne pas un POST/PUT/DELETE initié par un autre
                // site, donc un formulaire piégé ailleurs ne peut rien écrire en notre nom. Lax et non
                // Strict : on arrive ici en revenant de Google, c'est-à-dire par une navigation venue
                // d'un autre site, que Strict traiterait comme suspecte.
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.Path = "/api";

                // Session de 8 h, prolongée tant qu'on s'en sert.
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;

                // Il n'y a pas de page de connexion côté serveur : c'est Angular qui l'affiche. Une requête
                // refusée doit donc recevoir un 401/403 exploitable, pas une redirection vers
                // /Account/Login. .NET 10 le fait déjà pour les endpoints qu'il détecte comme API ;
                // l'écrire rend le comportement explicite et indépendant de cette détection.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        // Sans configuration complète, le handler Google n'est pas enregistré du tout : il refuserait
        // sa configuration à chaque requête, et le site public tomberait avec le back-office. La
        // connexion renvoie alors simplement un message sur la page de connexion.
        if (IsConfigured(config))
        {
            authentication.AddGoogle(options =>
            {
                options.ClientId = config["Auth:Google:ClientId"]!;
                options.ClientSecret = config["Auth:Google:ClientSecret"]!;
                options.CallbackPath = GoogleCallbackPath;

                // Le profil renvoyé par Google contient « email_verified », que le handler ne recopie pas
                // en claim par défaut : on le demande pour pouvoir le vérifier ci-dessous.
                options.ClaimActions.MapJsonKey("email_verified", "email_verified");

                // Appelé une fois l'identité obtenue, juste avant l'émission du cookie : c'est le point
                // où l'on décide d'accepter ou non ce compte.
                options.Events.OnTicketReceived = context =>
                {
                    var email = context.Principal?.FindFirstValue(ClaimTypes.Email);
                    var isVerified = string.Equals(
                        context.Principal?.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase);

                    if (email is null || !isVerified || !AllowedEmails(config).Contains(email))
                    {
                        // HandleResponse() : on prend la main sur la réponse, le handler n'émet donc pas
                        // de cookie de session.
                        context.Response.Redirect($"{LoginPage}?erreur=refus");
                        context.HandleResponse();
                    }

                    return Task.CompletedTask;
                };

                // Consentement refusé chez Google, retour expiré ou falsifié… : un message sur la page de
                // connexion plutôt qu'une erreur 500.
                options.Events.OnRemoteFailure = context =>
                {
                    context.Response.Redirect($"{LoginPage}?erreur=google");
                    context.HandleResponse();
                    return Task.CompletedTask;
                };
            });
        }

        services.AddAuthorization();

        // Le cookie est chiffré avec les clés de Data Protection. Par défaut, en conteneur, elles ne
        // vivent qu'en mémoire : chaque redémarrage en génère de nouvelles et invalide les sessions.
        // Rangées dans le dossier de stockage (volume Docker), elles survivent aux redéploiements.
        // ASP.NET avertit au démarrage qu'elles sont stockées non chiffrées : attendu, le volume n'est
        // lisible que depuis l'hôte.
        services.AddDataProtection()
            .SetApplicationName("SiteVitrine")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(storagePath, "keys")));

        return services;
    }

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth").WithTags("Auth");

        group.MapGet("/login", Login)
            .WithSummary("Redirige vers Google pour ouvrir une session (navigation pleine page, pas un appel XHR).");

        group.MapPost("/logout", Logout)
            .WithSummary("Ferme la session du site (pas celle de Google).");

        group.MapGet("/me", () => TypedResults.NoContent())
            .RequireAuthorization()
            .WithSummary("204 si la session est ouverte, 401 sinon. Interrogé par le garde de route Angular.");

        // /api/auth/signin-google n'apparaît pas ici : c'est le handler Google qui intercepte cette
        // adresse, avant même le routage.

        return routes;
    }

    private static Results<ChallengeHttpResult, RedirectHttpResult> Login(IConfiguration config)
    {
        if (!IsConfigured(config))
        {
            return TypedResults.Redirect($"{LoginPage}?erreur=non-configure");
        }

        // Challenge = « prouver son identité auprès de ce schéma ». Le handler Google répond par une
        // redirection vers Google ; au retour, cookie de session puis redirection vers RedirectUri.
        return TypedResults.Challenge(
            new AuthenticationProperties { RedirectUri = "/admin" },
            [GoogleDefaults.AuthenticationScheme]);
    }

    private static SignOutHttpResult Logout() =>
        TypedResults.SignOut(authenticationSchemes: [CookieAuthenticationDefaults.AuthenticationScheme]);
}
