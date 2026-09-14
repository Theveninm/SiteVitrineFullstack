import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { Resource } from '../ressources/resource.model';
import { ResourceService } from '../ressources/resource.service';

/**
 * Les écritures du back-office. Les lectures, elles, passent par le httpResource de ResourceService,
 * partagé avec la page publique.
 */
@Injectable({ providedIn: 'root' })
export class ResourceAdminService {
  private readonly http = inject(HttpClient);

  // Rechargée après chaque écriture : la liste du back-office comme la page publique affichent alors
  // l'état réel de la base.
  private readonly resources = inject(ResourceService).resources;

  async create(resource: Resource): Promise<void> {
    await firstValueFrom(this.http.post('/api/resources', resource));
    this.resources.reload();
  }

  async update(slug: string, resource: Resource): Promise<void> {
    await firstValueFrom(this.http.put(`/api/resources/${encodeURIComponent(slug)}`, resource));
    this.resources.reload();
  }

  async delete(slug: string): Promise<void> {
    await firstValueFrom(this.http.delete(`/api/resources/${encodeURIComponent(slug)}`));
    this.resources.reload();
  }

  /** @param slugs Tous les slugs, dans le nouvel ordre d'affichage. */
  async reorder(slugs: string[]): Promise<void> {
    await firstValueFrom(this.http.post('/api/resources/reorder', slugs));
    this.resources.reload();
  }

  /** Envoie une image et renvoie son nom dans le dossier d'images de l'API. */
  async uploadImage(file: File): Promise<string> {
    // FormData = encodage multipart/form-data, le seul qui transporte un fichier. HttpClient pose
    // lui-même le Content-Type et sa « boundary » : surtout ne pas le fixer à la main.
    const body = new FormData();
    body.append('file', file);

    const { fileName } = await firstValueFrom(
      this.http.post<{ fileName: string }>('/api/images', body),
    );
    return fileName;
  }
}
