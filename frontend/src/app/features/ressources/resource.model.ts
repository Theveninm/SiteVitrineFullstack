/**
 * Forme exacte du JSON renvoyé par GET /api/resources : l'entité `Resource` côté ASP.NET Core,
 * dont les deux champs techniques (Id, SortOrder) sont masqués par [JsonIgnore].
 * C'est aussi le corps attendu par les écritures du back-office (POST et PUT /api/resources).
 */
export interface Resource {
  /** Identifiant stable et lisible ; sert de clé de suivi dans les boucles @for. */
  slug: string;
  title: string;
  /** Nom du fichier dans le dossier d'images de l'API ; l'URL s'obtient avec resourceImageSrc(). */
  imageFile?: string | null;
  siteUrl?: string | null;
  avis: string;
  positifs: string[];
  negatifs: string[];
  note: number; // Note globale sur 5.
}

/** URL d'une image du contenu : le seul endroit du frontend qui connaît la route de service des images. */
export function resourceImageSrc(imageFile: string): string {
  return `/api/images/${encodeURIComponent(imageFile)}`;
}
