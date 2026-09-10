import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    // Rend HttpClient injectable dans toute l'application. C'est aussi ce qui alimente
    // httpResource() : sans ce provider, les appels HTTP échouent à l'injection.
    // Les intercepteurs (auth, gestion d'erreurs) se brancheront ici le moment venu, via
    // provideHttpClient(withInterceptors([...])).
    provideHttpClient(),
  ],
};
