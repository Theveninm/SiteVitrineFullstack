import { Component, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { CONTACT_EMAIL, GITHUB_URL } from '../../contact-info';

@Component({
  selector: 'app-header',
  // RouterLink/RouterLinkActive sont des directives standalone : on les importe
  // directement dans le composant qui en a besoin, pas de NgModule à déclarer.
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './header.html',
  styleUrl: './header.scss',
})
export class Header {
  protected readonly githubUrl = GITHUB_URL;
  protected readonly email = CONTACT_EMAIL;

  // signal = état réactif Angular : le template se met à jour quand la valeur change.
  // Ici : menu mobile ouvert (true) ou fermé (false).
  protected readonly menuOpen = signal(false);

  protected toggleMenu(): void {
    // update() calcule la nouvelle valeur à partir de l'ancienne (idéal pour un toggle).
    this.menuOpen.update((open) => !open);
  }

  protected closeMenu(): void {
    this.menuOpen.set(false);
  }
}
