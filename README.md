# Bataille Navale

Bataille navale à pouvoirs, jouable en solo contre l'IA ou en ligne à deux, dans une interface
inspirée des consoles portables à deux écrans. Serveur autoritaire : le client ne reçoit jamais
la position d'un navire adverse non découvert.

## Prérequis

- [.NET SDK 10](https://dotnet.microsoft.com/download) — la version exacte est verrouillée par
  [`global.json`](global.json) (`rollForward: latestFeature`, donc tout SDK 10.0.x récent
  convient). Vérifiez avec `dotnet --list-sdks`.
- Un navigateur récent (le front est du Blazor WebAssembly).
- Docker + Docker Compose, uniquement si vous voulez lancer via conteneurs (§ « Avec Docker »).

## Démarrage rapide (sans Docker)

```bash
git clone <url> && cd EMA-S9-Bataille-navale
dotnet restore
dotnet build      # 0 avertissement attendu (TreatWarningsAsErrors)
dotnet test       # 99 tests, tous verts
```

Puis, dans deux terminaux séparés :

```bash
dotnet run --project src/Naval.Api   # http://localhost:5119  (health : /health)
dotnet run --project src/Naval.App   # http://localhost:5018
```

Ouvrez `http://localhost:5018` : c'est le front. L'API et le front se trouvent déjà l'un
l'autre par défaut (`ApiBaseUrl` dans `src/Naval.App/wwwroot/appsettings.json` pointe sur
`http://localhost:5119`, et la policy CORS de l'API autorise `http://localhost:5018` par
défaut) — aucune configuration n'est nécessaire pour un premier lancement en local.

Pour lancer en HTTPS (profils `https` des deux projets, ports 7001/7002), utilisez
`dotnet run --project <projet> --launch-profile https`.

## Avec Docker

Deux conteneurs : `api` (Naval.Api, ASP.NET Core) et `app` (Naval.App compilé en statique,
servi par nginx). Vérifié de bout en bout (build, santé des deux conteneurs, partie créée via
l'API conteneurisée, gRPC-Web conteneurisé répond) — voir le détail des commandes ci-dessous.

```bash
cp .env.example .env   # valeurs par défaut déjà adaptées à un test en localhost
docker compose up --build -d
docker compose ps      # les deux services doivent passer "healthy"
```

Front sur `http://localhost:${APP_PUBLISHED_PORT:-8080}`, API sur
`http://localhost:${API_PUBLISHED_PORT:-5119}` (santé : `/health`). Pour vérifier rapidement
sans ouvrir de navigateur :

```bash
curl http://localhost:5119/health   # {"status":"Healthy","activeGames":0}
curl -o /dev/null -w '%{http_code}\n' http://localhost:8080/   # 200
```

Le gRPC-Web du §« Rejouer une partie terminée » ci-dessus répond aussi bien contre l'API
conteneurisée que contre `dotnet run` — mêmes ports, rien de spécifique à Docker.

```bash
docker compose logs -f      # suivre les deux services
docker compose down         # arrêter et nettoyer
```

Si vous exposez le jeu au-delà de `localhost` (VM, réseau local), adaptez `API_PUBLIC_URL` et
`APP_PUBLIC_URL` dans `.env` : ce sont des adresses que le **navigateur du joueur** doit pouvoir
atteindre, jamais un nom de service Docker (`http://api:8080` ne résoudra pas côté client).
Détails, variables d'environnement et dépannage :
[`infra/docker/README.md`](infra/docker/README.md).

## Jouer une partie

1. Sur l'écran d'accueil, créez une partie solo (contre l'IA) ou en ligne.
2. Déployez votre flotte (placement manuel ou `Flotte aléatoire`), puis validez.
3. Tirez à tour de rôle jusqu'à la destruction d'une des deux flottes.
4. Sur l'écran de résultat, **Rejouer** ramène au lobby pour démarrer une nouvelle partie.

## Rejouer une partie terminée (gRPC-Web)

`NavalReplayService` (`src/Naval.Api/Grpc/`) expose un RPC en streaming serveur,
`StreamReplay`, qui rejoue le journal d'événements d'une partie terminée — c'est le volet gRPC
du référentiel (`E-29`). Il n'y a pas de client dans `Naval.App` (voir
[`docs/adr/ADR-002-rest-signalr-grpc.md`](docs/adr/ADR-002-rest-signalr-grpc.md) pour le
périmètre retenu) ; pour le voir fonctionner :

```bash
dotnet test --filter NavalReplayServiceTests
```

Ce test fait un vrai échange gRPC-Web (via `GrpcWebHandler`, l'encodage HTTP qu'utiliserait un
navigateur) contre le serveur réel, et démontre les deux erreurs attendues : `NOT_FOUND` (partie
inconnue) et `FAILED_PRECONDITION` (partie pas encore terminée), chacune avec le code métier
stable en trailer gRPC. Pour l'interroger à la main, un client comme `grpcurl` ou Postman peut
pointer sur `src/Naval.Api/Protos/naval.proto` contre l'API démarrée (`http://localhost:5119`).

## Où trouver quoi

| Vous cherchez | Fichier |
|---|---|
| Ce qu'il faut construire, et dans quel ordre | `docs/01-fonctionnalites.md` |
| Les pouvoirs, leurs coûts, leur équilibrage | `docs/02-pouvoirs.md` |
| L'organisation du dépôt et du binôme | `docs/03-architecture.md` |
| Les assets et comment les produire | `docs/04-assets.md` |
| Le contrat HTTP | `contracts/openapi.yaml` |
| Le contrat gRPC (relecture de partie) | `src/Naval.Api/Protos/naval.proto` |
| Les DTO | `src/Naval.Shared/Contracts/` |
| Les décisions d'architecture (ADR) | `docs/adr/` |
| Le journal des échanges avec l'IA (livrable noté) | `PROMPTS.md` |
| Les revues critiques de propositions IA (livrable noté) | `REVUE-IA.md` |
| Requêtes HTTP prêtes à l'emploi (VS Code / JetBrains) | `api.http` |

## Commandes utiles

```bash
dotnet build                                   # compile tout
dotnet test                                    # tous les tests
dotnet test --filter FullyQualifiedName~Powers # tests des pouvoirs
dotnet format                                  # mise en forme
```

## Limites connues

- Pas de persistance (parties en mémoire, `E-27` non demandée), pas de TLS en local, pas de CI.
- gRPC-Web est limité à la relecture d'une partie terminée (`E-29`) ; il n'y a pas de client
  gRPC-Web dans `Naval.App`, ni de gRPC sur le cycle de vie du jeu (choix documenté dans
  [`docs/adr/ADR-002-rest-signalr-grpc.md`](docs/adr/ADR-002-rest-signalr-grpc.md)).
- Le mute audio n'est pas persisté entre deux sessions ; les polices ne sont pas auto-hébergées.
