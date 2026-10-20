import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { Project } from '../projects/project.model';
import { ProjectService } from '../projects/project.service';

/**
 * Les écritures du back-office sur les projets. Les lectures, elles, passent par le httpResource de
 * ProjectService, partagé avec la page publique. Pendant des écritures de ResourceAdminService, sans
 * le dépôt d'image : un projet n'en a pas.
 */
@Injectable({ providedIn: 'root' })
export class ProjectAdminService {
  private readonly http = inject(HttpClient);

  // Rechargée après chaque écriture : la liste du back-office comme la page publique affichent alors
  // l'état réel de la base.
  private readonly projects = inject(ProjectService).projects;

  async create(project: Project): Promise<void> {
    await firstValueFrom(this.http.post('/api/projects', project));
    this.projects.reload();
  }

  async update(slug: string, project: Project): Promise<void> {
    await firstValueFrom(this.http.put(`/api/projects/${encodeURIComponent(slug)}`, project));
    this.projects.reload();
  }

  async delete(slug: string): Promise<void> {
    await firstValueFrom(this.http.delete(`/api/projects/${encodeURIComponent(slug)}`));
    this.projects.reload();
  }

  /** @param slugs Tous les slugs, dans le nouvel ordre d'affichage. */
  async reorder(slugs: string[]): Promise<void> {
    await firstValueFrom(this.http.post('/api/projects/reorder', slugs));
    this.projects.reload();
  }
}
