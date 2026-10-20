using SiteVitrine.Api.Content;
using SiteVitrine.Api.Images;

namespace SiteVitrine.Api.Resources;

/// <summary>
/// Les champs éditables d'une ressource : tout <see cref="Resource"/> sauf ses deux champs techniques.
///
/// C'est la forme commune aux deux façons d'écrire du contenu — une entrée de
/// <c>content/content.yaml</c> (import) et le corps des requêtes du back-office. Validation et
/// recopie vers l'entité n'existent ainsi qu'en un seul exemplaire.
///
/// Classe mutable à constructeur vide plutôt que record : YamlDotNet instancie par réflexion et ne
/// sait pas satisfaire les membres <c>required</c> (cf. ContentFile).
/// </summary>
public sealed class ResourceContent : IContentFor<Resource>
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ImageFile { get; set; }
    public string? SiteUrl { get; set; }
    public string Avis { get; set; } = string.Empty;
    public List<string> Positifs { get; set; } = [];
    public List<string> Negatifs { get; set; } = [];
    public decimal Note { get; set; }

    /// <summary>
    /// Règles qui ne dépendent que des champs eux-mêmes. L'unicité du slug et l'existence de l'image
    /// dépendent du contexte (import ou back-office) : c'est à l'appelant de les vérifier.
    /// </summary>
    /// <returns>
    /// Un message par champ en erreur, indexé par le nom du champ en camelCase — le même que dans le
    /// JSON et le YAML, ce qui permet au frontend d'afficher l'erreur sous le bon champ. Vide si valide.
    /// </returns>
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        // TryAdd : on ne garde que la première erreur d'un champ, la plus fondamentale.
        void Fail(string field, string message) => errors.TryAdd(field, [message]);

        if (string.IsNullOrWhiteSpace(Slug)) Fail("slug", "Obligatoire.");
        if (!ContentRules.IsValidSlug(Slug)) Fail("slug", ContentRules.SlugMessage);
        if (Slug.Length > Resource.SlugMaxLength) Fail("slug", $"{Resource.SlugMaxLength} caractères maximum.");

        if (string.IsNullOrWhiteSpace(Title)) Fail("title", "Obligatoire.");
        if (Title.Length > Resource.TitleMaxLength) Fail("title", $"{Resource.TitleMaxLength} caractères maximum.");

        if (string.IsNullOrWhiteSpace(Avis)) Fail("avis", "Obligatoire.");

        if (Note is < 0 or > 5) Fail("note", "Entre 0 et 5.");

        if (!string.IsNullOrWhiteSpace(ImageFile) && !ImageStore.IsValidName(ImageFile))
        {
            Fail("imageFile", "Nom de fichier invalide.");
        }

        if (!string.IsNullOrWhiteSpace(SiteUrl))
        {
            if (!ContentRules.IsWebUrl(SiteUrl)) Fail("siteUrl", ContentRules.UrlMessage);
            if (SiteUrl.Length > Resource.SiteUrlMaxLength) Fail("siteUrl", $"{Resource.SiteUrlMaxLength} caractères maximum.");
        }

        return errors;
    }

    /// <summary>
    /// Recopie ces champs sur une entité existante. Si elle est suivie par EF, le prochain
    /// <c>SaveChangesAsync()</c> produira l'UPDATE des seules colonnes réellement modifiées.
    /// </summary>
    public void ApplyTo(Resource resource)
    {
        resource.Slug = Slug;
        resource.Title = Title;
        // Un champ facultatif laissé vide dans un formulaire arrive en "" : on stocke null, pour
        // qu'« aucune image » n'ait qu'une seule représentation en base.
        resource.ImageFile = NullIfBlank(ImageFile);
        resource.SiteUrl = NullIfBlank(SiteUrl);
        resource.Avis = Avis;
        resource.Positifs = Positifs;
        resource.Negatifs = Negatifs;
        resource.Note = Note;
    }

    /// <summary>Crée une entité à partir de ces champs (son <c>SortOrder</c> reste à fixer).</summary>
    public Resource ToNewResource()
    {
        var resource = new Resource { Slug = Slug, Title = Title, Avis = Avis };
        ApplyTo(resource);
        return resource;
    }

    /// <summary>Implémentation explicite : <see cref="ToNewResource"/> en porte le nom métier.</summary>
    Resource IContentFor<Resource>.ToNew() => ToNewResource();

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
