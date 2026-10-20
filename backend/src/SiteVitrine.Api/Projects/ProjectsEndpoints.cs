using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteVitrine.Api.Content;
using SiteVitrine.Api.Data;

namespace SiteVitrine.Api.Projects;

public static class ProjectsEndpoints
{
    /// <summary>
    /// Enregistre les routes de la feature. Même découpage que les ressources : lecture publique,
    /// écriture réservée à une session du back-office, les deux partageant les mêmes URL.
    /// </summary>
    public static IEndpointRouteBuilder MapProjectsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/projects").WithTags("Projects");

        group.MapGet("/", GetAll)
            .WithName("GetProjects")
            .WithSummary("Liste tous les projets, dans l'ordre d'affichage du site.");

        group.MapGet("/{slug}", GetBySlug)
            .WithName("GetProjectBySlug")
            .WithSummary("Récupère un projet par son slug.");

        // Sous-groupe sans préfixe supplémentaire, pour poser RequireAuthorization() une seule fois.
        var writes = group.MapGroup("").RequireAuthorization();

        writes.MapPost("/", Create)
            .WithSummary("Crée un projet, placé en fin de liste.");

        writes.MapPut("/{slug}", Update)
            .WithSummary("Modifie un projet (le slug lui-même peut changer).");

        writes.MapDelete("/{slug}", Delete)
            .WithSummary("Supprime un projet.");

        writes.MapPost("/reorder", Reorder)
            .WithSummary("Fixe l'ordre d'affichage à partir de la liste complète des slugs.");

        return routes;
    }

    private static async Task<IResult> GetAll(AppDbContext db, CancellationToken ct)
    {
        var projects = await db.Projects
            // AsNoTracking : on ne fait que lire, inutile de garder une copie pour la détection
            // des modifications.
            .AsNoTracking()
            .OrderBy(p => p.SortOrder)
            .ToListAsync(ct);

        return TypedResults.Ok(projects);
    }

    private static async Task<IResult> GetBySlug(string slug, AppDbContext db, CancellationToken ct)
    {
        var project = await db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Slug == slug, ct);

        return project is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(project);
    }

    private static async Task<Results<Created<Project>, ValidationProblem>> Create(
        ProjectContent content, AppDbContext db, CancellationToken ct)
    {
        var errors = await ValidateAsync(content, currentSlug: null, db, ct);
        if (errors.Count > 0) return TypedResults.ValidationProblem(errors);

        var project = content.ToNewProject();
        project.SortOrder = await db.Projects.NextSortOrderAsync(ct);

        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);

        return TypedResults.Created($"/api/projects/{project.Slug}", project);
    }

    private static async Task<Results<Ok<Project>, NotFound, ValidationProblem>> Update(
        string slug, ProjectContent content, AppDbContext db, CancellationToken ct)
    {
        // Sans AsNoTracking cette fois : l'entité suivie par EF, une fois modifiée, produit l'UPDATE.
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Slug == slug, ct);
        if (project is null) return TypedResults.NotFound();

        var errors = await ValidateAsync(content, currentSlug: slug, db, ct);
        if (errors.Count > 0) return TypedResults.ValidationProblem(errors);

        content.ApplyTo(project);
        await db.SaveChangesAsync(ct);

        return TypedResults.Ok(project);
    }

    private static async Task<Results<NoContent, NotFound>> Delete(
        string slug, AppDbContext db, CancellationToken ct)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Slug == slug, ct);
        if (project is null) return TypedResults.NotFound();

        db.Projects.Remove(project);
        await db.SaveChangesAsync(ct);

        return TypedResults.NoContent();
    }

    /// <param name="slugs">Tous les slugs, dans le nouvel ordre d'affichage.</param>
    private static async Task<Results<NoContent, ValidationProblem>> Reorder(
        [FromBody] string[] slugs, AppDbContext db, CancellationToken ct)
    {
        if (!await db.Projects.TryApplyOrderAsync(slugs, ct))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["slugs"] = ["La liste des projets a changé entre-temps : recharger la page."],
            });
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Les règles de <see cref="ProjectContent.Validate"/>, plus celle qui demande la base.
    /// </summary>
    /// <param name="currentSlug">Slug du projet modifié ; null pour une création.</param>
    private static async Task<Dictionary<string, string[]>> ValidateAsync(
        ProjectContent content, string? currentSlug, AppDbContext db, CancellationToken ct)
    {
        var errors = content.Validate();

        if (!errors.ContainsKey("slug")
            && content.Slug != currentSlug
            && await db.Projects.AnyAsync(p => p.Slug == content.Slug, ct))
        {
            errors["slug"] = ["Déjà utilisé par un autre projet."];
        }

        return errors;
    }
}
