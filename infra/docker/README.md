# Déploiement Docker

Deux images, une par projet déployable (`Naval.Shared` n'est qu'une bibliothèque compilée
dedans, jamais son propre conteneur — toujours quatre projets, zéro conteneur de plus) :

| Image | Contenu | Sert sur |
|---|---|---|
| `naval-api` | `Naval.Api` publié, exécuté par `dotnet` sur l'image runtime ASP.NET | `:8080` (interne) |
| `naval-app` | `Naval.App` publié en fichiers statiques, servis par nginx | `:80` (interne) |

## Démarrage rapide

```bash
cp .env.example .env   # ajustez si besoin, les valeurs par défaut marchent en local
docker compose up --build
```

- Front : http://localhost:8080
- API : http://localhost:5119 (ex. http://localhost:5119/health)

## Le point qui casse tout si vous l'oubliez

`Naval.App` est du Blazor **WebAssembly** : le code qui lit `ApiBaseUrl` s'exécute dans le
navigateur du joueur, pas dans le conteneur `naval-app`. Un `ApiBaseUrl` figé au build vers
`http://api:8080` (nom de service Docker) serait injoignable depuis le navigateur — DNS
interne au réseau Compose, invisible de l'extérieur.

Conséquence :

- `API_BASE_URL` (conteneur `app`) et `Cors__AppOrigin` (conteneur `api`) doivent toujours
  désigner des adresses **publiques**, atteignables par le navigateur.
- Ces deux valeurs sont donc injectées **à l'exécution**, pas au build :
  - côté `api`, c'est juste une variable d'environnement ASP.NET Core classique
    (`Cors__AppOrigin`, double underscore = section `Cors:AppOrigin`) ;
  - côté `app`, un script dans `docker-entrypoint.d/` régénère `wwwroot/appsettings.json` à
    partir d'un template à chaque démarrage du conteneur, via `envsubst`. C'est ce qui permet
    de construire l'image une seule fois et de la redéployer telle quelle en dev, staging et
    prod — seule la variable d'environnement change.

Pour un vrai déploiement (domaine réel, hôtes séparés), copiez `.env.example` en `.env` à la
racine du dépôt et remplacez les deux URL par les adresses publiques réelles — jamais par un
nom de service Docker.

## Bâtir une image seule

```bash
docker build -f infra/docker/Naval.Api.Dockerfile -t naval-api .
docker build -f infra/docker/Naval.App.Dockerfile -t naval-app .
```

Le contexte de build (`.`) doit rester la racine du dépôt dans les deux cas : les Dockerfile
copient `global.json` et `Directory.Build.props` en plus du code, pour que MSBuild retrouve les
mêmes réglages (`TargetFramework`, `Nullable`, `TreatWarningsAsErrors`, verrou de SDK) qu'en
local.

## Ce qui n'est pas couvert ici

- **Pas de base de données** : `IGameStore` reste en mémoire (voir `CLAUDE.md`) — donc pas de
  volume de persistance, pas de service `db`. Une partie perdue au redémarrage du conteneur
  `api` est attendu, pas un bug.
- **Pas de TLS** : les deux services écoutent en HTTP nu à l'intérieur des conteneurs. En
  production, terminez le TLS en amont (reverse proxy / load balancer) — ce dépôt ne fournit
  pas cette couche.
- **Pas de CI** : ces Dockerfile sont faits pour être buildés et lancés, pas pour un pipeline.
