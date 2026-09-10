using Microsoft.EntityFrameworkCore;
using SiteVitrine.Api.Data;

namespace SiteVitrine.Api.Resources;

public static class ResourcesEndpoints
{
    /// <summary>
    /// Enregistre les routes publiques de la feature. Program.cs appelle cette méthode et n'a donc
    /// pas à connaître le détail des routes : ajouter une feature = un dossier + un appel à son
    /// <c>MapXxxEndpoints</c>.
    ///
    /// Ces routes sont en lecture seule. Tout ce qui écrit vit dans <c>Admin/</c>, sur le port
    /// d'administration — la séparation est celle du port, pas seulement celle du code.
    /// </summary>
    public static IEndpointRouteBuilder MapResourcesEndpoints(this IEndpointRouteBuilder routes)
    {
        // Un "group" préfixe toutes les routes du bloc et permet d'y appliquer d'un coup des
        // métadonnées (ici le tag OpenAPI, qui regroupe les routes dans l'interface du back-office).
        var group = routes.MapGroup("/api/resources").WithTags("Resources");

        group.MapGet("/", GetAll)
            .WithName("GetResources")
            .WithSummary("Liste toutes les ressources, dans l'ordre d'affichage du site.");

        group.MapGet("/{slug}", GetBySlug)
            .WithName("GetResourceBySlug")
            .WithSummary("Récupère une ressource par son slug.");

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
}
