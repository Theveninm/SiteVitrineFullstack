namespace SiteVitrine.Api.Content;

/// <summary>
/// Le contrat qu'un <c>XxxContent</c> (les champs éditables d'un contenu) remplit vis-à-vis de son
/// entité : savoir se nommer, se recopier sur une entité existante, et en créer une neuve.
///
/// Son unique raison d'être est de permettre à <see cref="ContentImporter"/> d'écrire <b>une seule
/// fois</b> l'algorithme d'import — apparier par slug, créer ou mettre à jour, numéroter l'ordre,
/// repérer les orphelins — au lieu d'en tenir une copie par type de contenu. C'est le seul endroit
/// qui s'en sert : une feature n'a pas à le connaître pour exister.
/// </summary>
/// <typeparam name="TEntity">L'entité EF correspondante (Resource, Project…).</typeparam>
public interface IContentFor<TEntity> where TEntity : class, IOrderedContent
{
    /// <summary>Clé métier sur laquelle l'import apparie le fichier et la base.</summary>
    string Slug { get; }

    /// <summary>Recopie ces champs sur une entité existante.</summary>
    void ApplyTo(TEntity entity);

    /// <summary>Crée une entité à partir de ces champs (son <c>SortOrder</c> reste à fixer).</summary>
    TEntity ToNew();

    /// <summary>Les règles de validation du type, indexées par nom de champ en camelCase.</summary>
    Dictionary<string, string[]> Validate();
}
