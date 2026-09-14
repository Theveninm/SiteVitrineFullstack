import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';

/**
 * Garde de route fonctionnel : une simple fonction, là où l'on écrivait autrefois une classe. Elle
 * renvoie true pour laisser passer, ou un UrlTree pour rediriger.
 *
 * Ce garde ne protège rien côté serveur — l'API refuse d'elle-même toute écriture sans session. Il
 * évite seulement d'afficher un écran d'édition inutilisable.
 */
export const authGuard: CanActivateFn = async () => {
  // inject() ne fonctionne que dans le contexte d'injection, c'est-à-dire AVANT le premier await.
  const auth = inject(AuthService);
  const router = inject(Router);

  return (await auth.isLoggedIn()) || router.createUrlTree(['/admin/connexion']);
};
