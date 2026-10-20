import { Routes } from '@angular/router';

import { authGuard } from './auth.guard';

/**
 * Routes du back-office, montées sous /admin par app.routes.ts.
 *
 * Une section par type de contenu éditable, avec les mêmes trois écrans : la liste, la création, la
 * modification. Les onglets d'AdminNav mènent aux deux listes.
 */
export const adminRoutes: Routes = [
  {
    path: 'connexion',
    loadComponent: () => import('./login/login').then((m) => m.Login),
  },
  {
    // Route sans composant, qui ne sert qu'à porter le garde : il protège d'un coup toutes les
    // routes enfants.
    path: '',
    canActivate: [authGuard],
    children: [
      {
        path: 'ressources',
        loadComponent: () => import('./resource-list/resource-list').then((m) => m.ResourceList),
      },
      {
        path: 'ressources/nouvelle',
        loadComponent: () => import('./resource-form/resource-form').then((m) => m.ResourceForm),
      },
      {
        // « modifier/ » en préfixe : une ressource dont le slug serait « nouvelle » reste atteignable.
        path: 'ressources/modifier/:slug',
        loadComponent: () => import('./resource-form/resource-form').then((m) => m.ResourceForm),
      },
      {
        path: 'projets',
        loadComponent: () => import('./project-list/project-list').then((m) => m.ProjectList),
      },
      {
        path: 'projets/nouveau',
        loadComponent: () => import('./project-form/project-form').then((m) => m.ProjectForm),
      },
      {
        // Même précaution que pour les ressources (cf. ci-dessus).
        path: 'projets/modifier/:slug',
        loadComponent: () => import('./project-form/project-form').then((m) => m.ProjectForm),
      },
      {
        // /admin tout court : pas d'écran d'accueil à afficher, on ouvre la première section.
        path: '',
        pathMatch: 'full',
        redirectTo: 'ressources',
      },
    ],
  },
];
