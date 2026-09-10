import { Project } from './project.model';

// Données statiques pour cette v1. Le jour où ces projets viendront d'un backend,
// seul ce fichier disparaît — ProjectsComponent n'aura pas à changer (cf. plan du projet).
export const PROJECTS: Project[] = [
  {
    title: 'DocFileEditor',
    description:
      "Utilitaire de modding pour Might & Magic: Duel of Champions - Revival. Rétro-ingénierie d'un " +
      "format d'archive propriétaire (FAT-like), avec lecture et écriture des entrées compressées " +
      '(SharpZipLib) et un mécanisme de sauvegarde/restauration des fichiers originaux.' +
      "Pour l'instant le projet est en suspend, d'une part parceque c'etait aussi un projet " +
      '"découverte", et d\'autre part en raison de la partie "reverse engineering" sur laquelle ' +
      'je patauge un peu, par ailleurs, si je reprennais le projet, il y a de forte chances que je ' +
      'le refactorise fortement. Pour autant, ce projet devrait vous donner un aperçu de mon niveau en C#/WPF.',
    techs: ['C#', 'WPF', 'ILSpy', 'HxD'],
    githubUrl: 'https://github.com/Theveninm/DocFileEditor',
  },
  {
    title: 'Site personnel',
    description:
      "Création d'un site vitrine de présentation depuis un vps ubuntu, permettant de tester " +
      "mes compétences en développement angular et experimenter le déploiement d'une application " +
      '"depuis zéro (serveur ubuntu vide)".',
    techs: ['Angular', 'Typescript', 'Cursor', 'Docker', 'Nginx', 'Ubuntu'],
  },
];
