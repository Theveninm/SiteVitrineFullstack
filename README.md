# Site vitrine

Site personnel : présentation du parcours, des projets, et une sélection de livres et outils avec
un avis sur chacun.

| Partie | Techno | Détail |
| ------ | ------ | ------ |
| [`frontend/`](frontend/) | Angular 22 (SPA), servie par Nginx | [README](frontend/README.md) |
| [`backend/`](backend/) | ASP.NET Core 10 + EF Core 10 | [README](backend/README.md) |
| Base | PostgreSQL 18 | — |

Le tout se déploie comme **une seule stack Docker**, pilotée depuis Portainer et exposée par
Nginx Proxy Manager.

## Architecture

```
Internet ──> Nginx Proxy Manager ──> web (Nginx)  ──/api──> api :8080 ──> db (PostgreSQL)
                 (TLS, domaine)      │                        ▲               ▲
                                     └── Angular, /admin      │               │
                                                              │               │
   toi ──[ tunnel SSH ]──> 127.0.0.1:8081 ────────────────────┘               │
                     administration technique                                 │
                                                          api-init ───────────┘
                                                    (migre le schéma, puis s'arrête)
```

Le frontend n'appelle que des **URL relatives** (`/api/...`) : c'est le Nginx qui sert le site qui
relaie ce préfixe vers l'API, sur le réseau interne. Conséquences :

- le navigateur ne voit qu'une seule origine, donc **aucun CORS** à configurer ;
- NPM n'a qu'un hôte à connaître, le conteneur `web` ;
- `db` n'est exposé nulle part, et de `api` seul le port d'administration est publié — sur la
  boucle locale de l'hôte, donc injoignable depuis le réseau, y compris par NPM.

Chaque responsabilité a son mécanisme — c'est le principe d'organisation de la stack :

| Responsabilité | Mécanisme | Quand |
| --- | --- | --- |
| **Schéma** (migrations EF) | conteneur `api-init`, s'exécute et s'arrête | à chaque déploiement, automatique |
| **Contenu** (avis, images) | la base, éditée dans le **back-office** `https://<domaine>/admin` (compte Google) ; images dans le volume `storage` | à chaque enregistrement |
| **Copie du contenu** | [`content/`](content/) : `resources.yaml` + `images/`, monté en lecture seule | import / export, à la demande |
| **Administration technique** | port `8081`, hors du chemin public : état, import, export | tunnel SSH |

La base fait foi. Le contenu éditorial ne vit **pas** dans le code : corriger une faute dans un avis
n'exige ni recompilation ni redéploiement. Voir [`backend/README.md`](backend/README.md#modifier-le-contenu).

---

## Développement

Prérequis : Node 22+, .NET 10 SDK, Docker.

**Tout dans Docker** — le plus proche de la production :

```bash
docker compose up -d --build
```

Ajouter `ports: ['8081:80']` au service `web` pour y accéder sur `http://localhost:8081`.

**Frontend en rechargement à chaud, backend dans Docker** — le mode de travail courant :

```bash
docker compose up -d db api
```

puis, dans `frontend/` :

```bash
npm start
```

Le site est sur `http://localhost:4200`. `frontend/proxy.conf.json` relaie `/api` vers
`http://localhost:5080` — même principe qu'en production, avec la même URL relative dans le code.
Pour que l'API soit joignable ainsi, publier son port en ajoutant `ports: ['5080:8080']` au
service `api`.

Sur une base neuve, penser à charger le contenu une fois — la migration ne crée que le schéma :

```bash
docker compose run --rm api import
```

