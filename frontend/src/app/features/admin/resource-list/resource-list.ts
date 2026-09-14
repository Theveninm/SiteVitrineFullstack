import { CdkDrag, CdkDragDrop, CdkDropList, moveItemInArray } from '@angular/cdk/drag-drop';
import { Component, inject, linkedSignal, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { Resource, resourceImageSrc } from '../../ressources/resource.model';
import { ResourceService } from '../../ressources/resource.service';
import { AuthService } from '../auth.service';
import { errorMessage } from '../problem';
import { ResourceAdminService } from '../resource-admin.service';

@Component({
  selector: 'app-resource-list',
  // CdkDropList / CdkDrag : directives standalone de @angular/cdk/drag-drop, importées comme
  // n'importe quel composant.
  imports: [RouterLink, CdkDropList, CdkDrag],
  templateUrl: './resource-list.html',
  styleUrl: './resource-list.scss',
})
export class ResourceList {
  private readonly admin = inject(ResourceAdminService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly resources = inject(ResourceService).resources;
  protected readonly imageSrc = resourceImageSrc;
  protected readonly error = signal<string | null>(null);

  /**
   * Copie locale de la liste, que le glisser-déposer réordonne. linkedSignal : un signal inscriptible
   * (on peut le set()) qui se recalcule de lui-même chaque fois que sa source change — ici à chaque
   * rechargement depuis l'API.
   *
   * hasValue() et non value() : value() lève une exception quand la ressource est en erreur.
   */
  protected readonly items = linkedSignal(() =>
    this.resources.hasValue() ? this.resources.value() : [],
  );

  constructor() {
    // La liste du service peut dater d'une visite antérieure de la page publique : on repart de la base.
    this.resources.reload();
  }

  protected async drop(event: CdkDragDrop<Resource[]>): Promise<void> {
    if (event.previousIndex === event.currentIndex) return;

    // moveItemInArray modifie le tableau sur place : on travaille sur une copie, pour que le signal
    // reçoive un nouveau tableau (un signal compare les valeurs par référence).
    const reordered = [...this.items()];
    moveItemInArray(reordered, event.previousIndex, event.currentIndex);

    // Affichage immédiat, sans attendre le serveur ; en cas d'échec, run() recharge l'ordre réel.
    this.items.set(reordered);
    await this.run(() => this.admin.reorder(reordered.map((resource) => resource.slug)));
  }

  protected async remove(resource: Resource): Promise<void> {
    if (!confirm(`Supprimer « ${resource.title} » ?`)) return;

    await this.run(() => this.admin.delete(resource.slug));
  }

  protected async logout(): Promise<void> {
    await this.auth.logout();
    await this.router.navigate(['/admin/connexion']);
  }

  /** Exécute une écriture ; si elle échoue, affiche pourquoi et réaffiche l'état réel de la base. */
  private async run(write: () => Promise<void>): Promise<void> {
    this.error.set(null);
    try {
      await write();
    } catch (error) {
      this.error.set(errorMessage(error));
      this.resources.reload();
    }
  }
}
