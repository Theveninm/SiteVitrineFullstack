/**
 * Forme exacte du JSON renvoyé par GET /api/resources : l'entité `Resource` côté ASP.NET Core,
 * dont les deux champs techniques (Id, SortOrder) sont masqués par [JsonIgnore].
 * Le contenu lui-même est édité dans content/resources.yaml, à la racine du dépôt.
 */
export interface Resource {
  /** Identifiant stable et lisible ; sert de clé de suivi dans les boucles @for. */
  slug: string;
  title: string;
  imageUrl?: string;
  siteUrl?: string;
  avis: string;
  positifs: string[];
  negatifs: string[];
  note: number; // Note globale sur 5.
}
