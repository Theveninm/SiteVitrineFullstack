import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { unauthorizedInterceptor } from './features/admin/unauthorized.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    // Rend HttpClient injectable dans toute l'application. C'est aussi ce qui alimente
    // httpResource() : sans ce provider, les appels HTTP échouent à l'injection.
    // withInterceptors : fonctions appliquées à chaque requête/réponse HTTP de l'application, dans
    // l'ordre du tableau. Ici, une session expirée renvoie vers la page de connexion du back-office.
    provideHttpClient(withInterceptors([unauthorizedInterceptor])),
  ],
};
