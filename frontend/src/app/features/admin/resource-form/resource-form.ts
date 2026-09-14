import { httpResource } from '@angular/common/http';
import { Component, inject, linkedSignal } from '@angular/core';
import {
  FieldTree,
  FormField,
  FormRoot,
  form,
  max,
  min,
  pattern,
  required,
} from '@angular/forms/signals';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { Resource } from '../../ressources/resource.model';
import { FieldError } from '../field-error/field-error';
import { ImageDrop } from '../image-drop/image-drop';
import { errorMessage, validationErrors } from '../problem';
import { ResourceAdminService } from '../resource-admin.service';

/**
 * Ce que manipule le formulaire. Proche de Resource, avec deux écarts pensés pour la saisie : les
 * points positifs/négatifs sont un texte à raison d'un point par ligne, et les champs facultatifs
 * valent '' plutôt que null (un <input> ne sait pas afficher null).
 */
interface ResourceFormModel {
  slug: string;
  title: string;
  imageFile: string;
  siteUrl: string;
  avis: string;
  positifs: string;
  negatifs: string;
  note: number;
}

@Component({
  selector: 'app-resource-form',
  imports: [FormField, FormRoot, RouterLink, FieldError, ImageDrop],
  templateUrl: './resource-form.html',
  styleUrl: './resource-form.scss',
})
export class ResourceForm {
  private readonly admin = inject(ResourceAdminService);
  private readonly router = inject(Router);

  /** Slug de la ressource modifiée, lu dans l'URL (/admin/modifier/:slug) ; null en création. */
  protected readonly editedSlug = inject(ActivatedRoute).snapshot.paramMap.get('slug');

  /** En modification, la ressource actuelle. Une URL undefined laisse le httpResource au repos. */
  protected readonly existing = httpResource<Resource>(() =>
    this.editedSlug ? `/api/resources/${encodeURIComponent(this.editedSlug)}` : undefined,
  );

  /**
   * Le modèle du formulaire. linkedSignal : vide au départ, il se remplit de lui-même quand la
   * ressource existante arrive, puis la saisie le modifie librement.
   */
  private readonly model = linkedSignal<ResourceFormModel>(() =>
    this.existing.hasValue() ? toFormModel(this.existing.value()) : emptyModel(),
  );

  // Les règles essentielles sont doublées ici pour un retour immédiat pendant la saisie. L'API reste
  // la référence : ce qu'elle refuse revient par l'action de soumission, ci-dessous.
  protected readonly resourceForm = form(
    this.model,
    (path) => {
      required(path.title, { message: 'Obligatoire.' });
      required(path.slug, { message: 'Obligatoire.' });
      pattern(path.slug, /^[a-z0-9]+(-[a-z0-9]+)*$/, {
        message: 'Minuscules, chiffres et tirets uniquement (ex. cs-players-guide).',
      });
      required(path.avis, { message: 'Obligatoire.' });
      min(path.note, 0, { message: 'Entre 0 et 5.' });
      max(path.note, 5, { message: 'Entre 0 et 5.' });
    },
    {
      submission: {
        action: async (fields) => {
          try {
            const resource = toResource(this.model());
            if (this.editedSlug) {
              await this.admin.update(this.editedSlug, resource);
            } else {
              await this.admin.create(resource);
            }
            await this.router.navigate(['/admin']);
            return undefined;
          } catch (error) {
            return serverErrors(error, fields);
          }
        },
      },
    },
  );
}

/**
 * Rattache chaque erreur de validation du serveur (slug déjà pris, image expirée…) au champ du même
 * nom : elle s'affiche alors sous ce champ, exactement comme une erreur locale. Une erreur sans champ
 * correspondant reste au niveau du formulaire.
 */
function serverErrors(error: unknown, fields: FieldTree<ResourceFormModel>) {
  const fieldsByName: Record<string, FieldTree<unknown> | undefined> = {
    slug: fields.slug,
    title: fields.title,
    imageFile: fields.imageFile,
    siteUrl: fields.siteUrl,
    avis: fields.avis,
    note: fields.note,
  };

  const errors = Object.entries(validationErrors(error)).map(([name, messages]) => ({
    kind: 'server',
    fieldTree: fieldsByName[name],
    message: messages.join(' '),
  }));

  return errors.length > 0 ? errors : { kind: 'server', message: errorMessage(error) };
}

function emptyModel(): ResourceFormModel {
  return { slug: '', title: '', imageFile: '', siteUrl: '', avis: '', positifs: '', negatifs: '', note: 3 };
}

function toFormModel(resource: Resource): ResourceFormModel {
  return {
    ...resource,
    imageFile: resource.imageFile ?? '',
    siteUrl: resource.siteUrl ?? '',
    positifs: resource.positifs.join('\n'),
    negatifs: resource.negatifs.join('\n'),
  };
}

function toResource(model: ResourceFormModel): Resource {
  const lines = (text: string) =>
    text
      .split('\n')
      .map((line) => line.trim())
      .filter((line) => line.length > 0);

  return {
    slug: model.slug.trim(),
    title: model.title.trim(),
    imageFile: model.imageFile || null,
    siteUrl: model.siteUrl.trim() || null,
    avis: model.avis.trim(),
    positifs: lines(model.positifs),
    negatifs: lines(model.negatifs),
    note: model.note,
  };
}
