using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SiteVitrine.Api.Resources;

/// <summary>
/// Une ressource (livre, outil, cours, chaîne YouTube…) présentée sur la page « Livres &amp; outils ».
/// Miroir côté serveur de l'interface <c>Resource</c> du frontend (resource.model.ts).
///
/// Cette classe est sérialisée telle quelle par les endpoints : les huit champs de contenu sont
/// exactement ce que le site affiche, un DTO intermédiaire ne ferait que les recopier. Les deux champs
/// purement techniques sont masqués par <c>[JsonIgnore]</c>. Le jour où une colonne devra rester
/// privée (notes de travail, statut de publication…), c'est le signal qu'il faut introduire un DTO
/// plutôt qu'empiler les attributs.
///
/// En écriture, c'est <see cref="ResourceContent"/> qui sert de corps de requête.
/// </summary>
public sealed class Resource
{
    // Longueurs maximales, partagées par la configuration EF (taille des colonnes) et par la validation
    // (message clair avant d'atteindre la base).
    public const int SlugMaxLength = 120;
    public const int TitleMaxLength = 200;
    public const int ImageFileMaxLength = 300;
    public const int SiteUrlMaxLength = 500;

    /// <summary>Clé primaire technique. Par convention EF, une propriété nommée <c>Id</c> de type
    /// int devient la clé primaire auto-incrémentée — pas besoin de l'annoter.</summary>
    [JsonIgnore]
    public int Id { get; set; }

    /// <summary>
    /// Identifiant stable et lisible (ex. « cs-players-guide »). Sert de clé métier : c'est sur lui
    /// que l'import apparie le fichier et la base, et c'est lui qu'utilisent les URL de l'API
    /// (/api/resources/cs-players-guide).
    /// </summary>
    public required string Slug { get; set; }

    public required string Title { get; set; }

    /// <summary>
    /// Nom du fichier dans le dossier d'images de l'API (ex. « 3f9a…c1.png »), servi sur
    /// <c>/api/images/{nom}</c>. Un nom plutôt qu'une URL : l'adresse de service peut changer sans
    /// migrer les données.
    /// </summary>
    public string? ImageFile { get; set; }

    /// <summary>Lien vers le site officiel de la ressource.</summary>
    public string? SiteUrl { get; set; }

    /// <summary>Avis rédigé, en texte libre.</summary>
    public required string Avis { get; set; }

    /// <summary>
    /// Points positifs. PostgreSQL sait stocker nativement un tableau (<c>text[]</c>) et Npgsql
    /// mappe <c>List&lt;string&gt;</c> dessus sans table de jointure. On garde donc le modèle
    /// relationnel aussi simple que le modèle TypeScript : ces listes n'ont ni identité propre,
    /// ni attributs, et ne sont jamais interrogées indépendamment de leur ressource.
    /// </summary>
    public List<string> Positifs { get; set; } = [];

    /// <inheritdoc cref="Positifs"/>
    public List<string> Negatifs { get; set; } = [];

    /// <summary>Note globale sur 5. Décimal pour autoriser les demi-notes (le front arrondit).</summary>
    public decimal Note { get; set; }

    /// <summary>
    /// Ordre d'affichage sur la page ; l'ordre d'insertion en base n'est pas garanti. Modifié par le
    /// glisser-déposer du back-office, ou par le rang de l'entrée dans content/resources.yaml lors d'un
    /// import. Inutile au frontend, qui reçoit déjà la liste triée — d'où le [JsonIgnore].
    /// </summary>
    [JsonIgnore]
    public int SortOrder { get; set; }
}

/// <summary>
/// Configuration EF de l'entité (« Fluent API »). Découverte automatiquement par
/// <c>ApplyConfigurationsFromAssembly</c> dans AppDbContext.
/// </summary>
public sealed class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.ToTable("resources");

        // Unicité du slug : garantie par la base, pas seulement par le code applicatif.
        builder.HasIndex(r => r.Slug).IsUnique();

        builder.Property(r => r.Slug).HasMaxLength(Resource.SlugMaxLength);
        builder.Property(r => r.Title).HasMaxLength(Resource.TitleMaxLength);
        builder.Property(r => r.ImageFile).HasMaxLength(Resource.ImageFileMaxLength);
        builder.Property(r => r.SiteUrl).HasMaxLength(Resource.SiteUrlMaxLength);

        // Sans précision explicite, EF choisirait numeric sans échelle. (2,1) = un chiffre avant
        // la virgule, un après : exactement ce qu'il faut pour une note de 0.0 à 5.0.
        builder.Property(r => r.Note).HasPrecision(2, 1);

        // Contrainte de domaine côté base : une note hors [0,5] est refusée même si un endpoint
        // d'écriture oubliait de valider.
        builder.ToTable(t => t.HasCheckConstraint("ck_resources_note_range", "\"Note\" >= 0 AND \"Note\" <= 5"));
    }
}
