using SiteVitrine.Api.Projects;
using SiteVitrine.Api.Resources;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;
using YamlDotNet.Serialization.NamingConventions;

namespace SiteVitrine.Api.Content;

/// <summary>
/// Reflet C# du fichier <c>content/content.yaml</c>. C'est le contrat entre le fichier et
/// l'application : une clé de premier niveau par type de contenu, et sous chacune les champs de son
/// <c>XxxContent</c> — ajouter un champ à <see cref="ResourceContent"/>, c'est ajouter une clé
/// reconnue dans le YAML.
///
/// Volontairement distinct des entités : le fichier ne connaît ni <c>Id</c> (technique) ni
/// <c>SortOrder</c> (déduit de l'ordre des entrées, cf. <see cref="ContentImporter"/>).
/// </summary>
public sealed class ContentFile
{
    public List<ResourceContent> Resources { get; set; } = [];

    public List<ProjectContent> Projects { get; set; } = [];

    // YamlDotNet instancie les objets par réflexion : pas de `required` sur les propriétés de
    // ResourceContent, ce modificateur ferait échouer la construction. La validation se fait à
    // l'import, où l'on peut produire un message parlant plutôt qu'une exception opaque.
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        // Le YAML s'écrit en camelCase (imageFile, siteUrl) et se mappe sur les propriétés PascalCase.
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        // Une clé en double est une faute de frappe, pas une extension silencieuse : on veut l'erreur.
        .WithDuplicateKeyChecking()
        .Build();

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        // N'écrit pas les clés dont la valeur est nulle : un export reste lisible.
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        // Sans ça, un export réécrirait chaque avis en une seule ligne de 1000 caractères, et le
        // fichier deviendrait pénible à éditer après chaque aller-retour base → fichier.
        .WithEventEmitter(next => new LongTextAsLiteralBlock(next))
        .Build();

    /// <summary>
    /// Écrit les longs textes en bloc littéral (<c>|-</c>) au lieu d'une ligne unique. Purement
    /// cosmétique : dans les deux cas le YAML produit se relit à l'identique. Le seuil est arbitraire
    /// et sépare les avis (200 caractères et plus) des titres et points positifs/négatifs.
    /// </summary>
    private sealed class LongTextAsLiteralBlock(IEventEmitter next) : ChainedEventEmitter(next)
    {
        public override void Emit(ScalarEventInfo eventInfo, IEmitter emitter)
        {
            // Le style littéral ne sait pas représenter fidèlement les retours chariot ni les espaces
            // en bordure : dans ces cas-là on laisse YamlDotNet choisir (et échapper) tout seul.
            if (eventInfo.Source.Value is string text
                && text.Length >= 200
                && !text.Contains('\r')
                && text.Trim() == text)
            {
                eventInfo.Style = ScalarStyle.Literal;
            }

            base.Emit(eventInfo, emitter);
        }
    }

    /// <summary>Emplacement du fichier, paramétrable par <c>Content:Path</c> (cf. appsettings.json).</summary>
    public static string PathFrom(IConfiguration config) =>
        config["Content:Path"] ?? throw new InvalidOperationException(
            "Content:Path absent de la configuration (cf. appsettings.json).");

    /// <summary>
    /// Dossier <c>images/</c> à côté du fichier : c'est là que l'import va chercher les images qu'une
    /// base neuve ne possède pas encore.
    /// </summary>
    public static string ImagesDirectoryFrom(IConfiguration config) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(PathFrom(config)))!, "images");

    public static async Task<ContentFile> LoadAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Fichier de contenu introuvable : {Path.GetFullPath(path)}. " +
                "En conteneur, vérifier que le volume ./content est bien monté sur /app/content.",
                path);
        }

        var yaml = await File.ReadAllTextAsync(path, ct);
        return Deserializer.Deserialize<ContentFile>(yaml) ?? new ContentFile();
    }

    public string ToYaml() => Serializer.Serialize(this);
}
