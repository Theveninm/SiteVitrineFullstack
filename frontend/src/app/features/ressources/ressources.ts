import { Component, inject } from '@angular/core';
import { resourceImageSrc } from './resource.model';
import { ResourceService } from './resource.service';

@Component({
  selector: 'app-ressources',
  imports: [],
  templateUrl: './ressources.html',
  styleUrl: './ressources.scss',
})
export class Ressources {
  // La ressource HTTP vit dans le service (injecté en root) : les données sont donc conservées
  // si l'utilisateur quitte la page puis y revient, au lieu d'être rechargées à chaque montage.
  protected readonly resources = inject(ResourceService).resources;

  protected readonly imageSrc = resourceImageSrc;

  // Étoiles pleines vs vides pour l'affichage de la note (arrondie à l'entier le plus proche).
  protected starsFor(note: number): boolean[] {
    return Array.from({ length: 5 }, (_, i) => i < Math.round(note));
  }
}
