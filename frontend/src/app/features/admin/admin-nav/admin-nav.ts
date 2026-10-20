import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';

import { AuthService } from '../auth.service';

/**
 * L'en-tête commun aux écrans de liste du back-office : les onglets des sections éditables et le
 * bouton de déconnexion. Un seul endroit à toucher le jour où une troisième section apparaît.
 */
@Component({
  selector: 'app-admin-nav',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './admin-nav.html',
  styleUrl: './admin-nav.scss',
})
export class AdminNav {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected async logout(): Promise<void> {
    await this.auth.logout();
    await this.router.navigate(['/admin/connexion']);
  }
}
