# Backend — API du site vitrine

API REST qui sert le contenu du site (aujourd'hui les ressources de la page « Livres & outils »).

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
| **Contenu** | les avis, les notes, les liens | [`../content/resources.yaml`](../content/resources.yaml) | quand tu le décides — jamais automatiquement |
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
    │   ├── ContentFile.cs         Lecture/écriture de resources.yaml
    │   └── ContentImporter.cs     Import idempotent (par slug), export, validation
    ├── Admin/
    │   ├── AdminCommands.cs       Les actions en ligne de commande
    │   └── AdminEndpoints.cs      Les mêmes en HTTP + isolation par port
    └── Resources/             Une feature = un dossier autonome
        ├── Resource.cs            Entité + configuration EF
        └── ResourcesEndpoints.cs  Routes publiques (lecture seule)
```

**Ajouter une feature** (authentification, messages, réservations…) :

1. créer `MaFeature/` avec son entité (+ `IEntityTypeConfiguration<T>`, découverte automatiquement)
   et ses endpoints groupés dans une méthode `MapMaFeatureEndpoints()` ;
2. ajouter le `DbSet<T>` dans `AppDbContext` ;
3. ajouter `app.MapMaFeatureEndpoints();` dans `Program.cs` ;
4. générer la migration (voir plus bas).

Rien d'autre à toucher : c'est le but de ce découpage par feature, calqué sur le dossier
`features/` du frontend Angular.

---

## Les deux surfaces

L'API écoute sur **deux ports**, et c'est ce qui remplace l'authentification du back-office :

| Port | Surface | Qui y accède |
| --- | --- | --- |
| `8080` | site public, **lecture seule** | le conteneur `web` (Nginx), par le réseau interne |
| `8081` | back-office, écritures | toi, par tunnel SSH — publié sur `127.0.0.1` seulement |

Le middleware `UseAdminPortIsolation` (dans `Admin/AdminEndpoints.cs`) renvoie **404** pour
`/admin`, `/openapi` et `/scalar` dès que la connexion n'arrive pas sur le port d'administration.
Il compare le port TCP réel de la connexion, pas l'en-tête `Host` : un en-tête se falsifie, un port
d'arrivée non.

> **Ce choix a une limite à connaître.** Il n'y a aucun compte, aucun mot de passe : la sécurité
> repose entièrement sur le fait que `8081` n'est joignable que depuis la machine hôte. Le jour où
> le back-office devra être accessible depuis internet, le filtre de port ne suffira plus et il
> faudra ajouter une authentification.

### Endpoints publics (port 8080)

| Méthode | Route | Réponse |
| ------- | ----- | ------- |
| `GET` | `/api/resources` | Toutes les ressources, dans l'ordre du site |
| `GET` | `/api/resources/{slug}` | Une ressource, ou `404` |
| `GET` | `/health` | `Healthy` si la base répond |

Il n'y a **pas de configuration CORS** : le navigateur n'appelle jamais cette API depuis une autre
origine (le Nginx du frontend relaie `/api` en production, `ng serve` en développement). Elle
serait à rétablir si l'API devait un jour vivre sur un domaine distinct.

### Back-office (port 8081)

| Méthode | Route | Rôle |
| ------- | ----- | ---- |
| `GET` | `/scalar` | **L'interface** : tous les endpoints avec formulaires |
| `GET` | `/admin/status` | Migrations appliquées/en attente, nb de ressources, état du fichier |
| `POST` | `/admin/content/import` | Charge `resources.yaml` en base (`?prune=true` pour élaguer) |
| `GET` | `/admin/content/export` | Renvoie la base au format du fichier |

Ouvrir le tunnel, puis `http://localhost:8081/scalar` :

```bash
ssh -L 8081:127.0.0.1:8081 utilisateur@serveur
```

---

## Modifier le contenu

[`content/resources.yaml`](../content/resources.yaml) est la source de vérité. L'ordre des entrées
définit l'ordre d'affichage ; la clé `slug` est l'identifiant stable sur lequel se fait
l'appariement (la renommer crée une nouvelle ressource au lieu de mettre à jour l'existante).

1. éditer le fichier — il est monté dans le conteneur, pas besoin de reconstruire l'image ;
2. déclencher l'import : bouton `POST /admin/content/import` dans Scalar, ou en ligne de commande

```bash
docker compose run --rm api import
```

L'import est **idempotent** et **non destructif** : il crée les nouvelles, met à jour les
existantes, et se contente de *signaler* celles qui sont en base mais plus dans le fichier. Pour
les supprimer réellement, il faut le demander : `--prune` en CLI, `?prune=true` en HTTP.

Le fichier est validé avant toute écriture en base (slug manquant ou en double, titre ou avis vide,
note hors de [0,5]) — une erreur produit un message qui nomme l'entrée fautive et rien n'est écrit.

### Le chemin de retour

Si tu édites directement en base (LinqPad, ou un futur back-office d'édition), l'export récupère le
tout au format du fichier :

```bash
docker compose run --rm api export > content/resources.yaml
```

Les diagnostics partent sur `stderr` et les données sur `stdout` : la redirection ne récupère que
du YAML. L'aller-retour est sans perte, y compris la mise en forme en blocs `|-` des longs avis.

---

## Développement local

Une base PostgreSQL est nécessaire. Depuis la **racine du dépôt** :

```bash
docker compose up -d db
```

Pour joindre ce port depuis l'IDE, ajouter `ports: ['5432:5432']` au service `db`. La chaîne de
connexion de développement est dans `appsettings.Development.json`, qui pointe aussi
`Content:Path` vers `content/resources.yaml` à la racine du dépôt.

Les trois commandes, hors conteneur :

```bash
dotnet run --project src/SiteVitrine.Api -- migrate
```

```bash
dotnet run --project src/SiteVitrine.Api -- import
```

```bash
dotnet run --project src/SiteVitrine.Api -- export
```

Sans argument, l'application démarre en serveur. `dotnet run` ajoutant ses propres messages sur la
sortie standard, utiliser `--no-launch-profile` si la sortie de `export` doit être redirigée.

### Générer une migration après modification d'une entité

```bash
dotnet ef migrations add NomDeLaMigration --project src/SiteVitrine.Api --output-dir Data/Migrations
```

Pas de `database update` à lancer : le conteneur `api-init` applique les migrations en attente au
déploiement. En local, `dotnet run -- migrate` fait la même chose.

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
  qu'empiler les attributs.
