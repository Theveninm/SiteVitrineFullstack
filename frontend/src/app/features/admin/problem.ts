import { HttpErrorResponse } from '@angular/common/http';
import { FieldTree } from '@angular/forms/signals';

/**
 * Erreurs par champ d'une réponse 400 « ValidationProblem » d'ASP.NET Core (RFC 7807), de la forme
 * { errors: { slug: ['Déjà utilisé…'] } }. Objet vide pour toute autre erreur.
 */
export function validationErrors(error: unknown): Record<string, string[]> {
  const isValidationProblem =
    error instanceof HttpErrorResponse && error.status === 400 && error.error?.errors;

  return isValidationProblem ? error.error.errors : {};
}

/** Un message affichable pour n'importe quelle erreur HTTP. */
export function errorMessage(error: unknown): string {
  const messages = Object.values(validationErrors(error)).flat();

  return messages.length > 0 ? messages.join(' ') : 'L’opération a échoué. Réessayer plus tard.';
}

/**
 * Rattache chaque erreur de validation du serveur (slug déjà pris, image expirée…) au champ du même
 * nom : elle s'affiche alors sous ce champ, exactement comme une erreur locale. Une erreur sans champ
 * correspondant reste au niveau du formulaire.
 *
 * @param fieldsByName Les champs du formulaire, indexés par le nom qu'emploie l'API — le même qu'en
 * JSON et en YAML (cf. la validation côté serveur).
 */
export function serverFieldErrors(
  error: unknown,
  fieldsByName: Record<string, FieldTree<unknown> | undefined>,
) {
  const errors = Object.entries(validationErrors(error)).map(([name, messages]) => ({
    kind: 'server',
    fieldTree: fieldsByName[name],
    message: messages.join(' '),
  }));

  return errors.length > 0 ? errors : { kind: 'server', message: errorMessage(error) };
}
