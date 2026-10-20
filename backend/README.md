# Backend — API du site vitrine

API REST qui sert le contenu du site (aujourd'hui les ressources de la page « Livres & outils »), et
les écritures du back-office qui permet de l'éditer.

- **ASP.NET Core 10**, Minimal APIs
- **EF Core 10** + **PostgreSQL 18** (provider Npgsql)

> Le **déploiement** est décrit à la racine du dépôt : [`../README.md`](../README.md).
> Le backend n'a pas de stack propre, il fait partie de la stack unique du site.

---

## Les trois choses à ne pas confondre

C'est le principe d'organisation du backend, et ce qui explique le découpage des dossiers :

| | Quoi | Où ça vit | Quand ça bouge |
| --- | --- | --- | --- |
| **Schéma** | tables, index, contraintes | `Data/Migrations/` (généré) | conteneur `api-init`, à chaque déploiement |
| **Contenu** | les avis, les notes, les liens, les images | la base, éditée dans le back-office ; images dans `Storage:Path` ; copie dans [`../content/`](../content/) | à chaque enregistrement dans le back-office |
| **Code** | endpoints, import, validation | le reste | à la compilation |

Le contenu n'est **pas** dans le code : corriger une faute dans un avis n'exige ni recompilation,
ni reconstruction d'image, ni redéploiement. Et migrer le schéma ne charge aucune donnée — les
deux opérations sont indépendantes et se déclenchent séparément.

## Organisation du code

```
backend/
├── Dockerfile                 Image de l'API (build multi-stage, deux ports)
└── src/SiteVitrine.Api/
    ├── Program.cs             Deux modes : serveur HTTP, ou commande one-shot
    ├── Data/
    │   ├── AppDbContext.cs        Modèle EF, découverte auto des configurations
    │   ├── DatabaseMigrator.cs    Migrations — schéma uniquement
    │   └── Migrations/            Généré par `dotnet ef` — ne pas éditer à la main
    ├── Content/
    │   ├── ContentFile.cs         Lecture/écriture de content.yaml
    │   ├── ContentImporter.cs     Import idempotent (par slug), export, validation
    │   ├── ContentOrdering.cs     Rang d'affichage et réordonnancement, communs aux features
    │   ├── ContentRules.cs        Règles de validation communes (format de slug, URL web)
    │   └── IContentFor.cs         Contrat d'un XxxContent vis-à-vis de son entité (import)
    ├── Admin/                 Administration technique (port 8081)
    │   ├── AdminCommands.cs       Les actions en ligne de commande
    │   └── AdminEndpoints.cs      Les mêmes en HTTP + isolation par port
    ├── Auth/                  Connexion au back-office
    │   └── AuthEndpoints.cs       Connexion Google, comptes autorisés, cookie de session
    ├── Images/
    │   ├── ImageStore.cs          Dossier d'images : dépôt, lecture, ménage automatique
    │   └── ImagesEndpoints.cs     Service des images, dépôt (authentifié)
    ├── Resources/             Une feature = un dossier autonome
    │   ├── Resource.cs            Entité + configuration EF
    │   ├── ResourceContent.cs     Champs éditables : validation et recopie (YAML et back-office)
    │   └── ResourcesEndpoints.cs  Lecture publique, écritures authentifiées
    └── Projects/              Même découpage, sans image
        ├── Project.cs             Entité + configuration EF
        ├── ProjectContent.cs      Champs éditables : validation et recopie
        └── ProjectsEndpoints.cs   Lecture publique, écritures authentifiées
```

**Ajouter une feature** (authentification, messages, réservations…) :

1. créer `MaFeature/` avec son entité (+ `IEntityTypeConfiguration<T>`, découverte automatiquement)
   et ses endpoints groupés dans une méthode `MapMaFeatureEndpoints()` — écritures derrière
   `RequireAuthorization()` ;
2. ajouter le `DbSet<T>` dans `AppDbContext` ;
3. ajouter `app.MapMaFeatureEndpoints();` dans `Program.cs` ;
4. générer la migration (voir plus bas).

Rien d'autre à toucher : c'est le but de ce découpage par feature, calqué sur le dossier
`features/` du frontend Angular.

---

## Les deux surfaces

L'API écoute sur **deux ports** :

| Port | Surface | Qui y accède | Protection des écritures |
| --- | --- | --- | --- |
| `8080` | site public (lecture) **et** back-office (écritures) | le conteneur `web` (Nginx), par le réseau interne | session du back-office |
| `8081` | administration technique : Scalar, état, import, export | toi, par tunnel SSH — publié sur `127.0.0.1` seulement | isolation réseau |

Le middleware `UseAdminPortIsolation` (dans `Admin/AdminEndpoints.cs`) renvoie **404** pour
`/admin`, `/openapi` et `/scalar` dès que la connexion n'arrive pas sur le port d'administration.
Il compare le port TCP réel de la connexion, pas l'en-tête `Host` : un en-tête se falsifie, un port
d'arrivée non.

### Authentification du back-office

Connexion avec un **compte Google** (OAuth 2.0, paquet `Microsoft.AspNetCore.Authentication.Google`) :
le site ne stocke aucun mot de passe et ne voit jamais celui du compte.

1. le bouton de la page de connexion mène à `GET /api/auth/login`, qui redirige vers Google ;
2. l'utilisateur s'y connecte ;
3. Google le renvoie sur `/api/auth/signin-google` avec un code, que l'API échange contre l'identité
   du compte, de serveur à serveur, grâce au secret client ;
4. si l'adresse est **vérifiée** par Google et figure dans `Auth:AllowedEmails`, l'API émet son propre
   cookie de session et renvoie sur `/admin`. Sinon, retour sur `/admin/connexion?erreur=refus`, sans
   session.

Google prouve l'identité ; la décision d'autoriser reste dans l'API.

- **Session par cookie** `HttpOnly` (hors de portée du JavaScript), `Secure`, `SameSite=Lax`, limité à
  `/api`, valable 8 h glissantes. `Lax` plutôt que `Strict` pour ne pas gêner le retour depuis Google,
  qui est une navigation venue d'un autre site. Les écritures restent protégées du CSRF : un cookie
  `Lax` n'accompagne pas un `POST`/`PUT`/`DELETE` initié par un autre site — d'où le
  `DisableAntiforgery()` du dépôt d'image.
- **Écritures** : `RequireAuthorization()` sur les routes concernées ; sans session, `401`.
- **Clés de chiffrement** du cookie (Data Protection) rangées dans `Storage:Path/keys` : un
  redéploiement ne déconnecte pas. Au démarrage, ASP.NET avertit qu'elles sont stockées non
  chiffrées — attendu, le volume n'est lisible que depuis l'hôte.
- **Adresse de retour** : construite à partir du schéma et de l'hôte de la requête, tels que les
  rétablit `UseForwardedHeaders` (cf. `Program.cs`) : `https` grâce au `X-Forwarded-Proto` de NPM
  relayé par Nginx, et l'hôte avec son port grâce au `Host $http_host` de `frontend/nginx.conf`. Si
  l'un des deux se perd, Google refuse l'adresse (`redirect_uri_mismatch`).

Configuration : `Auth:Google:ClientId`, `Auth:Google:ClientSecret` et `Auth:AllowedEmails` (adresses
séparées par des virgules). Tant que l'une est vide, le handler Google n'est pas enregistré : le site
public fonctionne normalement, et la connexion renvoie sur `/admin/connexion?erreur=non-configure`.

> L'administration technique, elle, n'a **aucune** authentification : sa sécurité repose entièrement
> sur le fait que `8081` n'est joignable que depuis la machine hôte. Elle agit sur la base entière (un
> import écrase les modifications du back-office) : la garder hors d'internet.

