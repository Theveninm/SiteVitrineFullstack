import { CdkDrag, CdkDragDrop, CdkDropList, moveItemInArray } from '@angular/cdk/drag-drop';
import { Component, inject, linkedSignal, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { Project } from '../../projects/project.model';
import { ProjectService } from '../../projects/project.service';
import { AdminNav } from '../admin-nav/admin-nav';
import { errorMessage } from '../problem';
import { ProjectAdminService } from '../project-admin.service';

@Component({
  selector: 'app-project-list',
  // CdkDropList / CdkDrag : directives standalone de @angular/cdk/drag-drop, importées comme
  // n'importe quel composant.
  imports: [RouterLink, CdkDropList, CdkDrag, AdminNav],
  templateUrl: './project-list.html',
  styleUrl: './project-list.scss',
})
export class ProjectList {
  private readonly admin = inject(ProjectAdminService);

  protected readonly projects = inject(ProjectService).projects;
  protected readonly error = signal<string | null>(null);

  /**
   * Copie locale de la liste, que le glisser-déposer réordonne. linkedSignal : un signal inscriptible
   * (on peut le set()) qui se recalcule de lui-même chaque fois que sa source change — ici à chaque
   * rechargement depuis l'API.
   *
   * hasValue() et non value() : value() lève une exception quand la ressource est en erreur.
   */
  protected readonly items = linkedSignal(() =>
    this.projects.hasValue() ? this.projects.value() : [],
  );

  constructor() {
    // La liste du service peut dater d'une visite antérieure de la page publique : on repart de la base.
    this.projects.reload();
  }

  protected async drop(event: CdkDragDrop<Project[]>): Promise<void> {
    if (event.previousIndex === event.currentIndex) return;

    // moveItemInArray modifie le tableau sur place : on travaille sur une copie, pour que le signal
    // reçoive un nouveau tableau (un signal compare les valeurs par référence).
    const reordered = [...this.items()];
    moveItemInArray(reordered, event.previousIndex, event.currentIndex);

    // Affichage immédiat, sans attendre le serveur ; en cas d'échec, run() recharge l'ordre réel.
    this.items.set(reordered);
    await this.run(() => this.admin.reorder(reordered.map((project) => project.slug)));
  }

  protected async remove(project: Project): Promise<void> {
    if (!confirm(`Supprimer « ${project.title} » ?`)) return;

    await this.run(() => this.admin.delete(project.slug));
  }

  /** Exécute une écriture ; si elle échoue, affiche pourquoi et réaffiche l'état réel de la base. */
  private async run(write: () => Promise<void>): Promise<void> {
    this.error.set(null);
    try {
      await write();
    } catch (error) {
      this.error.set(errorMessage(error));
      this.projects.reload();
    }
  }
}
