import { HttpErrorResponse } from '@angular/common/http';

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
