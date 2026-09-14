using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteVitrine.Api.Data;
using SiteVitrine.Api.Images;

namespace SiteVitrine.Api.Resources;

public static class ResourcesEndpoints
{
    /// <summary>
    /// Enregistre les routes de la feature. Program.cs appelle cette méthode et n'a donc pas à
    /// connaître le détail des routes : ajouter une feature = un dossier + un appel à son
    /// <c>MapXxxEndpoints</c>.
    ///
    /// Lecture publique ; écriture réservée à une session du back-office. Les deux partagent les
    /// mêmes URL, seule l'exigence d'authentification les distingue.
    /// </summary>
    public static IEndpointRouteBuilder MapResourcesEndpoints(this IEndpointRouteBuilder routes)
    {
        // Un "group" préfixe toutes les routes du bloc et permet d'y appliquer d'un coup des
        // métadonnées (ici le tag OpenAPI, qui regroupe les routes dans l'interface Scalar).
        var group = routes.MapGroup("/api/resources").WithTags("Resources");

        group.MapGet("/", GetAll)
            .WithName("GetResources")
            .WithSummary("Liste toutes les ressources, dans l'ordre d'affichage du site.");

        group.MapGet("/{slug}", GetBySlug)
            .WithName("GetResourceBySlug")
            .WithSummary("Récupère une ressource par son slug.");

        // Sous-groupe sans préfixe supplémentaire, pour poser RequireAuthorization() une seule fois :
        // sans cookie de session valide, la requête reçoit un 401 avant d'atteindre le handler.
        var writes = group.MapGroup("").RequireAuthorization();

        writes.MapPost("/", Create)
            .WithSummary("Crée une ressource, placée en fin de liste.");

        writes.MapPut("/{slug}", Update)
            .WithSummary("Modifie une ressource (le slug lui-même peut changer).");

        writes.MapDelete("/{slug}", Delete)
            .WithSummary("Supprime une ressource, et son image si plus rien ne l'utilise.");

        writes.MapPost("/reorder", Reorder)
            .WithSummary("Fixe l'ordre d'affichage à partir de la liste complète des slugs.");

        return routes;
    }

    // Les paramètres des handlers sont résolus par injection de dépendances : ASP.NET voit un
    // AppDbContext enregistré dans le conteneur et le fournit (instance propre à cette requête).
    // CancellationToken est fourni par le framework et s'annule si le client coupe la connexion —
    // le passer à EF permet d'abandonner la requête SQL au lieu de la mener à son terme pour rien.
    private static async Task<IResult> GetAll(AppDbContext db, CancellationToken ct)
    {
        var resources = await db.Resources
            // AsNoTracking : on ne fait que lire. Sans ça, EF garderait une copie de chaque entité
            // pour détecter d'éventuelles modifications — coût inutile en lecture pure.
            .AsNoTracking()
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

        return TypedResults.Ok(resources);
    }

    private static async Task<IResult> GetBySlug(string slug, AppDbContext db, CancellationToken ct)
    {
        var resource = await db.Resources
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Slug == slug, ct);

        // TypedResults plutôt que Results : le type de retour est connu statiquement, donc OpenAPI
        // documente correctement les réponses 200/404 sans annotation supplémentaire.
        return resource is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(resource);
    }

    // Results<A, B, C> : l'union des réponses possibles, vérifiée à la compilation et documentée
    // telle quelle dans OpenAPI.
    private static async Task<Results<Created<Resource>, ValidationProblem>> Create(
        ResourceContent content, AppDbContext db, ImageStore images, CancellationToken ct)
    {
        var errors = await ValidateAsync(content, currentSlug: null, db, images, ct);
        if (errors.Count > 0) return TypedResults.ValidationProblem(errors);

        var resource = content.ToNewResource();
        // MaxAsync sur un int? : renvoie null (et non une exception) quand la table est vide.
        resource.SortOrder = (await db.Resources.MaxAsync(r => (int?)r.SortOrder, ct) ?? -1) + 1;

        db.Resources.Add(resource);
        await db.SaveChangesAsync(ct);

        return TypedResults.Created($"/api/resources/{resource.Slug}", resource);
    }

    private static async Task<Results<Ok<Resource>, NotFound, ValidationProblem>> Update(
        string slug, ResourceContent content, AppDbContext db, ImageStore images, CancellationToken ct)
    {
        // Sans AsNoTracking cette fois : l'entité suivie par EF, une fois modifiée, produit l'UPDATE.
        var resource = await db.Resources.FirstOrDefaultAsync(r => r.Slug == slug, ct);
        if (resource is null) return TypedResults.NotFound();

        var errors = await ValidateAsync(content, currentSlug: slug, db, images, ct);
        if (errors.Count > 0) return TypedResults.ValidationProblem(errors);

        var previousImage = resource.ImageFile;
        content.ApplyTo(resource);
        await db.SaveChangesAsync(ct);

        // Après l'écriture seulement : si elle avait échoué, l'ancienne image serait toujours utilisée.
        await images.DeleteUnusedAsync(db, released: [previousImage], ct);

        return TypedResults.Ok(resource);
    }

    private static async Task<Results<NoContent, NotFound>> Delete(
        string slug, AppDbContext db, ImageStore images, CancellationToken ct)
    {
        var resource = await db.Resources.FirstOrDefaultAsync(r => r.Slug == slug, ct);
        if (resource is null) return TypedResults.NotFound();

        db.Resources.Remove(resource);
        await db.SaveChangesAsync(ct);

        await images.DeleteUnusedAsync(db, released: [resource.ImageFile], ct);

        return TypedResults.NoContent();
    }

    /// <param name="slugs">Tous les slugs, dans le nouvel ordre d'affichage.</param>
    private static async Task<Results<NoContent, ValidationProblem>> Reorder(
        [FromBody] string[] slugs, AppDbContext db, CancellationToken ct)
    {
        var resources = await db.Resources.ToDictionaryAsync(r => r.Slug, ct);

        // La liste reçue doit décrire exactement la base. Une ressource ajoutée ou supprimée entre-temps
        // (depuis un autre onglet) rendrait l'ordre demandé ambigu : mieux vaut refuser.
        if (slugs.Length != resources.Count || !resources.Keys.ToHashSet().SetEquals(slugs))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["slugs"] = ["La liste des ressources a changé entre-temps : recharger la page."],
            });
        }

        foreach (var (slug, index) in slugs.Select((slug, index) => (slug, index)))
        {
            resources[slug].SortOrder = index;
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Les règles de <see cref="ResourceContent.Validate"/>, plus celles qui demandent la base ou le
    /// dossier d'images.
    /// </summary>
    /// <param name="currentSlug">Slug de la ressource modifiée ; null pour une création.</param>
    private static async Task<Dictionary<string, string[]>> ValidateAsync(
        ResourceContent content, string? currentSlug, AppDbContext db, ImageStore images, CancellationToken ct)
    {
        var errors = content.Validate();

        if (!errors.ContainsKey("slug")
            && content.Slug != currentSlug
            && await db.Resources.AnyAsync(r => r.Slug == content.Slug, ct))
        {
            errors["slug"] = ["Déjà utilisé par une autre ressource."];
        }

        // Image inconnue : jamais déposée, ou déposée il y a trop longtemps et déjà supprimée par le
        // ménage (formulaire resté ouvert plus d'une heure).
        if (!errors.ContainsKey("imageFile")
            && !string.IsNullOrWhiteSpace(content.ImageFile)
            && !images.Exists(content.ImageFile))
        {
            errors["imageFile"] = ["Image introuvable : la déposer à nouveau."];
        }

        return errors;
    }
}
