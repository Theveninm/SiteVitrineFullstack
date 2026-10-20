using Microsoft.EntityFrameworkCore;
using SiteVitrine.Api.Data;
using SiteVitrine.Api.Images;
using SiteVitrine.Api.Projects;
using SiteVitrine.Api.Resources;

namespace SiteVitrine.Api.Content;

/// <summary>Bilan d'un import pour un type de contenu.</summary>
/// <param name="OrphanSlugs">Présents en base, absents du fichier. Vide si <c>prune</c> les a supprimés.</param>
public sealed record ImportCounts(
    int Created,
    int Updated,
    int Unchanged,
    int Removed,
    IReadOnlyList<string> OrphanSlugs)
{
    public string Summary => $"{Created} créé(s), {Updated} mis à jour, {Unchanged} inchangé(s), "
        + $"{Removed} supprimé(s), {OrphanSlugs.Count} orphelin(s) en base.";
}

/// <summary>Bilan d'un import, renvoyé au back-office et journalisé par la commande CLI.</summary>
public sealed record ImportReport(ImportCounts Resources, ImportCounts Projects)
{
    public string Summary => $"ressources — {Resources.Summary} ; projets — {Projects.Summary}";

    /// <summary>
    /// Les orphelins des deux types, préfixés par leur section du fichier : le message
    /// d'avertissement doit dire dans quelle liste du YAML l'entrée manque.
    /// </summary>
    public IReadOnlyList<string> OrphanSlugs =>
    [
        .. Resources.OrphanSlugs.Select(slug => $"resources/{slug}"),
        .. Projects.OrphanSlugs.Select(slug => $"projects/{slug}"),
    ];
}

/// <summary>
/// Transfère le contenu du fichier YAML vers la base, et inversement.
///
/// La base fait foi : le contenu s'édite dans le back-office. Le fichier sert à charger une base
/// neuve (avec les images de <c>content/images/</c>) et à garder une copie lisible du contenu.
/// L'import n'est <b>jamais</b> automatique : il se déclenche par la commande <c>import</c> ou depuis
/// l'administration technique — et il écrase ce qui a été modifié dans le back-office depuis.
/// </summary>
public static class ContentImporter
{
    /// <param name="imagesSourceDirectory">Dossier où prendre les images absentes du dossier de l'API.</param>
    /// <param name="prune">
    /// Supprime les entrées présentes en base mais absentes du fichier. Désactivé par défaut :
    /// sans ce drapeau, l'import ne détruit jamais rien, il se contente de signaler les orphelines.
    /// </param>
    public static async Task<ImportReport> ImportAsync(
        AppDbContext db,
        ContentFile file,
        ImageStore images,
        string imagesSourceDirectory,
        bool prune = false,
        CancellationToken ct = default)
    {
        Validate(file, images, imagesSourceDirectory);

        images.CopyMissingFrom(
            imagesSourceDirectory,
            file.Resources.Select(r => r.ImageFile).Where(name => !string.IsNullOrWhiteSpace(name))!);

        // Relevé avant toute écriture : ce sont les images qu'un remplacement va libérer, et dont le
        // ménage devra disposer une fois la base à jour.
        var imagesBefore = await db.Resources.Select(r => r.ImageFile).ToListAsync(ct);

        var resources = await UpsertAsync<Resource, ResourceContent>(db, db.Resources, file.Resources, prune, ct);
        var projects = await UpsertAsync<Project, ProjectContent>(db, db.Projects, file.Projects, prune, ct);

        // Un seul SaveChanges pour les deux types : EF l'enveloppe dans une transaction, donc l'import
        // est tout ou rien.
        await db.SaveChangesAsync(ct);

        // Les images remplacées par le fichier, ou dont la ressource a été élaguée.
        await images.DeleteUnusedAsync(db, released: imagesBefore, ct);

        return new ImportReport(resources, projects);
    }

