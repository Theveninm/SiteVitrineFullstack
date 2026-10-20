using SiteVitrine.Api.Content;

namespace SiteVitrine.Api.Projects;

/// <summary>
/// Les champs éditables d'un projet : tout <see cref="Project"/> sauf ses deux champs techniques.
///
/// C'est la forme commune aux deux façons d'écrire du contenu — une entrée de
/// <c>content/content.yaml</c> (import) et le corps des requêtes du back-office. Validation et
/// recopie vers l'entité n'existent ainsi qu'en un seul exemplaire.
///
/// Classe mutable à constructeur vide plutôt que record : YamlDotNet instancie par réflexion et ne
/// sait pas satisfaire les membres <c>required</c> (cf. ContentFile).
/// </summary>
public sealed class ProjectContent : IContentFor<Project>
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Techs { get; set; } = [];
    public string? GithubUrl { get; set; }
    public string? DemoUrl { get; set; }

    /// <summary>
    /// Règles qui ne dépendent que des champs eux-mêmes. L'unicité du slug dépend du contexte
    /// (import ou back-office) : c'est à l'appelant de la vérifier.
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
        if (Slug.Length > Project.SlugMaxLength) Fail("slug", $"{Project.SlugMaxLength} caractères maximum.");

        if (string.IsNullOrWhiteSpace(Title)) Fail("title", "Obligatoire.");
        if (Title.Length > Project.TitleMaxLength) Fail("title", $"{Project.TitleMaxLength} caractères maximum.");

        if (string.IsNullOrWhiteSpace(Description)) Fail("description", "Obligatoire.");

        // Une étiquette vide s'afficherait comme une pastille creuse, et une trop longue casserait la
        // mise en page : mieux vaut le refuser à la saisie.
        if (Techs.Any(string.IsNullOrWhiteSpace)) Fail("techs", "Aucune technologie ne peut être vide.");
        if (Techs.Any(tech => tech.Length > Project.TechMaxLength))
        {
            Fail("techs", $"{Project.TechMaxLength} caractères maximum par technologie.");
        }

        ValidateUrl("githubUrl", GithubUrl);
        ValidateUrl("demoUrl", DemoUrl);

        return errors;

        void ValidateUrl(string field, string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            if (!ContentRules.IsWebUrl(url)) Fail(field, ContentRules.UrlMessage);
            if (url.Length > Project.UrlMaxLength) Fail(field, $"{Project.UrlMaxLength} caractères maximum.");
        }
    }

    /// <summary>
    /// Recopie ces champs sur une entité existante. Si elle est suivie par EF, le prochain
    /// <c>SaveChangesAsync()</c> produira l'UPDATE des seules colonnes réellement modifiées.
    /// </summary>
    public void ApplyTo(Project project)
    {
        project.Slug = Slug;
        project.Title = Title;
        project.Description = Description;
        project.Techs = Techs;
        // Un champ facultatif laissé vide dans un formulaire arrive en "" : on stocke null, pour
        // qu'« aucun lien » n'ait qu'une seule représentation en base.
        project.GithubUrl = NullIfBlank(GithubUrl);
        project.DemoUrl = NullIfBlank(DemoUrl);
    }

    /// <summary>Crée une entité à partir de ces champs (son <c>SortOrder</c> reste à fixer).</summary>
    public Project ToNewProject()
    {
        var project = new Project { Slug = Slug, Title = Title, Description = Description };
        ApplyTo(project);
        return project;
    }

    /// <summary>Implémentation explicite : <see cref="ToNewProject"/> en porte le nom métier.</summary>
    Project IContentFor<Project>.ToNew() => ToNewProject();

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
