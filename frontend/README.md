# SiteVitrine

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 22.0.8.

## Development server

To start a local development server, run:

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4200/`. The application will automatically reload whenever you modify any of the source files.

### Dépendance à l'API

La page « Livres & outils » consomme `GET /api/resources`, servi par le backend (`../backend`).
Sans lui, la page affiche « Impossible de charger les ressources » — le reste du site fonctionne.

Démarrer la base et l'API avant `ng serve`, depuis la racine du dépôt :

```bash
docker compose up -d db api
```

Le code n'appelle que l'URL **relative** `/api`. En développement, `proxy.conf.json` la relaie vers
`http://localhost:5080` ; en production, c'est le `location /api` de `nginx.conf` (ce même
conteneur sert le site et relaie l'API). Dans les deux cas le navigateur reste sur une seule
origine, donc aucun CORS à gérer.

Voir [`../README.md`](../README.md) pour le déploiement.

### Back-office

`/admin` (aucun lien du site n'y mène) : connexion avec un compte Google autorisé, liste des
ressources réordonnable par glisser-déposer, formulaire d'édition avec dépôt d'image. Pour s'y
connecter en local, voir [`../backend/README.md`](../backend/README.md#se-connecter-au-back-office-en-local).

```
src/app/features/admin/
├── admin.routes.ts             Routes chargées à la demande sous /admin, garde de session
├── auth.service.ts             Session : vérification, déconnexion
├── auth.guard.ts               Redirige vers /admin/connexion sans session
├── unauthorized.interceptor.ts Session expirée (401) → page de connexion
├── resource-admin.service.ts   Écritures : création, modification, suppression, ordre, image
├── problem.ts                  Lecture des erreurs de validation de l'API
├── _admin-form.scss            Styles communs, inclus par @use
├── login/                      Page de connexion (bouton Google, messages de refus)
├── resource-list/              Liste + glisser-déposer (@angular/cdk/drag-drop)
├── resource-form/              Formulaire d'édition (Signal Forms)
├── image-drop/                 Zone de dépôt d'image, contrôle de formulaire maison
└── field-error/                Message d'erreur d'un champ
```

Les formulaires utilisent **Signal Forms** (`@angular/forms/signals`, stable depuis Angular 22). Les
erreurs de validation renvoyées par l'API sont rattachées au champ concerné et s'affichent comme les
erreurs locales.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Running unit tests

To execute unit tests with the [Vitest](https://vitest.dev/) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