Le back-office est sur `http://localhost:4200/admin`, ou `http://localhost:8082/admin` avec la stack
Docker complète. Il se connecte avec Google : il faut d'abord créer un client OAuth (voir
[Identifiants Google](#identifiants-google-back-office)), puis renseigner `GOOGLE_CLIENT_ID`,
`GOOGLE_CLIENT_SECRET` et `ADMIN_EMAILS` dans `.env` (stack Docker) ou dans les user secrets (API
lancée depuis l'IDE, cf. [`backend/README.md`](backend/README.md#se-connecter-au-back-office-en-local)).

Voir [`backend/README.md`](backend/README.md) pour lancer l'API depuis l'IDE, générer une
migration, ou modifier le contenu des ressources.

---

## Déploiement (Portainer)

Tout se fait depuis trois interfaces graphiques, sans ligne de commande :

| Outil | Rôle |
| ----- | ---- |
| **Portainer** | stacks, conteneurs, logs |
| **Nginx Proxy Manager** | domaine et certificat TLS |
| **VS Code + Remote-SSH** | fichiers du serveur, et accès au back-office (redirection de port) |

### 1. Repérer le réseau de Nginx Proxy Manager et un port libre

**Portainer → Networks.** Noter le nom du réseau auquel appartient le conteneur NPM (par exemple
`nginx-proxy-manager_default`). Il est référencé comme réseau `external` dans `docker-compose.yml` :
le nom doit être exact, sinon la stack refuse de démarrer.

**Portainer → Containers**, colonne _Published Ports_ : vérifier que `127.0.0.1:8081` n'est pas
déjà pris par une autre stack. Sinon, choisir un autre port pour `ADMIN_PORT` (étape 4).

### 2. Déposer la copie du contenu sur le serveur

Portainer clone le dépôt **à l'intérieur de son propre conteneur** : le dossier `content/` du dépôt
n'existe pas pour le Docker de l'hôte, et un montage `./content` y serait vide. La copie du contenu
vit donc dans un dossier ordinaire du serveur, désigné par `CONTENT_DIR`.

1. VS Code → **Remote Explorer** → **SSH** → se connecter au serveur ;
2. **File → Open Folder** → `/root` ;
3. dans l'explorateur de fichiers de VS Code, créer `site-vitrine/content/` ;
4. y glisser-déposer `content/resources.yaml` **et** le dossier `content/images/` depuis
   l'explorateur Windows.

Ces fichiers ne servent qu'à charger la base neuve (étape 6). Ensuite, le contenu s'édite dans le
back-office.

### 3. Créer la stack

**Portainer → Stacks → Add stack**, nom `site-vitrine`, méthode **Repository** :

| Champ | Valeur |
| ----- | ------ |
| Repository URL | URL HTTPS du dépôt GitHub |
| Repository reference | `refs/heads/master` |
| Compose path | `docker-compose.yml` |
| Authentication | désactivée (dépôt public) |

Portainer clone le dépôt et construit les deux images lui-même.

### 4. Variables d'environnement

Dans la section **Environment variables** de la stack, cliquer sur **Advanced mode**, coller
`.env.example`, puis adapter :

| Variable | Valeur |
| -------- | ------ |
| `POSTGRES_DB` | `sitevitrine` |
| `POSTGRES_USER` | `sitevitrine` |
| `POSTGRES_PASSWORD` | mot de passe long et aléatoire (généré par un gestionnaire de mots de passe) |
| `PROXY_NETWORK` | nom relevé à l'étape 1 |
| `ADMIN_PORT` | `8081`, ou le port libre choisi à l'étape 1 |
| `CONTENT_DIR` | `/root/site-vitrine/content` (chemin **absolu**, cf. étape 2) |
| `GOOGLE_CLIENT_ID` | identifiant du client OAuth Google (ci-dessous) |
| `GOOGLE_CLIENT_SECRET` | secret de ce client (ci-dessous) |
| `ADMIN_EMAILS` | ton adresse Gmail ; plusieurs adresses séparées par des virgules |
| `API_IMAGE` | `sitevitrine-api:latest` |
| `WEB_IMAGE` | `sitevitrine-web:latest` |

#### Identifiants Google (back-office)

Le back-office se connecte avec un compte Google : il faut déclarer le site auprès de Google, une
fois. Dans la [console Google Cloud](https://console.cloud.google.com/) :

1. créer un projet (sélecteur de projet en haut à gauche → **New project**), par exemple `site-vitrine` ;
2. **APIs & Services → OAuth consent screen** → **Get started** : nom de l'application, adresse de
   support, audience **External**, adresse de contact ;
3. **Audience** : laisser l'application en mode **Testing** et ajouter ton adresse Gmail dans
   **Test users**. Seuls ces comptes peuvent se connecter — une seconde barrière, en plus
   d'`ADMIN_EMAILS` ;
4. **Clients → Create client**, type **Web application**, puis dans **Authorized redirect URIs** :
   - `https://<domaine>/api/auth/signin-google` (production) ;
   - `http://localhost:4200/api/auth/signin-google` et `http://localhost:8082/api/auth/signin-google`
     (développement) ;
5. **Create**, puis copier le **Client ID** et le **Client secret** dans les variables ci-dessus.

Le secret ne va que dans Portainer (ou le `.env` local, ignoré par git) : jamais dans le dépôt ni le
frontend. S'il fuite : **Clients** → ce client → ajouter un nouveau secret, le mettre dans
Portainer, puis supprimer l'ancien.

Puis **Deploy the stack**. Le premier déploiement prend plusieurs minutes : il télécharge le SDK
.NET et les paquets npm pour compiler les images.

Les services démarrent en cascade : `db` doit être saine avant que `api-init` ne migre le schéma,
`api-init` doit être **sorti en succès** avant `api`, et `api` doit être saine avant `web`. Si la
migration échoue, la stack s'arrête là plutôt que de démarrer une API sur une base douteuse.

Dans les logs de `sitevitrine-api-init` (**Portainer → Containers → Logs**), attendre `Schéma à
jour.` puis la sortie du conteneur ; dans ceux de `sitevitrine-api`,
`Now listening on: http://[::]:8080`. `db`, `api` et `web` doivent finir en `healthy`, `api-init`
en `exited (0)`.

### 5. Accéder à l'administration technique

À ne pas confondre avec le back-office d'édition (étape 8). L'administration technique n'est publiée que sur `127.0.0.1:<ADMIN_PORT>` du serveur. VS Code sait l'amener sur
ton poste :

1. fenêtre VS Code connectée au serveur (étape 2) → panneau **Ports** (à côté du Terminal) ;
2. **Forward a Port** → saisir la valeur de `ADMIN_PORT` ;
3. ouvrir `http://localhost:<ADMIN_PORT>/scalar` dans le navigateur.

Tant que la fenêtre VS Code reste connectée, le tunnel reste ouvert. L'équivalent en ligne de
commande est `ssh -L <ADMIN_PORT>:127.0.0.1:<ADMIN_PORT> utilisateur@serveur`.

L'interface Scalar liste tous les endpoints avec leurs formulaires — état de la base, import et
export du contenu. Détail dans [`backend/README.md`](backend/README.md#administration-technique-port-8081).

Il n'y a **ni compte ni mot de passe** : la sécurité repose entièrement sur le fait que ce port
n'est pas joignable depuis le réseau. Ne jamais le publier sur `0.0.0.0` ni le déclarer dans NPM
sans avoir d'abord ajouté une authentification.

### 6. Charger le contenu (premier déploiement seulement)

La base est vide : la migration crée le schéma, elle n'insère **aucune** donnée. C'est voulu — un
redéploiement ne doit jamais écraser silencieusement du contenu.

Dans Scalar :

1. `GET /admin/status` → vérifier `contentFile.exists: true` (sinon, `CONTENT_DIR` ne pointe pas
   sur le dossier de l'étape 2) et `backOffice.googleConfigured: true` (sinon,
   l'une des trois variables Google de l'étape 4 est vide) ;
2. `POST /admin/content/import` → le rapport indique ce qui a été créé, mis à jour ou laissé tel
   quel. Les images de `content/images/` sont copiées dans le volume `storage` au passage.

⚠️ Une fois le back-office en service, **ne plus importer** : l'import écraserait avec le fichier tout
ce qui a été modifié depuis dans le back-office.

### 7. Publier le domaine dans Nginx Proxy Manager

**Hosts → Proxy Hosts → Add Proxy Host** :

| Champ | Valeur |
| ----- | ------ |
| Domain Names | le domaine (ou un sous-domaine de test, voir ci-dessous) |
| Scheme | `http` |
| Forward Hostname / IP | `sitevitrine-web` |
| Forward Port | `80` |
| Block Common Exploits | activé |
| Onglet SSL | _Request a new SSL Certificate_, puis **Force SSL** |

Rien à configurer pour `/api` : c'est le Nginx du conteneur `web` qui s'en charge. Si le dépôt d'une
image échoue avec une erreur `413`, c'est la taille maximale de requête de NPM : ajouter
`client_max_body_size 10m;` dans l'onglet **Advanced** du proxy host. Pour que NPM
résolve `sitevitrine-web`, son conteneur doit partager le réseau `PROXY_NETWORK` (étape 1).

**Remplacer un site déjà en ligne sans coupure** : publier d'abord sur un sous-domaine de test
(enregistrement DNS `A` vers l'IP du serveur, chez le registrar), valider, puis **éditer** le proxy
host existant du domaine principal pour lui donner `sitevitrine-web` / `80` comme cible. Supprimer
ensuite le proxy host de test et l'ancienne stack.

Vérification dans le navigateur : la page Ressources affiche les avis, et
`https://<domaine>/api/resources` renvoie du JSON.

### 8. Utiliser le back-office

`https://<domaine>/admin` → **Se connecter avec Google**, avec un compte listé dans `ADMIN_EMAILS`.
Aucun lien du site n'y mène.

- **Ajouter / modifier** une ressource : formulaire ; l'image se **glisse-dépose** dans la zone
  prévue et part aussitôt vers l'API ;
- **Réordonner** : glisser une ligne de la liste ;
- **Images inutilisées** : supprimées automatiquement. L'ancienne image d'une ressource modifiée ou
  supprimée disparaît dès l'enregistrement ; une image déposée dans un formulaire finalement
  abandonné disparaît au bout d'une heure.

La session dure 8 h, prolongée tant qu'on s'en sert. **Se déconnecter** ferme la session du site,
pas celle de Google.

### 9. Mettre à jour

> **Passage au back-office (une seule fois, sur une base déjà remplie).** Les images ne font plus
> partie de l'image Docker du frontend : après le premier redéploiement, elles sont en 404 tant
> qu'elles ne sont pas dans le volume `storage`. Et la clé `imageUrl` du YAML est devenue
> `imageFile` (la migration `RenameImageUrlToImageFile` renomme la colonne sans perte). Donc :
> créer les identifiants Google et renseigner leurs variables (étape 4), remplacer sur le serveur `resources.yaml` par la
> version du dépôt et y ajouter `images/` (étape 2), redéployer, puis lancer **un** import (étape 6)
> — il copie les images et laisse le contenu inchangé.

**Le code** : pousser sur GitHub, puis **Portainer → Stacks → site-vitrine → Pull and redeploy**.
Le volume `pgdata` n'est pas touché : les données sont conservées, et seules les migrations en
attente sont appliquées par `api-init`. Compose ne recrée que les services dont l'image ou la
définition a changé — modifier le frontend ne redémarre donc ni l'API ni la base.

> ⚠️ **Sans reconstruction, l'ancien code est redéployé sans prévenir.** Compose réutilise l'image
> existante tant que son tag n'a pas changé, même si les sources ont été modifiées. Après un
> redéploiement, vérifier dans **Portainer → Images** que `sitevitrine-api` / `sitevitrine-web` ont
> une date de création récente. Le symptôme typique d'un oubli est une trace d'erreur qui mentionne
> un fichier source supprimé.

**Le contenu** n'est pas concerné par un redéploiement : il vit dans la base et le volume `storage`,
et s'édite dans le back-office. Pour garder une copie lisible dans le dépôt : `GET /admin/content/export`
(étape 5), coller le résultat dans `content/resources.yaml`, et récupérer les nouvelles images avec la
commande de sauvegarde ci-dessous.

---

## Sauvegarde et restauration

L'état vit dans deux volumes : `pgdata` (la base) et `storage` (images et clés de session). Pour la
base, un dump logique est plus sûr qu'une copie du volume (il survit à un changement de version
majeure de PostgreSQL).

```bash
docker exec sitevitrine-db pg_dump -U sitevitrine -d sitevitrine -Fc > sitevitrine-$(date +%F).dump
```

Restauration :

```bash
docker exec -i sitevitrine-db pg_restore -U sitevitrine -d sitevitrine --clean < sitevitrine-2026-08-11.dump
```

Les images, copiées hors du conteneur puis remises en place :

```bash
docker cp sitevitrine-api:/app/storage/images ./images-$(date +%F)
```

```bash
docker cp ./images-2026-08-11/. sitevitrine-api:/app/storage/images/
```

Les deux vont ensemble : la base référence les images par leur nom de fichier.

---

## Points d'attention

- **`db` n'est exposé nulle part**, et de `api` seul le port d'administration est publié, sur
  `127.0.0.1`. Seul `web` rejoint le réseau du proxy. Ne pas publier le port de PostgreSQL « pour
  déboguer avec pgAdmin » — passer par le tunnel SSH, ou par la console du conteneur dans Portainer.
- **Le mot de passe de la base n'est pas versionné** : il est saisi dans Portainer, et `.env` est
  dans `.gitignore`. Il n'est appliqué qu'à la **création** du volume : le changer dans Portainer
  sur un volume existant n'a aucun effet, PostgreSQL ignore `POSTGRES_PASSWORD` si le répertoire de
  données est déjà initialisé.
- **Le back-office `/admin` est joignable depuis internet**, réservé aux comptes Google listés dans
  `ADMIN_EMAILS` (adresse vérifiée par Google). Le site ne stocke aucun mot de passe ; la session est
  un cookie `HttpOnly`, `Secure`, `SameSite=Lax` (protection CSRF des écritures).
- **Connexion Google derrière les proxys** : Google exige une adresse de retour en `https`, que l'API
  déduit de l'en-tête `X-Forwarded-Proto` posé par NPM. Nginx le relaie (`map` de
  `frontend/nginx.conf`) et `UseForwardedHeaders` l'applique (`Program.cs`, deux proxys de
  confiance). Ajouter ou retirer un maillon (un CDN devant NPM…) impose de revoir ces deux réglages ;
  symptôme sinon : `redirect_uri_mismatch` sur la page Google.
- **L'administration technique n'a pas d'authentification** : elle repose sur l'isolation du port
  `8081`. Cette garantie tombe dès qu'on publie ce port autrement que sur la boucle locale.
- **Migrations dans un conteneur dédié**, pas au démarrage de l'API : un échec se voit comme un
  conteneur en erreur, et l'API ne démarre pas sur un schéma incertain. Tient aussi si la stack
  passait un jour à plusieurs répliques.
- **OpenAPI n'est pas exposé publiquement** : le document et son interface Scalar ne répondent que
  sur le port d'administration, en production comme en développement.
