import { Component, input } from '@angular/core';
import { FieldTree } from '@angular/forms/signals';

/**
 * Affiche la première erreur d'un champ de Signal Forms, une fois que l'utilisateur l'a quitté ou a
 * tenté d'envoyer le formulaire (touched). Les erreurs du serveur renvoyées par l'action de
 * soumission s'affichent ici aussi : pour Signal Forms, ce sont des erreurs de champ comme les autres.
 */
@Component({
  selector: 'app-field-error',
  templateUrl: './field-error.html',
  styleUrl: './field-error.scss',
})
export class FieldError {
  readonly field = input.required<FieldTree<unknown>>();
}
