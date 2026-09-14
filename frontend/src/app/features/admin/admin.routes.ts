import { Routes } from '@angular/router';

import { authGuard } from './auth.guard';

/** Routes du back-office, montées sous /admin par app.routes.ts. */
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
        path: '',
        loadComponent: () => import('./resource-list/resource-list').then((m) => m.ResourceList),
      },
      {
        path: 'nouvelle',
        loadComponent: () => import('./resource-form/resource-form').then((m) => m.ResourceForm),
      },
      {
        // « modifier/ » en préfixe : une ressource dont le slug serait « nouvelle » reste atteignable.
        path: 'modifier/:slug',
        loadComponent: () => import('./resource-form/resource-form').then((m) => m.ResourceForm),
      },
    ],
  },
];
