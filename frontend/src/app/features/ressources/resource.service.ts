import { Injectable } from '@angular/core';
import { httpResource } from '@angular/common/http';

import { Resource } from './resource.model';

@Injectable({ providedIn: 'root' })
export class ResourceService {
  /**
   * httpResource() (stable depuis Angular 22) enveloppe un appel HTTP dans un ensemble de
   * signaux : `value()`, `isLoading()`, `error()`. Plus besoin de gérer manuellement un
   * Observable, un subscribe et trois propriétés d'état — le template lit directement les
   * signaux et se met à jour tout seul.
   *
   * L'URL est passée sous forme de fonction : si elle dépendait d'un signal (un filtre, un
   * slug de route…), la requête serait automatiquement relancée à chaque changement.
   *
   * URL relative : en production Nginx Proxy Manager aiguille /api vers l'API .NET sur le même
   * domaine ; en développement c'est proxy.conf.json qui joue ce rôle. Dans les deux cas la
   * requête part vers la même origine que la page — donc aucun CORS à gérer côté navigateur.
   *
   * defaultValue : évite d'avoir à traiter le cas `undefined` avant la première réponse ; le
   * template affiche simplement une liste vide pendant le chargement.
   */
  readonly resources = httpResource<Resource[]>(() => '/api/resources', { defaultValue: [] });
}
