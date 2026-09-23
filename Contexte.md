# Contexte du projet

Complété par le binôme.

* Vision du projet, règles et expérience visée :
  - Bataille navale web jouable en solo contre une IA et en multijoueur en ligne.
  - Interface « Navcom » : console deux écrans façon console portable, en CSS pur.
  - Règle absolue : aucune position adverse non découverte n'atteint le client.
  - Expérience rétro : sprites SVG originaux, bruitages 8-bit maison, animations désactivables.

* Backlog, priorités et périmètre retenu :
  - Priorité 1 : socle solo S-01 à S-17 (partie, flotte, tirs, IA, forfait).
  - Priorité 2 : polish visuel et sonore (Navcom, sprites, icônes, sons).
  - Priorité 3 : E-A multijoueur (E-01 à E-09 : code, salon public, temps réel, présence, timer, spectateur).
  - Priorité 4 : pouvoirs (E-10 à E-16), commencés une fois le solo jouable : Sonar, Bombe lourde
    (3×3), Tsar Bomba (5×5, usage unique) ; `POST /api/games/{id}/powers` et méthode `UsePower`
    du hub, handlers `IPowerHandler` dans `Naval.Shared/Domain/Powers`.
  - Hors périmètre : persistance (E-27 non demandée), TLS, CI.

* Organisation du code et contrats :
  - 4 projets : `Naval.Shared`, `Naval.Api`, `Naval.App` (Blazor WASM), `Naval.Tests`.
  - `Naval.App` ne référence jamais `Naval.Api`.
  - Moteur : `GameEngine` statique pur ; `GameService` seul orchestrateur ; endpoints minces.
  - Temps réel : `GameHub` + `GameNotifier` (groupes `game:{id}` et `player:{id}`).
  - Front : pages → `GameStateStore` → `IGameApiClient` (réel ou `FakeGameApiClient`).
  - Contrat : `contracts/openapi.yaml` source de vérité, DTO dans `Naval.Shared/Contracts`.
  - Erreurs : ProblemDetails avec code métier stable (`ErrorCodes`).
  - Grilles : `BoardViewDto.Rows`, un caractère par case.
  - Authentification joueur : en-tête `X-Player-Token`.

* Commandes, ports et environnement :
  - `dotnet build` (0 warning exigé) et `dotnet test`.
  - `dotnet run --project src/Naval.Api` et `dotnet run --project src/Naval.App`.
  - Ports HTTPS : API 7001, App 7002 ; profils HTTP : 5119 / 5018.
  - SDK .NET 10 verrouillé par `global.json` (`rollForward: latestFeature`).
  - Front : `ApiBaseUrl` dans `wwwroot/appsettings.json` ; API : `Cors:AppOrigin`.
  - Docker : `docker compose up --build -d` avec `.env` issu de `.env.example`.
  - Docker : variables `API_BASE_URL` et `Cors__AppOrigin` injectées à l'exécution.
  - Santé : `GET /health`.

* Conventions et méthode de collaboration :
  - Règles communes dans `CLAUDE.md`.
  - `TreatWarningsAsErrors` et `Nullable` via `Directory.Build.props`.
  - Tout changement de contrat touche dans le même commit : DTO, mapper, `openapi.yaml`, faux client.
  - Aucune règle de jeu dans un composant Razor.
  - Binôme réparti moteur / front ; pas de modification du code de l'autre moitié sans accord.
  - Branches thématiques (`ui`, `assets`) fusionnées dans `main`.
  - Journal des prompts : hook `.claude/hooks/journal-prompt.sh` + commande `/journal`.

* Décisions structurantes et références des ADR :
  - Double plateau Incoming / Outgoing par joueur (non-fuite).
  - Moteur statique pur, `GameService` orchestrateur unique.
  - Store front centralisant appels et erreurs (`RunAsync`).
  - Serveur seule autorité ; validation front = aide visuelle.
  - Configuration réseau injectée à l'exécution (Docker).
  - Audio sans JavaScript.
  - `GameNotifier` comme unique point de diffusion SignalR.
  - Numéros d'ADR : à reporter depuis le dossier des ADR du dépôt.

* Vérifications réalisées et limites connues :
  - Tests : 37 (socle) → 47 (solo complet) → 69 (multijoueur) → 96 verts (pouvoirs).
  - Tests de régression vérifiés rouges avant correctif (`Player2.IsReady`).
  - Bout en bout : `curl` sur API réelle, parcours navigateur Brave, pilotage du DOM.
  - Docker : santé, MIME WASM, fallback SPA et CORS vérifiés.
  - Limite : validation de placement Razor sans test automatisé.
  - Limite : parcours multijoueur non validé visuellement par un humain.
  - Limite : mute non persisté, polices non auto-hébergées.
  - Limite : pas de persistance, pas de TLS, pas de CI.

* Arbitrages et évolution du périmètre :
  - Validation de placement sur faux client annulée, reprise après l'arrivée de l'API.
  - Composant `DsField` écarté (YAGNI) au profit de classes CSS.
  - Audio JS remplacé par du C# pur, à la demande du binôme.
  - Ratio d'écran 4:3 écarté au profit du 16:10.
  - Front servi par nginx plutôt que par `Naval.Api`.
  - Spectateur en polling REST plutôt qu'en groupe SignalR.
  - Montage `<Content Link>` des assets remplacé par une copie physique.
