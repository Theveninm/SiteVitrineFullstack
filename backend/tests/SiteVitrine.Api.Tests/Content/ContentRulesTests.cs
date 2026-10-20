using SiteVitrine.Api.Content;

namespace SiteVitrine.Api.Tests.Content;

/// <summary>
/// Les règles partagées par tous les contenus. Elles ne dépendent de rien : ni base, ni fichier.
/// </summary>
public sealed class ContentRulesTests
{
    // [Theory] + [InlineData] : le même test rejoué avec plusieurs jeux de valeurs, chacun compté
    // comme un test à part — un échec nomme donc la valeur fautive.
    [Theory]
    [InlineData("linqpad")]
    [InlineData("cs-players-guide")]
    [InlineData("c10")]
    [InlineData("a-b-c-d")]
    public void IsValidSlug_accepte_les_slugs_bien_formés(string slug) =>
        Assert.True(ContentRules.IsValidSlug(slug));

    [Theory]
    [InlineData("")]
    [InlineData("LINQPad")] // majuscules
    [InlineData("linq pad")] // espace
    [InlineData("linq_pad")] // souligné
    [InlineData("-linqpad")] // tiret en bordure
    [InlineData("linqpad-")]
    [InlineData("linq--pad")] // tiret doublé
    [InlineData("ressource-éditée")] // accent
    [InlineData("linqpad/..")] // ce qui ressemblerait à un chemin
    public void IsValidSlug_refuse_tout_le_reste(string slug) =>
        Assert.False(ContentRules.IsValidSlug(slug));

    [Theory]
    [InlineData("https://csharpplayersguide.com/")]
    [InlineData("http://localhost:4200/")]
    public void IsWebUrl_accepte_http_et_https(string url) => Assert.True(ContentRules.IsWebUrl(url));

    [Theory]
    [InlineData("")]
    [InlineData("csharpplayersguide.com")] // sans schéma : pas une adresse absolue
    [InlineData("/ressources")] // chemin relatif
    [InlineData("ftp://exemple.fr/fichier")]
    [InlineData("file:///C:/Windows/System32")]
    [InlineData("javascript:alert(1)")]
    public void IsWebUrl_refuse_les_autres_schémas(string url) =>
        Assert.False(ContentRules.IsWebUrl(url));
}