### Endpoints du port 8080

| Méthode | Route | Session | Réponse |
| ------- | ----- | :-----: | ------- |
| `GET` | `/api/resources` | | Toutes les ressources, dans l'ordre du site |
| `GET` | `/api/resources/{slug}` | | Une ressource, ou `404` |
| `POST` | `/api/resources` | ✔ | Crée une ressource en fin de liste |
| `PUT` | `/api/resources/{slug}` | ✔ | Modifie une ressource (slug compris) |
| `DELETE` | `/api/resources/{slug}` | ✔ | Supprime une ressource |
| `POST` | `/api/resources/reorder` | ✔ | Nouvel ordre, à partir de la liste complète des slugs |
| `GET` | `/api/images/{fichier}` | | Une image du contenu |
| `POST` | `/api/images` | ✔ | Dépôt d'image (multipart, champ `file`, 5 Mo max) → `{ fileName }` |
| `GET` | `/api/auth/login` | | Redirection vers Google (navigation pleine page) |
| `GET` | `/api/auth/signin-google` | | Retour de Google, traité par le handler : cookie de session puis `/admin` |
| `POST` | `/api/auth/logout` | | Ferme la session |
| `GET` | `/api/auth/me` | ✔ | `204` si la session est ouverte |
| `GET` | `/health` | | `Healthy` si la base répond |

