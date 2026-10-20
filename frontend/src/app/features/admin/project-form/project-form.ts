import { httpResource } from '@angular/common/http';
import { Component, inject, linkedSignal } from '@angular/core';
import { FormField, FormRoot, form, pattern, required } from '@angular/forms/signals';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { Project } from '../../projects/project.model';
import { FieldError } from '../field-error/field-error';
import { serverFieldErrors } from '../problem';
import { ProjectAdminService } from '../project-admin.service';

/**
 * Ce que manipule le formulaire. Proche de Project, avec deux écarts pensés pour la saisie : les
 * technologies sont un texte à raison d'une par ligne, et les champs facultatifs valent '' plutôt
 * que null (un <input> ne sait pas afficher null).
 */
interface ProjectFormModel {
  slug: string;
  title: string;
  description: string;
  techs: string;
  githubUrl: string;
  demoUrl: string;
}

@Component({
  selector: 'app-project-form',
  imports: [FormField, FormRoot, RouterLink, FieldError],
  templateUrl: './project-form.html',
  styleUrl: './project-form.scss',
})
export class ProjectForm {
  private readonly admin = inject(ProjectAdminService);
  private readonly router = inject(Router);

  /** Slug du projet modifié, lu dans l'URL (/admin/projets/modifier/:slug) ; null en création. */
  protected readonly editedSlug = inject(ActivatedRoute).snapshot.paramMap.get('slug');

  /** En modification, le projet actuel. Une URL undefined laisse le httpResource au repos. */
  protected readonly existing = httpResource<Project>(() =>
    this.editedSlug ? `/api/projects/${encodeURIComponent(this.editedSlug)}` : undefined,
  );

  /**
   * Le modèle du formulaire. linkedSignal : vide au départ, il se remplit de lui-même quand le
   * projet existant arrive, puis la saisie le modifie librement.
   */
  private readonly model = linkedSignal<ProjectFormModel>(() =>
    this.existing.hasValue() ? toFormModel(this.existing.value()) : emptyModel(),
  );

  // Les règles essentielles sont doublées ici pour un retour immédiat pendant la saisie. L'API reste
  // la référence : ce qu'elle refuse revient par l'action de soumission, ci-dessous.
  protected readonly projectForm = form(
    this.model,
    (path) => {
      required(path.title, { message: 'Obligatoire.' });
      required(path.slug, { message: 'Obligatoire.' });
      pattern(path.slug, /^[a-z0-9]+(-[a-z0-9]+)*$/, {
        message: 'Minuscules, chiffres et tirets uniquement (ex. cs-players-guide).',
      });
      required(path.description, { message: 'Obligatoire.' });
    },
    {
      submission: {
        action: async (fields) => {
          try {
            const project = toProject(this.model());
            if (this.editedSlug) {
              await this.admin.update(this.editedSlug, project);
            } else {
              await this.admin.create(project);
            }
            await this.router.navigate(['/admin/projets']);
            return undefined;
          } catch (error) {
            return serverFieldErrors(error, {
              slug: fields.slug,
              title: fields.title,
              description: fields.description,
              techs: fields.techs,
              githubUrl: fields.githubUrl,
              demoUrl: fields.demoUrl,
            });
          }
        },
      },
    },
  );
}

function emptyModel(): ProjectFormModel {
  return { slug: '', title: '', description: '', techs: '', githubUrl: '', demoUrl: '' };
}

function toFormModel(project: Project): ProjectFormModel {
  return {
    ...project,
    techs: project.techs.join('\n'),
    githubUrl: project.githubUrl ?? '',
    demoUrl: project.demoUrl ?? '',
  };
}

function toProject(model: ProjectFormModel): Project {
  return {
    slug: model.slug.trim(),
    title: model.title.trim(),
    description: model.description.trim(),
    techs: model.techs
      .split('\n')
      .map((line) => line.trim())
      .filter((line) => line.length > 0),
    githubUrl: model.githubUrl.trim() || null,
    demoUrl: model.demoUrl.trim() || null,
  };
}
