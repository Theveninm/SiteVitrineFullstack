/**
 * Forme exacte du JSON renvoyé par GET /api/projects : l'entité `Project` côté ASP.NET Core,
 * dont les deux champs techniques (Id, SortOrder) sont masqués par [JsonIgnore].
 * C'est aussi le corps attendu par les écritures du back-office (POST et PUT /api/projects).
 */
export interface Project {
  /** Identifiant stable et lisible ; sert de clé de suivi dans les boucles @for. */
  slug: string;
  title: string;
  description: string;
  /** Principales technologies, affichées en étiquettes. */
  techs: string[];
  githubUrl?: string | null;
  demoUrl?: string | null;
}
