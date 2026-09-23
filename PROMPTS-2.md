# Échanges décisifs avec l'IA

Sélection des 6 échanges structurants, synthétisés depuis le journal automatique
(`.claude/hooks/journal-prompt.sh`). Quand le prompt n'a pas été consigné mot pour mot, la
demande est reformulée et signalée comme telle.

**Note :** L'ensemble des prompts qui ont été utilisés ont été générés par IA (Dans la très grande majorité par Claude Opus 5, effort moyen). Nous avons passé du temps pour faire comprendre le contexte du projet à Claude dans le but de lui faire générer des prompts précis, qui correpondent à ce que nous attendions afin de guider au mieux le ou les agents qui se sont occupés de la partie développement.

---

## Backend du socle (S-01 à S-17)

* Outil / modèle si connu : Claude Code (modèle exact non consigné)
* Contexte : aucun code serveur ; specs dans `docs/01-fonctionnalites.md`, archi dans `docs/03-architecture.md`.
* Prompt réellement utilisé : « En te basant sur `docs/01-fonctionnalites.md` (identifiants S-01 à S-17) et sur l'arborescence de `docs/03-architecture.md`, implémente tout le backend de la partie solo. Dans `src/Naval.Shared/Domain`, modélise `Game`, `Player`, `Ship` et `Board` avec des identifiants typés (`GameId`, `PlayerId`) et donne à chaque joueur deux plateaux distincts (Incoming pour ses navires et les tirs reçus, Outgoing pour ses tirs) afin qu'aucune vue construite pour un joueur ne puisse lire les navires adverses non découverts. Place les règles (placement, bornes, chevauchement, tir, touché/coulé, fin de partie, alternance des tours) dans un `GameEngine` statique et pur, testable sans infrastructure. Orchestre tout depuis un unique `GameService` qui sérialise chaque mutation par un `SemaphoreSlim` propre à la partie et s'appuie sur un `IGameStore` en mémoire (pas de base de données). Ajoute une IA `HuntTargetAi` (chasse en damier `(x + y) % 2 == 0`, puis ciblage des cases adjacentes après une touche). Dans `src/Naval.Api`, expose en Minimal APIs `POST /api/games`, `/fleet`, `/shots` et `/forfeit`, avec des endpoints de moins de 15 lignes, un `playerToken` lu dans l'en-tête `X-Player-Token`, une validation des requêtes entrantes et toutes les erreurs en `ProblemDetails` avec une extension `code` stable (`NOT_YOUR_TURN` en 409, `CELL_ALREADY_TARGETED`…). Les DTO sont des `record` avec `init` dans `Contracts/`, le mapping va dans `Contracts/Mapping/`, et `contracts/openapi.yaml` doit rester aligné. Écris les tests xUnit + FluentAssertions dans `tests/Naval.Tests/Domain/` via `GameBuilder`, puis vérifie `dotnet build` (0 warning, `TreatWarningsAsErrors`) et `dotnet test`. »
* Réponse et hypothèses résumées : deux `Board` par joueur (Incoming/Outgoing) ; `GameEngine` statique pur ; `GameService` seul orchestrateur ; IA `HuntTargetAi` en damier `(x + y) % 2 == 0` en phase de chasse.
* Décision et justification : retenu. Le double plateau empêche `BuildTargetView` de voir les navires adverses non découverts ; le moteur pur se teste sans instancier de `Game` ; aucune règle dans les endpoints (> 15 lignes = alerte).
* Scénario ou commande de vérification : `dotnet build`, `dotnet test`, puis `POST /api/games`, `/fleet`, `/shots`, `/forfeit`.
* Résultat attendu, puis résultat observé : attendu 0 warning et endpoints conformes → observé 0 warning (TreatWarningsAsErrors), 37 tests verts, `201` à la création, `409` hors tour, `Abandoned` + `winnerId` au forfait.
* Erreur que ce contrôle pourrait détecter : tir accepté hors tour, fuite de la flotte adverse, règle dupliquée dans l'API.
* Preuves reproductibles et limites : `dotnet test` ; limite : l'IA n'est testée que sur ses règles, pas sur sa force de jeu.

---

