using Microsoft.EntityFrameworkCore;
using SiteVitrine.Api.Data;
using SiteVitrine.Api.Images;
using SiteVitrine.Api.Resources;

namespace SiteVitrine.Api.Content;

/// <summary>Bilan d'un import, renvoyé au back-office et journalisé par la commande CLI.</summary>
public sealed record ImportReport(
    int Created,
    int Updated,
    int Unchanged,
    int Removed,
    IReadOnlyList<string> OrphanSlugs)
{
    public string Summary => $"{Created} créée(s), {Updated} mise(s) à jour, {Unchanged} inchangée(s), "
        + $"{Removed} supprimée(s), {OrphanSlugs.Count} orpheline(s) en base.";
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
    /// Supprime les ressources présentes en base mais absentes du fichier. Désactivé par défaut :
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

        // Une seule requête pour tout charger, indexée par slug. Ici le tracking d'EF est utile
        // (contrairement aux lectures publiques) : modifier une entité suivie suffit à produire
        // l'UPDATE, et le ChangeTracker nous dit ensuite lesquelles ont réellement changé.
        var existing = await db.Resources.ToDictionaryAsync(r => r.Slug, ct);
        var imagesBefore = existing.Values.Select(r => r.ImageFile).ToList();
        var seen = new HashSet<string>();
        var created = 0;

        // L'index dans le fichier devient l'ordre d'affichage : réordonner le YAML réordonne le site.
        foreach (var (content, index) in file.Resources.Select((c, i) => (c, i)))
        {
            seen.Add(content.Slug);

            if (existing.TryGetValue(content.Slug, out var resource))
            {
                content.ApplyTo(resource);
            }
            else
            {
                resource = content.ToNewResource();
                db.Resources.Add(resource);
                created++;
            }

            resource.SortOrder = index;
        }

        var orphans = existing.Keys.Where(slug => !seen.Contains(slug)).Order().ToList();
        var removed = 0;

        if (prune && orphans.Count > 0)
        {
            db.Resources.RemoveRange(orphans.Select(slug => existing[slug]));
            removed = orphans.Count;
        }

        // À lire avant SaveChanges : après, tout est repassé à Unchanged.
        var updated = db.ChangeTracker.Entries<Resource>().Count(e => e.State == EntityState.Modified);

        await db.SaveChangesAsync(ct);

        // Les images remplacées par le fichier, ou dont la ressource a été élaguée.
        await images.DeleteUnusedAsync(db, released: imagesBefore, ct);

        return new ImportReport(
            Created: created,
            Updated: updated,
            Unchanged: file.Resources.Count - created - updated,
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

        return new ContentFile { Resources = resources };
    }

    /// <summary>
    /// Vérifie le fichier avant d'écrire quoi que ce soit : mieux vaut un message clair qu'une
    /// violation de contrainte remontée par PostgreSQL au milieu du SaveChanges.
    /// </summary>
    private static void Validate(ContentFile file, ImageStore images, string imagesSourceDirectory)
    {
        var errors = new List<string>();

        foreach (var (content, index) in file.Resources.Select((c, i) => (c, i)))
        {
            // L'entrée n'a pas forcément de slug utilisable dans le message : on la situe alors par son rang.
            var where = string.IsNullOrWhiteSpace(content.Slug) ? $"entrée n°{index + 1}" : content.Slug;
            var entryErrors = content.Validate();

            errors.AddRange(entryErrors.Select(error => $"{where}, {error.Key} : {error.Value[0]}"));

            var image = content.ImageFile;
            if (!entryErrors.ContainsKey("imageFile")
                && !string.IsNullOrWhiteSpace(image)
                && !images.Exists(image)
                && !File.Exists(Path.Combine(imagesSourceDirectory, image)))
            {
                errors.Add($"{where}, imageFile : « {image} » absente de {imagesSourceDirectory}.");
            }
        }

        var duplicates = file.Resources
            .GroupBy(r => r.Slug)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1)
            .Select(g => g.Key);

        errors.AddRange(duplicates.Select(slug => $"{slug} : slug en double."));

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Fichier de contenu invalide :" + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }
    }
}
