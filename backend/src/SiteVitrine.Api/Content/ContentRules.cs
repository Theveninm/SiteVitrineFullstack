using System.Text.RegularExpressions;

namespace SiteVitrine.Api.Content;

/// <summary>
/// Les règles de validation communes à tous les contenus éditables (ressources, projets…).
///
/// Elles vivent ici plutôt que dans chaque <c>XxxContent</c> parce qu'un slug est un slug : la même
/// forme, le même message d'erreur, et une seule expression régulière à corriger le jour où la règle
/// change. Les règles propres à un type de contenu (une note entre 0 et 5) restent, elles, dans son
/// propre <c>Validate()</c>.
/// </summary>
public static partial class ContentRules
{
    /// <summary>Message associé à <see cref="IsValidSlug"/> — le même partout, d'où sa place ici.</summary>
    public const string SlugMessage = "Minuscules, chiffres et tirets uniquement (ex. cs-players-guide).";

    public const string UrlMessage = "Adresse complète attendue (https://…).";

    /// <summary>
    /// Un identifiant stable et lisible : minuscules, chiffres, tirets simples en séparateur.
    /// Pas d'accent ni d'espace, puisqu'il apparaît tel quel dans les URL de l'API.
    /// </summary>
    public static bool IsValidSlug(string slug) => SlugFormat().IsMatch(slug);

    /// <summary>Une adresse web absolue, en http ou https — pas un <c>file://</c> ni un chemin relatif.</summary>
    public static bool IsWebUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// [GeneratedRegex] : l'expression est compilée en C# à la compilation par un générateur de code,
    /// au lieu d'être analysée à l'exécution. D'où la méthode <c>partial</c> sans corps, et le
    /// <c>partial</c> sur la classe.
    /// </summary>
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugFormat();
}
