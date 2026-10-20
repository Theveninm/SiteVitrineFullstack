using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SiteVitrine.Api.Content;

namespace SiteVitrine.Api.Projects;

/// <summary>
/// Un projet présenté sur la page « Projets ». Miroir côté serveur de l'interface <c>Project</c> du
/// frontend (project.model.ts).
///
/// Même parti que <see cref="Resources.Resource"/> : la classe est sérialisée telle quelle par les
/// endpoints, les champs de contenu étant exactement ce que le site affiche ; les deux champs
/// techniques sont masqués par <c>[JsonIgnore]</c>. En écriture, c'est <see cref="ProjectContent"/>
/// qui sert de corps de requête.
/// </summary>
public sealed class Project : IOrderedContent
{
    // Longueurs maximales, partagées par la configuration EF (taille des colonnes) et par la
    // validation (message clair avant d'atteindre la base).
    public const int SlugMaxLength = 120;
    public const int TitleMaxLength = 200;
    public const int UrlMaxLength = 500;
    public const int TechMaxLength = 60;

    /// <summary>Clé primaire technique, auto-incrémentée par convention EF.</summary>
    [JsonIgnore]
    public int Id { get; set; }

    /// <inheritdoc cref="IOrderedContent.Slug"/>
    public required string Slug { get; set; }

    public required string Title { get; set; }

    /// <summary>Présentation du projet, en texte libre (l'équivalent de l'avis d'une ressource).</summary>
    public required string Description { get; set; }

    /// <summary>
    /// Principales technologies, affichées en étiquettes. Comme les points positifs d'une ressource :
    /// un <c>text[]</c> PostgreSQL mappé par Npgsql sur une <c>List&lt;string&gt;</c>, sans table de
    /// jointure — ces valeurs n'ont ni identité propre ni attributs, et ne sont jamais interrogées
    /// indépendamment de leur projet.
    /// </summary>
    public List<string> Techs { get; set; } = [];

    /// <summary>Lien vers le dépôt. Facultatif : tous les projets ne sont pas publiés.</summary>
    public string? GithubUrl { get; set; }

    /// <summary>Lien vers une démonstration en ligne, quand le projet en a une.</summary>
    public string? DemoUrl { get; set; }

    /// <inheritdoc cref="IOrderedContent.SortOrder"/>
    /// <remarks>Inutile au frontend, qui reçoit déjà la liste triée — d'où le [JsonIgnore].</remarks>
    [JsonIgnore]
    public int SortOrder { get; set; }
}

/// <summary>
/// Configuration EF de l'entité (« Fluent API »). Découverte automatiquement par
/// <c>ApplyConfigurationsFromAssembly</c> dans AppDbContext.
/// </summary>
public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");

        // Unicité du slug : garantie par la base, pas seulement par le code applicatif.
        builder.HasIndex(p => p.Slug).IsUnique();

        builder.Property(p => p.Slug).HasMaxLength(Project.SlugMaxLength);
        builder.Property(p => p.Title).HasMaxLength(Project.TitleMaxLength);
        builder.Property(p => p.GithubUrl).HasMaxLength(Project.UrlMaxLength);
        builder.Property(p => p.DemoUrl).HasMaxLength(Project.UrlMaxLength);
    }
}
