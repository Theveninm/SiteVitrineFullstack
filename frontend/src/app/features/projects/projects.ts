import { Component, inject } from '@angular/core';

import { ProjectService } from './project.service';

@Component({
  selector: 'app-projects',
  imports: [],
  templateUrl: './projects.html',
  styleUrl: './projects.scss',
})
export class Projects {
  // La ressource HTTP vit dans le service (injecté en root) : les données sont donc conservées
  // si l'utilisateur quitte la page puis y revient, au lieu d'être rechargées à chaque montage.
  protected readonly projects = inject(ProjectService).projects;
}
