import { Component, inject, model, signal } from '@angular/core';
import { FormValueControl } from '@angular/forms/signals';

import { resourceImageSrc } from '../../ressources/resource.model';
import { errorMessage } from '../problem';
import { ResourceAdminService } from '../resource-admin.service';

/** Même limite que l'API (ImageStore.MaxBytes) : vérifiée ici pour éviter un envoi voué à l'échec. */
const MAX_BYTES = 5 * 1024 * 1024;

/**
 * Zone de dépôt d'image. L'image est envoyée à l'API dès qu'elle est déposée ; le champ ne contient
 * que le nom de fichier renvoyé par le serveur.
 *
 * Si le formulaire n'est finalement pas enregistré, rien à nettoyer ici : le serveur supprime
 * lui-même, au bout d'une heure, les images déposées que rien n'utilise.
 */
@Component({
  selector: 'app-image-drop',
  templateUrl: './image-drop.html',
  styleUrl: './image-drop.scss',
})
export class ImageDrop implements FormValueControl<string> {
  private readonly admin = inject(ResourceAdminService);

  /**
   * Contrat FormValueControl de Signal Forms : exposer la valeur sous forme de model(). Le composant
   * s'utilise alors comme un champ natif — <app-image-drop [formField]="form.imageFile" /> — et
   * [formField] se charge de la liaison dans les deux sens.
   */
  readonly value = model('');

  protected readonly imageSrc = resourceImageSrc;
  protected readonly dragOver = signal(false);
  protected readonly uploading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected onDragOver(event: DragEvent): void {
    // Sans preventDefault(), le navigateur refuse le dépôt… et ouvre le fichier à la place du site.
    event.preventDefault();
    this.dragOver.set(true);
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragOver.set(false);

    const file = event.dataTransfer?.files[0];
    if (file) void this.upload(file);
  }

  protected onFileChosen(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) void this.upload(file);

    // Vide la sélection, pour que choisir à nouveau le même fichier déclenche encore (change).
    input.value = '';
  }

  protected clear(): void {
    this.value.set('');
  }

  private async upload(file: File): Promise<void> {
    this.error.set(null);

    if (file.size > MAX_BYTES) {
      this.error.set('L’image doit faire au plus 5 Mo.');
      return;
    }

    this.uploading.set(true);
    try {
      this.value.set(await this.admin.uploadImage(file));
    } catch (error) {
      this.error.set(errorMessage(error));
    } finally {
      this.uploading.set(false);
    }
  }
}
