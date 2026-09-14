using Microsoft.AspNetCore.Http.HttpResults;

namespace SiteVitrine.Api.Images;

public static class ImagesEndpoints
{
    public static IEndpointRouteBuilder MapImagesEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/images").WithTags("Images");

        group.MapGet("/{fileName}", Get)
            .WithSummary("Sert une image du contenu.");

        group.MapPost("/", Upload)
            .RequireAuthorization()
            // Les endpoints qui lisent un formulaire (IFormFile) exigent par défaut un jeton
            // antiforgery. La protection CSRF est ici assurée par le cookie de session en
            // SameSite=Lax (cf. Auth/AuthEndpoints.cs) : un formulaire piégé sur un autre site
            // envoie son POST sans ce cookie, et reçoit donc un 401.
            .DisableAntiforgery()
            .WithSummary("Dépose une image (glisser-déposer du back-office) et renvoie son nom.");

        return routes;
    }

    private static Results<PhysicalFileHttpResult, NotFound> Get(string fileName, ImageStore images, HttpContext context)
    {
        var path = images.Find(fileName);
        if (path is null) return TypedResults.NotFound();

        var headers = context.Response.Headers;

        // Un nom du dossier ne change jamais de contenu (empreinte pour les dépôts, et l'import ne
        // remplace jamais un fichier existant) : le navigateur peut garder l'image un an sans revalider.
        headers.CacheControl = "public, max-age=31536000, immutable";

        // Un SVG ouvert directement (et non via <img>) est un document capable d'exécuter du script
        // sur l'origine du site. Cette politique le lui interdit, et nosniff empêche le navigateur de
        // réinterpréter un fichier autrement que selon son Content-Type.
        headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; sandbox";
        headers.XContentTypeOptions = "nosniff";

        // PhysicalFile gère lui-même If-Modified-Since (304) et les requêtes partielles.
        return TypedResults.PhysicalFile(path, ImageStore.ContentTypeOf(fileName), lastModified: File.GetLastWriteTimeUtc(path));
    }

    public sealed record UploadedImage(string FileName);

    /// <param name="file">Nom du champ multipart attendu : <c>file</c>.</param>
    private static async Task<Results<Ok<UploadedImage>, ValidationProblem>> Upload(
        IFormFile file, ImageStore images, CancellationToken ct)
    {
        if (file.Length is 0 or > ImageStore.MaxBytes)
        {
            return Invalid($"L'image doit faire au plus {ImageStore.MaxBytes / 1024 / 1024} Mo.");
        }

        if (!ImageStore.HasAllowedExtension(file.FileName))
        {
            return Invalid($"Formats acceptés : {string.Join(", ", ImageStore.AllowedExtensions)}.");
        }

        await using var stream = file.OpenReadStream();
        var name = await images.SaveAsync(stream, Path.GetExtension(file.FileName), ct);

        return TypedResults.Ok(new UploadedImage(name));

        static ValidationProblem Invalid(string message) =>
            TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [message] });
    }
}