Une écriture invalide répond `400` au format `ValidationProblem`, avec un message par champ
(`{ "errors": { "slug": ["Déjà utilisé…"] } }`) que le formulaire Angular affiche sous le champ
concerné.

Il n'y a **pas de configuration CORS** : le navigateur n'appelle jamais cette API depuis une autre
origine (le Nginx du frontend relaie `/api` en production, `ng serve` en développement). Elle
serait à rétablir si l'API devait un jour vivre sur un domaine distinct.

### Administration technique (port 8081)

| Méthode | Route | Rôle |
| ------- | ----- | ---- |
| `GET` | `/scalar` | **L'interface** : tous les endpoints avec formulaires |
| `GET` | `/admin/status` | Migrations, nombre de ressources, de projets et d'images, fichier de contenu, connexion Google configurée |
| `POST` | `/admin/content/import` | Charge `content.yaml` et ses images en base (`?prune=true` pour élaguer) |
| `GET` | `/admin/content/export` | Renvoie la base au format du fichier |

Ouvrir le tunnel, puis `http://localhost:8081/scalar` :

```bash
ssh -L 8081:127.0.0.1:8081 utilisateur@serveur
```

---

## Modifier le contenu

**La base fait foi**, et le back-office (page `/admin` du site) est le chemin normal : formulaire,
glisser-déposer des images et de l'ordre d'affichage. Rien à recompiler ni à redéployer.

### Cycle de vie des images

Toutes les images du contenu vivent dans un seul dossier, `Storage:Path/images` (le volume `storage`
en conteneur), servi sur `/api/images/{fichier}`. En base, une ressource ne stocke que le **nom** du
fichier (`imageFile`).

1. **Dépôt** — l'image glissée dans le formulaire part aussitôt vers `POST /api/images`. Elle est
   renommée d'après l'empreinte SHA-256 de son contenu : aucun nom venu du client sur le disque, pas
   de doublon, et un nom qui ne change jamais de contenu (d'où un cache navigateur d'un an).
