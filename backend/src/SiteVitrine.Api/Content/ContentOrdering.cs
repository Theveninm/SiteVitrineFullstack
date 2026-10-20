using Microsoft.EntityFrameworkCore;

namespace SiteVitrine.Api.Content;

/// <summary>
/// Ce que partagent tous les contenus affichés en liste sur le site : une clé métier lisible et un
/// rang d'affichage. C'est ce contrat minimal qui permet aux helpers de <see cref="ContentOrdering"/>
/// d'être écrits une seule fois pour les ressources comme pour les projets.
/// </summary>
public interface IOrderedContent
{
    /// <summary>Identifiant stable et lisible, unique dans sa table (ex. « cs-players-guide »).</summary>
    string Slug { get; set; }

    /// <summary>Rang d'affichage sur le site ; l'ordre d'insertion en base n'est pas garanti.</summary>
    int SortOrder { get; set; }
}

/// <summary>
/// Le placement et le réordonnancement, identiques pour chaque type de contenu listé : les écrire une
/// fois ici évite que la correction d'un bug d'ordre ne doive être reportée dans chaque feature.
/// </summary>
public static class ContentOrdering
{
    /// <summary>Rang à donner à une nouvelle entrée, placée en fin de liste.</summary>
    public static async Task<int> NextSortOrderAsync<T>(this DbSet<T> set, CancellationToken ct)
        where T : class, IOrderedContent =>
        // MaxAsync sur un int? : renvoie null (et non une exception) quand la table est vide.
        (await set.MaxAsync(e => (int?)e.SortOrder, ct) ?? -1) + 1;

    /// <summary>
    /// Fixe le rang de chaque entrée d'après sa position dans <paramref name="slugs"/>. Les entités
    /// sont chargées suivies par EF : le <c>SaveChangesAsync</c> de l'appelant produira les UPDATE.
    /// </summary>
    /// <param name="slugs">Tous les slugs de la table, dans le nouvel ordre d'affichage.</param>
    /// <returns>
    /// <c>false</c> si la liste reçue ne décrit pas exactement la table — une entrée ajoutée ou
    /// supprimée entre-temps (depuis un autre onglet) rendrait l'ordre demandé ambigu, et rien n'est
    /// alors modifié. À l'appelant de formuler l'erreur dans les termes de sa feature.
    /// </returns>
    public static async Task<bool> TryApplyOrderAsync<T>(
        this DbSet<T> set, string[] slugs, CancellationToken ct)
        where T : class, IOrderedContent
    {
        var entities = await set.ToDictionaryAsync(e => e.Slug, ct);

        if (slugs.Length != entities.Count || !entities.Keys.ToHashSet().SetEquals(slugs))
        {
            return false;
        }

        foreach (var (slug, index) in slugs.Select((slug, index) => (slug, index)))
        {
            entities[slug].SortOrder = index;
        }

        return true;
    }
}
