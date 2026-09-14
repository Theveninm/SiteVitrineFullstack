import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

/**
 * Intercepteur fonctionnel, branché dans app.config.ts. Si l'API répond 401 (session expirée pendant
 * qu'on édite, par exemple), renvoie vers la page de connexion au lieu de laisser un écran en échec.
 *
 * Les appels à /api/auth/ sont exclus : un 401 y est une réponse normale (le garde de route qui
 * vérifie la session), et rediriger créerait une boucle.
 */
export const unauthorizedInterceptor: HttpInterceptorFn = (request, next) => {
  const router = inject(Router);

  return next(request).pipe(
    catchError((error: unknown) => {
      const sessionLost =
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !request.url.startsWith('/api/auth/');

      if (sessionLost) {
        void router.navigate(['/admin/connexion']);
      }

      // L'erreur continue son chemin : l'appelant peut toujours la traiter.
      return throwError(() => error);
    }),
  );
};
