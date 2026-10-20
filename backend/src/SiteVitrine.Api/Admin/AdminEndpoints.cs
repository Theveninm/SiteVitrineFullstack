using Microsoft.EntityFrameworkCore;
using SiteVitrine.Api.Auth;
using SiteVitrine.Api.Content;
using SiteVitrine.Api.Data;
using SiteVitrine.Api.Images;

namespace SiteVitrine.Api.Admin;

/// <summary>
/// L'administration <b>technique</b> : état du schéma, import et export du fichier de contenu.
/// À ne pas confondre avec le back-office d'édition (page /admin du site, connexion Google),
/// qui écrit via les endpoints de chaque feature.
///
/// <b>Isolation par port plutôt que par authentification.</b> L'API écoute sur deux ports :
/// 8080 sert le site (relayé par Nginx) et 8081 sert ces routes-ci. Le compose ne publie 8081 que sur
/// 127.0.0.1 de l'hôte, et le Nginx du frontend n'en connaît pas l'existence : la surface publique ne
/// peut donc pas atteindre ces routes, quoi qu'il arrive. L'accès se fait par tunnel SSH (cf. README).
///
/// Ces opérations touchent la base entière (un import écrase les modifications du back-office) :
/// les garder hors d'internet reste la protection la plus sûre, même avec la connexion Google du
/// back-office.
/// </summary>
public static class AdminEndpoints
{
    /// <summary>Préfixes d'URL réservés au port d'administration.</summary>
    private static readonly string[] AdminPaths = ["/admin", "/openapi", "/scalar"];

    /// <summary>
    /// Barrière d'isolation, à placer tôt dans le pipeline. Un middleware plutôt qu'un filtre
    /// d'endpoint : il couvre d'un seul tenant les routes d'administration et celles mappées par
    /// OpenAPI et Scalar, dont on ne construit pas les endpoints nous-mêmes.
    ///
    /// On compare le port TCP réellement utilisé par la connexion, pas l'en-tête Host (ce que ferait
    /// <c>RequireHost</c>) : un en-tête se falsifie, un port d'arrivée non. Et 404 plutôt que 403,
    /// pour que ces routes n'apparaissent même pas comme existantes depuis le port public.
    /// </summary>
    public static IApplicationBuilder UseAdminPortIsolation(this IApplicationBuilder app, int adminPort) =>
        app.Use(async (context, next) =>
        {
            var isAdminPath = AdminPaths.Any(prefix => context.Request.Path.StartsWithSegments(prefix));

            if (isAdminPath && context.Connection.LocalPort != adminPort)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await next();
        });

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/admin").WithTags("Admin");

        group.MapGet("/status", GetStatus)
            .WithSummary("État du schéma, du contenu en base, des images et du fichier de contenu.");

        group.MapPost("/content/import", ImportContent)
            .WithSummary("Charge content/content.yaml en base (correspondance par slug).");

        group.MapGet("/content/export", ExportContent)
            .WithSummary("Renvoie le contenu de la base au format du fichier YAML.");

        return routes;
    }

    /// <summary>L'« écran d'accueil » technique : tout ce qu'on veut vérifier d'un coup d'œil.</summary>
    private static async Task<IResult> GetStatus(
        AppDbContext db, ImageStore images, IConfiguration config, CancellationToken ct)
    {
        var path = ContentFile.PathFrom(config);
        var fileInfo = new FileInfo(path);

        return TypedResults.Ok(new
        {
            Schema = new
            {
                Applied = await db.Database.GetAppliedMigrationsAsync(ct),
                Pending = await db.Database.GetPendingMigrationsAsync(ct),
            },
            Database = new
            {
                ResourceCount = await db.Resources.CountAsync(ct),
                ProjectCount = await db.Projects.CountAsync(ct),
            },
            Images = new
            {
                Path = images.Root,
                Count = Directory.EnumerateFiles(images.Root).Count(ImageStore.HasAllowedExtension),
            },
            ContentFile = new
            {
                Path = fileInfo.FullName,
                Exists = fileInfo.Exists,
                LastModifiedUtc = fileInfo.Exists ? fileInfo.LastWriteTimeUtc : (DateTime?)null,
            },
            BackOffice = new
            {
                // Sans identifiants Google ni adresse autorisée, toute connexion au back-office est refusée.
                GoogleConfigured = AuthEndpoints.IsConfigured(config),
                AllowedEmailCount = AuthEndpoints.AllowedEmails(config).Count,
            },
        });
    }

    /// <param name="prune">
    /// Supprime les ressources absentes du fichier. Sans ce drapeau, l'import n'efface rien et se
    /// contente de lister les orphelines dans son rapport.
    /// </param>
    private static async Task<IResult> ImportContent(
        AppDbContext db, ImageStore images, IConfiguration config, bool prune = false, CancellationToken ct = default)
    {
        var file = await ContentFile.LoadAsync(ContentFile.PathFrom(config), ct);
        var report = await ContentImporter.ImportAsync(
            db, file, images, ContentFile.ImagesDirectoryFrom(config), prune, ct);

        return TypedResults.Ok(report);
    }

    private static async Task<IResult> ExportContent(AppDbContext db, CancellationToken ct)
    {
        var file = await ContentImporter.ExportAsync(db, ct);

        // text/yaml plutôt que JSON : la réponse est destinée à être recollée dans content/content.yaml.
        return TypedResults.Text(file.ToYaml(), "text/yaml; charset=utf-8");
    }
}