## Branchement de `GameApiClient` sur `Naval.Api`

* Outil / modèle si connu : Claude Code
* Contexte : `Naval.Api` mergé, `GameApiClient` encore en `NotImplementedException`.
* Prompt réellement utilisé : « `Naval.Api` est maintenant mergé : remplace les `NotImplementedException` de `GameApiClient` dans `src/Naval.App` par de vrais appels HTTP, sans jamais référencer le projet `Naval.Api`. Centralise tous les appels dans une méthode privée générique `SendAsync<T>` qui ajoute l'en-tête `X-Player-Token`, désérialise les réponses avec des `JsonSerializerOptions` explicites (`JsonSerializerDefaults.Web` + `JsonStringEnumConverter`, pour éviter des valeurs par défaut silencieuses) et convertit toute réponse `ProblemDetails` en `GameApiException` portant le `code` métier. Vérifie chaque signature contre `contracts/openapi.yaml` (types de retour compris, par exemple celui du forfait) et corrige les divergences côté client. Implémente `GetFleetPresetAsync` en filtrant `GET /api/catalog/fleets` plutôt qu'en codant la flotte en dur. Aligne les ports et la configuration CORS entre l'API et le front. Si tu trouves un bug côté backend, ne le corrige pas : signale-le précisément, car cette moitié appartient à mon binôme. Valide avec `dotnet test` puis avec des appels `curl` sur l'API démarrée (création, 401 `MISSING_TOKEN`, catalogues, flotte, forfait). »
* Réponse et hypothèses résumées : point unique `SendAsync<T>` ; `JsonSerializerOptions` explicites (Web + `JsonStringEnumConverter`) ; constante locale `X-Player-Token` ; `GetFleetPresetAsync` par filtrage de `GET /api/catalog/fleets` ; ports réalignés sur 7001/7002.
* Décision et justification : retenu ; bug de contrat corrigé (`ForfeitAsync` renvoie `GameOverDto`, pas `GameStateDto`, conformément à `openapi.yaml`) ; bug backend `Player2.IsReady` signalé sans toucher au code de l'autre moitié du binôme.
* Scénario ou commande de vérification : `dotnet test` ; appels `curl` sur l'API démarrée (création, 401 `MISSING_TOKEN`, catalogues, flotte, forfait).
* Résultat attendu, puis résultat observé : 45/45 ; formes JSON conformes ; `/shots` en 409 `GAME_NOT_IN_PROGRESS` (conséquence du bug backend).
* Erreur que ce contrôle pourrait détecter : désérialisation silencieuse en valeurs par défaut, CORS bloqué, type de retour faux.
* Preuves reproductibles et limites : tir non vérifiable de bout en bout avant correction backend ; `/powers` en 404 (pas d'endpoint).

---

## Correction backend : la partie solo ne démarrait jamais

* Outil / modèle si connu : Claude Code + serveur MCP `playwright-brave`
* Contexte : partie solo bloquée en `AwaitingDeployment`.
* Prompt réellement utilisé : « En solo, la partie reste bloquée en `AwaitingDeployment` après que j'ai validé ma flotte : je ne peux jamais tirer. Trouve la cause en partant de `GameService` et de la transition vers `InProgress` (conditions de `StartBattle`, état `IsReady` de chaque joueur, traitement du joueur IA). Avant de corriger, écris dans `tests/Naval.Tests` un ou deux tests `GameServiceTests` qui reproduisent le blocage avec `GameBuilder` et vérifie qu'ils échouent ; applique ensuite le correctif le plus petit possible, sans modifier aucun contrat ni DTO, et montre que les mêmes tests passent. Termine par un `dotnet test` complet puis par un parcours réel dans le navigateur (Lobby → Deploy → Battle) avec l'API et le front démarrés, en confirmant que le statut passe à `InProgress` et que la navigation vers `/battle` a lieu. »
* Réponse et hypothèses résumées : `StartBattle` exige `Player1.IsReady && Player2.IsReady` ; l'IA n'était jamais marquée prête ; correctif d'une ligne `p2.IsReady = true;`.
* Décision et justification : retenu ; aucun contrat modifié.
* Scénario ou commande de vérification : 2 tests `GameServiceTests` vérifiés rouges (fix en stash) puis verts ; parcours Lobby → Deploy → Battle dans Brave.
* Résultat attendu, puis résultat observé : statut `InProgress`, navigation vers `/battle` → observé, 47/47.
* Erreur que ce contrôle pourrait détecter : régression du démarrage solo.
* Preuves reproductibles et limites : `dotnet test --filter GameServiceTests` ; tirs non testés au-delà du démarrage.

---

## Infra Docker (`infra/docker/`, `docker-compose.yml`)

* Outil / modèle si connu : Claude Code
* Contexte : besoin d'un déploiement conteneurisé.
* Prompt réellement utilisé : « Crée l'infrastructure de conteneurisation dans `infra/docker/` et un `docker-compose.yml` à la racine, sans ajouter de projet à la solution. Produis deux images multi-étapes : `naval-api` (build avec le SDK .NET 10, exécution sur `mcr.microsoft.com/dotnet/aspnet:10.0` avec un utilisateur non-root et un endpoint `/health` pour le healthcheck) et `naval-app` (publication Blazor WebAssembly servie par `nginx:1.27-alpine`, avec le bon type MIME `application/wasm`, la compression et un fallback SPA vers `index.html` pour les routes comme `/lobby`). Le code Blazor s'exécute dans le navigateur, donc `ApiBaseUrl` doit être une URL publique et non un nom de service Docker : injecte-la au démarrage du conteneur via `envsubst` dans `appsettings.json`, jamais au build, pour qu'une seule image serve plusieurs environnements. Côté API, lis l'origine autorisée depuis `Cors__AppOrigin` et n'autorise que celle-ci. Vérifie avec `docker compose up --build -d`, `curl /health`, `curl /appsettings.json`, les en-têtes des fichiers `.wasm`, le fallback `/lobby` et un préflight CORS depuis une origine étrangère (qui ne doit recevoir aucun en-tête `Access-Control-Allow-*`). Documente les limites (pas de TLS, pas de persistance, pas de CI) dans `infra/docker/README.md`. »
* Réponse et hypothèses résumées : image `naval-api` (aspnet:10.0) et `naval-app` (nginx 1.27-alpine) ; `ApiBaseUrl` et `Cors__AppOrigin` injectés à l'exécution (`envsubst`), jamais au build.
* Décision et justification : retenu ; le code Blazor WASM tourne dans le navigateur, un nom de service Docker y serait injoignable ; une image, plusieurs environnements.
* Scénario ou commande de vérification : `docker compose up --build -d`, `curl /health`, `curl /appsettings.json`, en-têtes `.wasm`, fallback `/lobby`, préflight CORS.
* Résultat attendu, puis résultat observé : tout conforme ; origine `http://evil.example` → 204 sans en-tête `Access-Control-Allow-*`. Premier build en échec sur `groupadd --gid 1000`, corrigé.
* Erreur que ce contrôle pourrait détecter : URL interne figée côté navigateur, MIME WASM incorrect, CORS trop permissif.
* Preuves reproductibles et limites : pas de persistance, pas de TLS, pas de CI (documenté dans `infra/docker/README.md`).

---

## Bruitages 8-bit en C# pur

* Outil / modèle si connu : Claude Code
* Contexte : aucun son ; première version via module JS.
* Prompt réellement utilisé : « Ajoute des bruitages 8-bit au jeu (tir, plouf, touché, coulé, victoire, défaite, navigation dans les menus, pose de navire, erreur…) sans aucune interop JavaScript ni `IJSRuntime` : tout doit rester en C#. Écris un petit générateur C# hors de la solution (pas de cinquième projet) qui synthétise des ondes carrées, triangulaires et du bruit avec enveloppes et glissandos, puis exporte des WAV PCM 16 bits mono dans `src/Naval.App/wwwroot/assets/audio/` ; ce sont des créations originales, donc aucune question de licence. Côté front, crée un `AudioService` injecté qui expose `Play(string)` (nom du son) et un état muet, et un composant `AudioChannel.razor` qui rend un élément `<audio autoplay>` par son joué, identifié par `@key` et retiré à la fin, avec un plafond de 6 sons simultanés pour ne pas saturer le DOM. Déclenche les sons depuis les réactions de l'UI aux changements d'état du `GameStateStore`, jamais depuis une règle de jeu. Vérifie que chaque WAV se décode (`decodeAudioData`), qu'une partie complète joue les sons dans le bon ordre sans avertissement d'autoplay, et que `grep IJSRuntime` ne renvoie plus rien ; lance `dotnet test`. »
* Réponse et hypothèses résumées : 12 WAV synthétisés par un générateur C# hors solution ; lecture via `AudioService` + `AudioChannel.razor` (`<audio autoplay>`, `@key`, plafond de 6 sons).
* Décision et justification : retenu ; créations originales (pas de licence), plus d'interop JS ; compromis : mute non persisté.
* Scénario ou commande de vérification : `decodeAudioData` ; `MutationObserver` sur `ended` pendant une partie ; `grep IJSRuntime`.
* Résultat attendu, puis résultat observé : 30 sons joués dans l'ordre, aucun avertissement d'autoplay → observé, 47/47.
* Erreur que ce contrôle pourrait détecter : son bloqué par la politique d'autoplay, DOM saturé d'éléments audio.
* Preuves reproductibles et limites : boucle `ambient-sea` exclue ; mute perdu au F5.

---

## Multijoueur en ligne (E-A, E-01 à E-09)

* Outil / modèle si connu : Claude Code
* Contexte : E-01 à E-03 existaient en REST, sans temps réel ni front.
* Prompt réellement utilisé : « En te basant sur `docs/01-fonctionnalites.md` (E-A, E-01 à E-09) et `docs/03-architecture.md`, mets en place le multijoueur en ligne. Ajoute un hub SignalR dans `Naval.Api` dont chaque méthode délègue au même `GameService` que les endpoints REST, sans dupliquer de logique, et un `GameNotifier` qui soit le seul point de diffusion : groupe `game:{id}` pour l'état public et groupe `player:{id}` pour la vue privée de chaque joueur, le `playerToken` étant lu dans la query string. `GameService` ne doit jamais dépendre de SignalR. Gère la présence avec un `PresenceService` (déconnexion tolérée pendant 60 s avant abandon) et un `TurnTimeoutService` en `BackgroundService` qui joue un tir aléatoire en réutilisant `RandomAi` quand le temps du tour est écoulé ; ces singletons doivent passer par `IServiceScopeFactory` pour ne pas capturer de service scoped. Ajoute une vue spectateur en REST (`GET /spectate`, rafraîchie par polling toutes les 2,5 s) qui ne révèle aucune case non découverte. Côté `Naval.App`, branche un client `HubConnection` qui alimente le `GameStateStore`. Mets à jour `contracts/openapi.yaml` et les DTO dans le même changement, et écris des `GameHubTests` avec `WebApplicationFactory` + `HubConnection` (alternance des tours, déconnexion, abandon propagé, absence de fuite vers un spectateur) avant de lancer `dotnet test`. »
* Réponse et hypothèses résumées : `GameNotifier` seul point de diffusion SignalR ; groupes `game:{id}` et `player:{id}` ; `PresenceService` (grâce 60 s) et `TurnTimeoutService` via `IServiceScopeFactory` ; timeout réutilisant `RandomAi` ; spectateur en polling REST 2,5 s.
* Décision et justification : retenu ; `GameService` reste ignorant de SignalR ; 3 bugs corrigés (`FleetPreset` absent de `GameStateDto`, `Player2.IsConnected` à `true` par défaut, `GameStateChanged` non poussé après abandon).
* Scénario ou commande de vérification : `dotnet test` (dont `GameHubTests` avec `WebApplicationFactory` + `HubConnection`) ; serveur réel avec timer à 5 s ; `GET /spectate`.
* Résultat attendu, puis résultat observé : 69/69 verts ; deux tirs `(temps écoulé)` enchaînés sans action ; grilles spectateur à `.` avant déploiement.
* Erreur que ce contrôle pourrait détecter : fuite vers un spectateur, service scoped capturé par un singleton, fin de partie non propagée.
* Preuves reproductibles et limites : aucune validation visuelle humaine du nouveau front (à faire à deux onglets).
