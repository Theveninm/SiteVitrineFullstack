import { Injectable } from '@angular/core';
import { httpResource } from '@angular/common/http';

import { Project } from './project.model';

@Injectable({ providedIn: 'root' })
export class ProjectService {
  /**
   * Même mécanique que ResourceService : httpResource() enveloppe l'appel HTTP dans des signaux
   * (`value()`, `isLoading()`, `error()`), et l'URL relative est relayée vers l'API par Nginx en
   * production, par proxy.conf.json en développement — donc aucun CORS à gérer.
   *
   * defaultValue : évite de traiter le cas `undefined` avant la première réponse.
   */
  readonly projects = httpResource<Project[]>(() => '/api/projects', { defaultValue: [] });
}
