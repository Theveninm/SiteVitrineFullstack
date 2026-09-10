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
                                     └── statiques Angular    │               │
                                                              │               │
   toi ──[ tunnel SSH ]──> 127.0.0.1:8081 ────────────────────┘               │
                            back-office                                       │
                                                          api-init ───────────┘
                                                    (migre le schéma, puis s'arrête)
```

Le frontend n'appelle que des **URL relatives** (`/api/...`) : c'est le Nginx qui sert le site qui
relaie ce préfixe vers l'API, sur le réseau interne. Conséquences :

- le navigateur ne voit qu'une seule origine, donc **aucun CORS** à configurer ;
- NPM n'a qu'un hôte à connaître, le conteneur `web` ;
- `db` n'est exposé nulle part, et de `api` seul le port d'administration est publié — sur la
  boucle locale de l'hôte, donc injoignable depuis le réseau, y compris par NPM.

Trois responsabilités, trois mécanismes distincts — c'est le principe d'organisation de la stack :

| Responsabilité | Mécanisme | Quand |
| --- | --- | --- |
| **Schéma** (migrations EF) | conteneur `api-init`, s'exécute et s'arrête | à chaque déploiement, automatique |
| **Contenu** (les avis) | [`content/resources.yaml`](content/resources.yaml), monté en volume | jamais automatique — commande ou back-office |
| **Administration** | port `8081`, hors du chemin public | tunnel SSH |

Le contenu éditorial ne vit **pas** dans le code : corriger une faute dans un avis n'exige ni
recompilation ni redéploiement. Voir [`backend/README.md`](backend/README.md#modifier-le-contenu).

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

Voir [`backend/README.md`](backend/README.md) pour lancer l'API depuis l'IDE, générer une
migration, ou modifier le contenu des ressources.

---

## Déploiement (Portainer)

### 1. Repérer le réseau de Nginx Proxy Manager

**Portainer → Networks.** Noter le nom du réseau auquel appartient le conteneur NPM (souvent
`npm_default`). Il est référencé comme réseau `external` dans `docker-compose.yml` : le nom doit
être exact, sinon la stack refuse de démarrer.

### 2. Créer la stack

**Portainer → Stacks → Add stack**, nom `site-vitrine`.

- **Méthode recommandée — _Repository_** : URL du dépôt Git, `docker-compose.yml` comme
  _Compose path_. Portainer clone le dépôt sur le serveur et construit les deux images lui-même.
  Les mises à jour se font ensuite d'un clic sur **Pull and redeploy**.
- **Alternative — _Web editor_** : coller le contenu de `docker-compose.yml`. Le serveur n'a alors
  pas les sources : il faut supprimer les blocs `build:` et pointer les `image:` sur des images
  déjà publiées dans un registre.

### 3. Variables d'environnement

Dans la section **Environment variables** de la stack, saisir les entrées de `.env.example` :

| Variable | Valeur |
| -------- | ------ |
| `POSTGRES_DB` | `sitevitrine` |
| `POSTGRES_USER` | `sitevitrine` |
| `POSTGRES_PASSWORD` | mot de passe long et aléatoire (`openssl rand -base64 32`) |
| `PROXY_NETWORK` | nom relevé à l'étape 1 |
| `API_IMAGE` | `sitevitrine-api:latest` |
| `WEB_IMAGE` | `sitevitrine-web:latest` |

Puis **Deploy the stack**.

Les services démarrent en cascade : `db` doit être saine avant que `api-init` ne migre le schéma,
`api-init` doit être **sorti en succès** avant `api`, et `api` doit être saine avant `web`. Si la
migration échoue, la stack s'arrête là plutôt que de démarrer une API sur une base douteuse.

Dans les logs de `sitevitrine-api-init` (**Portainer → Containers → Logs**), attendre `Schéma à
jour.` puis la sortie du conteneur ; dans ceux de `sitevitrine-api`,
`Now listening on: http://[::]:8080`. `db`, `api` et `web` doivent finir en `healthy`, `api-init`
en `exited (0)`.

### 4. Charger le contenu (premier déploiement seulement)

La base est vide : la migration crée le schéma, elle n'insère **aucune** donnée. C'est voulu — un
redéploiement ne doit jamais écraser silencieusement du contenu.

```bash
docker compose run --rm api import
```

Ou, une fois le tunnel SSH ouvert (étape 6), le bouton `POST /admin/content/import` dans
`http://localhost:8081/scalar`. Le résultat indique ce qui a été créé, mis à jour, ou laissé tel
quel. La commande est rejouable sans risque.

### 5. Publier le domaine dans Nginx Proxy Manager

Un seul **Proxy Host** :

| Champ | Valeur |
| ----- | ------ |
| Domain Names | `mondomaine.fr` |
| Scheme | `http` |
| Forward Hostname | `sitevitrine-web` |
| Forward Port | `80` |
| Websockets Support | activé |
| SSL | demander un certificat Let's Encrypt, puis **Force SSL** |

Rien à configurer pour `/api` : c'est le Nginx du conteneur `web` qui s'en charge. Pour que NPM
résolve `sitevitrine-web`, son conteneur doit partager le réseau `PROXY_NETWORK` (étape 1).

Vérification depuis un poste client :

```bash
curl -s https://mondomaine.fr/api/resources | head -c 200
```

### 6. Accéder au back-office

Le port `8081` n'est publié que sur `127.0.0.1` du serveur : il faut donc s'y rendre depuis le
serveur lui-même, ou ouvrir un tunnel depuis ton poste :

```bash
ssh -L 8081:127.0.0.1:8081 utilisateur@serveur
```

Puis `http://localhost:8081/scalar` : l'interface liste tous les endpoints avec leurs formulaires
— état de la base, import et export du contenu. Détail dans
[`backend/README.md`](backend/README.md#back-office-port-8081).

Il n'y a **ni compte ni mot de passe** : la sécurité repose entièrement sur le fait que ce port
n'est pas joignable depuis le réseau. Ne jamais le publier sur `0.0.0.0` ni le déclarer dans NPM
sans avoir d'abord ajouté une authentification.

### 7. Mettre à jour

**Portainer → Stacks → site-vitrine → Pull and redeploy**, en cochant **Re-pull image and
redeploy**. Le volume `pgdata` n'est pas touché : les données sont conservées, et seules les
migrations en attente sont appliquées par `api-init`. Compose ne recrée que les services dont
l'image ou la définition a changé — modifier le frontend ne redémarre donc ni l'API ni la base.

> ⚠️ **Sans reconstruction explicite, l'ancien code est redéployé sans prévenir.** `docker compose
> up` réutilise l'image existante tant que son tag n'a pas changé, même si les sources ont été
> modifiées. En ligne de commande, c'est `docker compose up -d --build`. Le symptôme typique est
> une trace d'erreur qui mentionne un fichier source que tu as supprimé.

Le **contenu** n'est pas concerné par un redéploiement : il n'est jamais rechargé tout seul. Après
avoir édité `content/resources.yaml`, relancer l'import (étape 4).

---

## Sauvegarde et restauration

Tout l'état vit dans le volume `pgdata`. Un dump logique est plus sûr qu'une copie du volume
(il survit à un changement de version majeure de PostgreSQL).

```bash
docker exec sitevitrine-db pg_dump -U sitevitrine -d sitevitrine -Fc > sitevitrine-$(date +%F).dump
```

Restauration :

```bash
docker exec -i sitevitrine-db pg_restore -U sitevitrine -d sitevitrine --clean < sitevitrine-2026-08-11.dump
```

---

## Points d'attention

- **`db` n'est exposé nulle part**, et de `api` seul le port d'administration est publié, sur
  `127.0.0.1`. Seul `web` rejoint le réseau du proxy. Ne pas publier le port de PostgreSQL « pour
  déboguer avec pgAdmin » — passer par le tunnel SSH, ou par la console du conteneur dans Portainer.
- **Le mot de passe de la base n'est pas versionné** : il est saisi dans Portainer, et `.env` est
  dans `.gitignore`. Il n'est appliqué qu'à la **création** du volume : le changer dans Portainer
  sur un volume existant n'a aucun effet, PostgreSQL ignore `POSTGRES_PASSWORD` si le répertoire de
  données est déjà initialisé.
- **Le back-office n'a pas d'authentification** : il repose sur l'isolation du port `8081`. Cette
  garantie tombe dès qu'on publie ce port autrement que sur la boucle locale.
- **Migrations dans un conteneur dédié**, pas au démarrage de l'API : un échec se voit comme un
  conteneur en erreur, et l'API ne démarre pas sur un schéma incertain. Tient aussi si la stack
  passait un jour à plusieurs répliques.
- **OpenAPI n'est pas exposé publiquement** : le document et son interface Scalar ne répondent que
  sur le port d'administration, en production comme en développement.
