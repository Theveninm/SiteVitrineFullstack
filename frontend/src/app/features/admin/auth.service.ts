import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/**
 * Session du back-office. La connexion elle-même n'est pas un appel HTTP : c'est une navigation vers
 * /api/auth/login, qui passe par Google (cf. la page de connexion).
 *
 * Le cookie de session est HttpOnly : Angular ne peut ni le lire ni le stocker, c'est le navigateur
 * qui l'envoie de lui-même. Pour savoir si la session est ouverte, on pose donc la question au serveur.
 *
 * Des Promise (firstValueFrom) plutôt que des Observable : ce sont des appels ponctuels, qu'on attend
 * avec await sans abonnement à gérer.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  async isLoggedIn(): Promise<boolean> {
    try {
      await firstValueFrom(this.http.get('/api/auth/me'));
      return true;
    } catch {
      return false;
    }
  }

  async logout(): Promise<void> {
    await firstValueFrom(this.http.post('/api/auth/logout', null));
  }
}