    /// <summary>
    /// L'algorithme d'import, commun à tous les types de contenu : apparier par slug, mettre à jour
    /// ou créer, numéroter l'ordre d'affichage, et relever les orphelins.
    ///
    /// Générique sur deux types parce qu'il en faut deux pour décrire la correspondance : l'entité en
    /// base (<typeparamref name="TEntity"/>) et les champs du fichier (<typeparamref name="TContent"/>).
    /// Les contraintes <c>where</c> sont ce qui rend le corps possible : la première garantit un
    /// <c>Slug</c> et un <c>SortOrder</c>, la seconde que le contenu sait se valider et se recopier
    /// sur l'entité (cf. <see cref="IContentFor{TEntity}"/>).
    ///
    /// Rien n'est enregistré ici : les entités chargées sont suivies par EF et c'est l'appelant qui
    /// déclenche le <c>SaveChangesAsync</c>, une fois tous les types traités.
    /// </summary>
    private static async Task<ImportCounts> UpsertAsync<TEntity, TContent>(
        AppDbContext db, DbSet<TEntity> set, List<TContent> contents, bool prune, CancellationToken ct)
        where TEntity : class, IOrderedContent
        where TContent : IContentFor<TEntity>
    {
        // Une seule requête pour tout charger, indexée par slug. Ici le tracking d'EF est utile
        // (contrairement aux lectures publiques) : modifier une entité suivie suffit à produire
        // l'UPDATE, et le ChangeTracker nous dit ensuite lesquelles ont réellement changé.
        var existing = await set.ToDictionaryAsync(e => e.Slug, ct);
        var seen = new HashSet<string>();
        var created = 0;

        // L'index dans le fichier devient l'ordre d'affichage : réordonner le YAML réordonne le site.
        foreach (var (content, index) in contents.Select((c, i) => (c, i)))
        {
            seen.Add(content.Slug);

            if (existing.TryGetValue(content.Slug, out var entity))
            {
                content.ApplyTo(entity);
            }
            else
            {
                entity = content.ToNew();
                set.Add(entity);
                created++;
            }

            entity.SortOrder = index;
        }

        var orphans = existing.Keys.Where(slug => !seen.Contains(slug)).Order().ToList();
        var removed = 0;

        if (prune && orphans.Count > 0)
        {
            set.RemoveRange(orphans.Select(slug => existing[slug]));
            removed = orphans.Count;
        }

        // À lire avant SaveChanges : après, tout est repassé à Unchanged.
        var updated = db.ChangeTracker.Entries<TEntity>().Count(e => e.State == EntityState.Modified);

        return new ImportCounts(
            Created: created,
            Updated: updated,
            Unchanged: contents.Count - created - updated,
            Removed: removed,
            OrphanSlugs: prune ? [] : orphans);
    }

    /// <summary>
    /// Produit le fichier de contenu à partir de la base : une copie lisible de ce qui a été saisi dans
    /// le back-office, à committer dans <c>content/</c> (les images se récupèrent à part, cf. README).
    /// </summary>
    public static async Task<ContentFile> ExportAsync(AppDbContext db, CancellationToken ct = default)
    {
        var resources = await db.Resources
            .AsNoTracking()
            .OrderBy(r => r.SortOrder)
            .Select(r => new ResourceContent
            {
                Slug = r.Slug,
                Title = r.Title,
                ImageFile = r.ImageFile,
                SiteUrl = r.SiteUrl,
                Avis = r.Avis,
                Positifs = r.Positifs,
                Negatifs = r.Negatifs,
                Note = r.Note,
            })
            .ToListAsync(ct);

        var projects = await db.Projects
            .AsNoTracking()
            .OrderBy(p => p.SortOrder)
            .Select(p => new ProjectContent
            {
                Slug = p.Slug,
                Title = p.Title,
                Description = p.Description,
                Techs = p.Techs,
                GithubUrl = p.GithubUrl,
                DemoUrl = p.DemoUrl,
            })
            .ToListAsync(ct);

        return new ContentFile { Resources = resources, Projects = projects };
    }

    /// <summary>
    /// Vérifie le fichier avant d'écrire quoi que ce soit : mieux vaut un message clair qu'une
    /// violation de contrainte remontée par PostgreSQL au milieu du SaveChanges.
    /// </summary>
    private static void Validate(ContentFile file, ImageStore images, string imagesSourceDirectory)
    {
        var errors = new List<string>();

        CollectEntryErrors<Resource, ResourceContent>(file.Resources, "ressource", errors);
        CollectEntryErrors<Project, ProjectContent>(file.Projects, "projet", errors);

        // Propre aux ressources : l'image doit être soit déjà dans le dossier de l'API, soit présente
        // dans content/images/ pour que l'import l'y copie.
        foreach (var (content, index) in file.Resources.Select((c, i) => (c, i)))
        {
            var image = content.ImageFile;

            if (!string.IsNullOrWhiteSpace(image)
                && ImageStore.IsValidName(image)
                && !images.Exists(image)
                && !File.Exists(Path.Combine(imagesSourceDirectory, image)))
            {
                errors.Add($"ressource {Describe(content.Slug, index)}, "
                    + $"imageFile : « {image} » absente de {imagesSourceDirectory}.");
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Fichier de contenu invalide :" + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }
    }

    /// <summary>Valide chaque entrée d'une section et repère les slugs en double.</summary>
    /// <param name="label">Nom du type au singulier, pour situer l'erreur (« ressource », « projet »).</param>
    private static void CollectEntryErrors<TEntity, TContent>(
        List<TContent> contents, string label, List<string> errors)
        where TEntity : class, IOrderedContent
        where TContent : IContentFor<TEntity>
    {
        foreach (var (content, index) in contents.Select((c, i) => (c, i)))
        {
            var where = Describe(content.Slug, index);

            errors.AddRange(content.Validate()
                .Select(error => $"{label} {where}, {error.Key} : {error.Value[0]}"));
        }

        var duplicates = contents
            .GroupBy(c => c.Slug)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1)
            .Select(group => group.Key);

        errors.AddRange(duplicates.Select(slug => $"{label} {slug} : slug en double."));
    }

    /// <summary>
    /// Situe une entrée dans les messages d'erreur. Elle n'a pas forcément de slug utilisable
    /// (c'est peut-être justement ce qui manque) : on la repère alors par son rang.
    /// </summary>
    private static string Describe(string slug, int index) =>
        string.IsNullOrWhiteSpace(slug) ? $"n°{index + 1}" : slug;
}
