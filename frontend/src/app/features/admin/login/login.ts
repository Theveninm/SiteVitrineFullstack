import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

/**
 * Messages associés aux codes que l'API ajoute à l'adresse (?erreur=…) quand une connexion Google
 * n'aboutit pas (cf. Auth/AuthEndpoints.cs).
 */
const LOGIN_ERRORS: Record<string, string> = {
  refus: 'Ce compte Google n’est pas autorisé à administrer le site.',
  google: 'La connexion avec Google a été interrompue. Réessayer.',
  'non-configure': 'La connexion Google n’est pas configurée sur le serveur (voir le README).',
};

@Component({
  selector: 'app-login',
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class Login {
  // snapshot : l'adresse ne change pas tant que la page est affichée, inutile de s'abonner à ses
  // évolutions.
  private readonly errorCode = inject(ActivatedRoute).snapshot.queryParamMap.get('erreur');

  protected readonly error = this.errorCode
    ? (LOGIN_ERRORS[this.errorCode] ?? LOGIN_ERRORS['google'])
    : null;
}