2. **Enregistrement** — le formulaire envoie ce nom avec le reste ; l'API vérifie que le fichier existe.
3. **Ménage** — après chaque modification, suppression ou import, `ImageStore.DeleteUnusedAsync`
   supprime les images que plus aucune ressource n'utilise :
   - l'image **libérée** par l'écriture (ancienne image d'une ressource modifiée ou supprimée) part
     immédiatement ;
   - une image déposée mais **jamais enregistrée** (formulaire abandonné) est gardée une heure — le
     formulaire est peut-être encore ouvert — puis supprimée au ménage suivant.

Les images sont servies avec `Content-Security-Policy: sandbox` et `nosniff` : un SVG ouvert
directement ne peut pas exécuter de script sur l'origine du site.

### Le fichier de contenu : charger une base neuve, garder une copie

[`content/content.yaml`](../content/content.yaml) et [`content/images/`](../content/images/)
sont une **copie** du contenu : ils servent à charger une base neuve et à versionner le contenu dans
git. Le fichier a une clé de premier niveau par type de contenu — `resources:` et `projects:` — et
dans chacune, l'ordre des entrées définit l'ordre d'affichage ; la clé `slug` est l'identifiant sur
lequel se fait l'appariement.

```bash
docker compose run --rm api import
```

L'import est **idempotent** et **non destructif** : il crée les nouvelles entrées, met à jour les
existantes, copie les images absentes du dossier de l'API, et se contente de *signaler* celles qui
sont en base mais plus dans le fichier. Il traite les deux sections en un seul enregistrement, et
son rapport compte séparément les ressources et les projets. Pour supprimer réellement les
orphelines : `--prune` en CLI, `?prune=true` en HTTP.

> ⚠️ « Met à jour les existantes » veut dire : **écrase** ce qui a été modifié dans le back-office
> depuis. Une fois le back-office en service, exporter avant d'importer quoi que ce soit.

Le fichier est validé avant toute écriture en base (slug manquant, mal formé ou en double, titre,
avis ou description vide, note hors de [0,5], image introuvable) — une erreur produit un message qui
nomme la section et l'entrée fautives, et rien n'est écrit. Ces règles sont les mêmes que celles du
back-office : elles vivent dans le `XxxContent.cs` de chaque feature, les plus générales (format de
slug, adresse web) dans `Content/ContentRules.cs`.

### Le chemin de retour

L'export écrit la base au format du fichier :

```bash
docker compose run --rm api export > content/content.yaml
```

Les diagnostics partent sur `stderr` et les données sur `stdout` : la redirection ne récupère que
du YAML. L'aller-retour est sans perte, y compris la mise en forme en blocs `|-` des longs avis.
Les images déposées depuis le back-office ne font pas partie de l'export : les recopier depuis le
volume (cf. [sauvegarde](../README.md#sauvegarde-et-restauration)) dans `content/images/`.

---

## Développement local

Une base PostgreSQL est nécessaire. Depuis la **racine du dépôt** :

```bash
docker compose up -d db
```

Pour joindre ce port depuis l'IDE, ajouter `ports: ['5432:5432']` au service `db`. La chaîne de
connexion de développement est dans `appsettings.Development.json`, qui pointe aussi
`Content:Path` vers `content/content.yaml` à la racine du dépôt.

Les commandes, hors conteneur :

```bash
dotnet run --project src/SiteVitrine.Api -- migrate
```

```bash
dotnet run --project src/SiteVitrine.Api -- import
```

```bash
dotnet run --project src/SiteVitrine.Api -- export
```

Sans argument, l'application démarre en serveur. Images et clés de session sont écrites dans
`src/SiteVitrine.Api/storage/`, ignoré par git.

### Se connecter au back-office en local

Il faut un client OAuth Google (cf. [README racine](../README.md#identifiants-google-back-office))
dont les adresses de retour incluent `http://localhost:4200/api/auth/signin-google`. Ses identifiants
se rangent dans les **user secrets** de .NET : stockés dans le profil Windows, hors du dépôt, et lus
seulement en environnement Development.

```bash
dotnet user-secrets set "Auth:Google:ClientId" "<client id>" --project src/SiteVitrine.Api
```

```bash
dotnet user-secrets set "Auth:Google:ClientSecret" "<client secret>" --project src/SiteVitrine.Api
```

```bash
dotnet user-secrets set "Auth:AllowedEmails" "<ton adresse gmail>" --project src/SiteVitrine.Api
```

Puis lancer l'API, `ng serve` dans `frontend/`, et ouvrir `http://localhost:4200/admin`. `dotnet run` ajoutant ses propres messages sur la
sortie standard, utiliser `--no-launch-profile` si la sortie de `export` doit être redirigée.

### Générer une migration après modification d'une entité

```bash
dotnet ef migrations add NomDeLaMigration --project src/SiteVitrine.Api --output-dir Data/Migrations
```

Pas de `database update` à lancer : le conteneur `api-init` applique les migrations en attente au
déploiement. En local, `dotnet run -- migrate` fait la même chose.

> ⚠️ Recompiler avant de lancer `dotnet run -- migrate` qui suit. `migrations add` écrit la migration
> et le snapshot du modèle sur le disque, mais un `--no-build` tournerait encore sur l'assembly
> d'avant — EF refuse alors de migrer (`PendingModelChangesWarning`).

---

## Tests

```bash
dotnet test
```

**Docker doit tourner** : les tests qui touchent à la base démarrent un vrai PostgreSQL jetable
(Testcontainers), dans la même image qu'en production. C'est voulu — les garanties qui comptent ici
sont portées par la base : unicité du slug, contrainte de domaine sur la note, et surtout les
tableaux natifs `text[]` sur lesquels Npgsql mappe les `List<string>`. Un fournisseur en mémoire
n'en rejoue aucune, et laisserait donc passer précisément ce que ces tests cherchent.

Le conteneur démarre une fois pour toute la collection `postgres` et les tables sont vidées entre
deux tests ; la suite complète tient en quelques secondes.

| Fichier | Ce qu'il couvre |
| ------- | --------------- |
| `Content/ContentRulesTests` | Format de slug et adresse web — sans base ni fichier |
| `Resources/ResourceContentTests`, `Projects/ProjectContentTests` | Validation par champ et recopie vers l'entité : les mêmes règles servent au YAML et au back-office |
| `Content/ContentFileTests` | Aller-retour par le fichier : blocs littéraux, clés nulles omises, clé en double refusée, `C#` non quoté |
| `Images/ImageStoreTests` | Nommage par empreinte, dédoublonnage, noms de fichiers refusés (`../`, sous-dossier, format) |
| `Images/ImageCleanupTests` | Le ménage : image libérée supprimée tout de suite, image déposée gardée une heure, image partagée conservée |
| `Content/ContentImporterTests` | Idempotence, ordre d'affichage, orphelins signalés puis élagués, fichier invalide refusé **avant** toute écriture |

Les endpoints HTTP ne sont pas couverts : leur logique se réduit à valider, appeler ces briques-là,
et choisir un code de retour. Le jour où elle s'épaissit, c'est `WebApplicationFactory` qu'il faudra
sortir.

---

## Points d'attention

- `<TreatWarningsAsErrors>` est actif : un paquet NuGet vulnérable ou une API obsolète fait
  échouer la compilation plutôt que de passer inaperçu dans les logs.
- `Microsoft.OpenApi` est épinglé explicitement dans le `.csproj` pour écraser la version
  transitive vulnérable tirée par `Microsoft.AspNetCore.OpenApi` (GHSA-v5pm-xwqc-g5wc). À
  supprimer quand un correctif d'ASP.NET Core embarquera une version saine.
- **Le seul DTO du projet a été supprimé.** `Resource` est sérialisée telle quelle, ses deux champs
  techniques masqués par `[JsonIgnore]`. Le jour où une colonne devra rester privée (notes de
  travail, statut de publication…), c'est le signal qu'il faut réintroduire un DTO plutôt
  qu'empiler les attributs. En écriture, c'est `ResourceContent` — la forme d'une entrée du YAML — qui
  sert de corps de requête : validation et recopie vers l'entité n'existent qu'en un exemplaire.
