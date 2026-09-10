import { Routes } from '@angular/router';

// `loadComponent` charge le composant en lazy (chunk séparé, chargé à la navigation).
// Utilisé dès maintenant pour toutes les routes : ça ne coûte rien de plus qu'un import
// direct, et ça évite d'avoir à migrer les routes existantes le jour où des routes
// protégées (auth, chat) viendront s'ajouter en lazy loading.
export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/home/home').then((m) => m.Home),
  },
  {
    path: 'parcours',
    loadComponent: () => import('./features/parcours/parcours').then((m) => m.Parcours),
  },
  {
    path: 'projets',
    loadComponent: () => import('./features/projects/projects').then((m) => m.Projects),
  },
  {
    path: 'ressources',
    loadComponent: () => import('./features/ressources/ressources').then((m) => m.Ressources),
  },
  {
    path: 'contact',
    loadComponent: () => import('./features/contact/contact').then((m) => m.Contact),
  },
  {
    path: '**',
    redirectTo: '',
  },
];
