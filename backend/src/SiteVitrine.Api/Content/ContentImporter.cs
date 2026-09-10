using Microsoft.EntityFrameworkCore;
using SiteVitrine.Api.Data;
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
/// L'import n'est <b>jamais</b> automatique : il se déclenche par la commande <c>--import</c> ou
/// depuis le back-office. C'est ce qui permet de garder migration de schéma et chargement de
/// contenu comme deux opérations indépendantes — la première tourne à chaque déploiement, la
/// seconde seulement quand on l'a décidé.
/// </summary>
public static class ContentImporter
{
    /// <param name="prune">
    /// Supprime les ressources présentes en base mais absentes du fichier. Désactivé par défaut :
    /// sans ce drapeau, l'import ne détruit jamais rien, il se contente de signaler les orphelines.
    /// </param>
    public static async Task<ImportReport> ImportAsync(
        AppDbContext db, ContentFile file, bool prune = false, CancellationToken ct = default)
    {
        Validate(file);

        // Une seule requête pour tout charger, indexée par slug. Ici le tracking d'EF est utile
        // (contrairement aux lectures publiques) : modifier une entité suivie suffit à produire
        // l'UPDATE, et le ChangeTracker nous dit ensuite lesquelles ont réellement changé.
        var existing = await db.Resources.ToDictionaryAsync(r => r.Slug, ct);
        var seen = new HashSet<string>();
        var created = 0;

        // L'index dans le fichier devient l'ordre d'affichage : réordonner le YAML réordonne le site.
        foreach (var (content, index) in file.Resources.Select((c, i) => (c, i)))
        {
            seen.Add(content.Slug);

            if (!existing.TryGetValue(content.Slug, out var resource))
            {
                resource = new Resource { Slug = content.Slug, Title = content.Title, Avis = content.Avis };
                db.Resources.Add(resource);
                created++;
            }

            resource.Title = content.Title;
            resource.ImageUrl = content.ImageUrl;
            resource.SiteUrl = content.SiteUrl;
            resource.Avis = content.Avis;
            resource.Positifs = content.Positifs;
            resource.Negatifs = content.Negatifs;
            resource.Note = content.Note;
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

        return new ImportReport(
            Created: created,
            Updated: updated,
            Unchanged: file.Resources.Count - created - updated,
            Removed: removed,
            OrphanSlugs: prune ? [] : orphans);
    }

    /// <summary>
    /// Produit le fichier de contenu à partir de la base. Sert de chemin de retour quand on a édité
    /// directement en base (LinqPad, back-office) : exporter, puis committer le YAML obtenu.
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
                ImageUrl = r.ImageUrl,
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
    private static void Validate(ContentFile file)
    {
        var errors = new List<string>();

        foreach (var (content, index) in file.Resources.Select((c, i) => (c, i)))
        {
            // L'entrée n'a pas encore de slug utilisable dans le message : on la situe par son rang.
            var where = string.IsNullOrWhiteSpace(content.Slug) ? $"entrée n°{index + 1}" : content.Slug;

            if (string.IsNullOrWhiteSpace(content.Slug)) errors.Add($"{where} : `slug` manquant.");
            if (string.IsNullOrWhiteSpace(content.Title)) errors.Add($"{where} : `title` manquant.");
            if (string.IsNullOrWhiteSpace(content.Avis)) errors.Add($"{where} : `avis` manquant.");
            if (content.Note is < 0 or > 5) errors.Add($"{where} : `note` doit être entre 0 et 5.");
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
