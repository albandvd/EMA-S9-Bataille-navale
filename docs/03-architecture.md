# 03 — Architecture

> Ce document suppose **.NET 10** (LTS). Vérifiez avec `dotnet --list-sdks` et remplacez
> partout `net10.0` par votre TFM si besoin. Les deux postes du binôme doivent utiliser la
> **même** version : ajoutez un `global.json` (voir §6).

---

## 1. Arborescence du monorepo

```
bataille-navale/
├── .claude/                     # contexte Claude Code (versionné, partagé)
│   ├── settings.json            # hooks → journal automatique
│   ├── hooks/
│   ├── commands/                # slash-commands du projet
│   └── skills/                  # skills métier
├── .github/workflows/ci.yml
├── docs/
│   ├── 01-fonctionnalites.md
│   ├── 02-pouvoirs.md
│   ├── 03-architecture.md
│   ├── 04-assets.md
│   └── adr/                     # décisions d'architecture, 1 fichier = 1 décision
├── contracts/
│   └── openapi.yaml             # source de vérité du contrat HTTP
├── assets/
│   ├── sprites/ icons/ audio/ fonts/
│   └── CREDITS.md               # licences — obligatoire
├── src/
│   ├── Naval.Shared/            # ① bibliothèque de modèles
│   ├── Naval.Api/               # ② API ASP.NET Core
│   └── Naval.App/               # ③ front Blazor WebAssembly
├── tests/
│   └── Naval.Tests/             # ④ tests
├── global.json
├── Directory.Build.props
├── Naval.sln
├── api.http
├── CLAUDE.md
├── PROMPTS.md
└── README.md
```

Quatre projets exactement, comme l'exige la consigne.

---

## 2. Les quatre projets

### ① `Naval.Shared` — bibliothèque de modèles (`net10.0`, classlib)

Deux dossiers, deux responsabilités distinctes :

```
Naval.Shared/
├── Domain/                 # le jeu. Aucune dépendance externe. Testable seul.
│   ├── Board.cs  Ship.cs  Fleet.cs  Coordinate.cs
│   ├── GameState.cs  GameEngine.cs  TurnResolver.cs
│   ├── Powers/            PowerDefinition.cs  IPowerHandler.cs  PowerCatalog.cs  handlers/
│   ├── Ai/                IAiStrategy.cs  RandomAi.cs  HuntTargetAi.cs  ProbabilityAi.cs
│   └── Events/            GameEvent.cs et ses sous-types
└── Contracts/              # les DTO. Sérialisables. Ce que voit le réseau.
    ├── Requests/  Responses/  Enums/  Realtime/
```

**Règle absolue : `Domain/` n'a pas le droit de référencer `Contracts/`.** Le sens est
`Contracts → Domain` via des mappeurs situés dans `Contracts/Mapping/`. Si vous inversez, votre
modèle de jeu devient l'esclave de votre format JSON et vous ne pourrez plus rien changer.

Aucun `using Microsoft.AspNetCore.*` dans ce projet. Blazor WebAssembly le référence : tout ce
qui est serveur-only casserait la compilation WASM.

### ② `Naval.Api` — ASP.NET Core (`net10.0`, web)

```
Naval.Api/
├── Program.cs
├── Endpoints/          GameEndpoints.cs  FleetEndpoints.cs  ShotEndpoints.cs
│                       PowerEndpoints.cs  CatalogEndpoints.cs
├── Hubs/               GameHub.cs
├── Grpc/               NavalReplayService.cs  (+ Protos/naval.proto)
├── Services/           IGameStore.cs  InMemoryGameStore.cs  GameService.cs
│                       AiTurnService.cs  TurnTimeoutService.cs
├── Validation/         CreateGameValidator.cs  FireValidator.cs  …
└── Infrastructure/     ProblemDetailsFactory.cs  PlayerTokenAccessor.cs
```

Minimal APIs groupées par `MapGroup("/api/games")`. Les endpoints ne contiennent **aucune règle
de jeu** : ils désérialisent, valident, appellent `GameService`, mappent le résultat. Une
méthode d'endpoint qui dépasse 15 lignes est un signal.

### ③ `Naval.App` — Blazor WebAssembly (`net10.0`, blazorwasm)

```
Naval.App/
├── Program.cs
├── Pages/              Index.razor  Lobby.razor  Deploy.razor  Battle.razor  Result.razor
├── Components/
│   ├── Ds/             DsConsole.razor  DsTopScreen.razor  DsBottomScreen.razor  DsButton.razor
│   ├── Game/           GridView.razor  GridCell.razor  ShipTray.razor  PowerBar.razor
│   │                   EnergyGauge.razor  ChargeMeter.razor  EventLog.razor
│   └── Fx/             ExplosionFx.razor  SplashFx.razor  SonarPingFx.razor
├── Services/           GameApiClient.cs  GameHubClient.cs  GameStateStore.cs
│                       AudioService.cs  InputService.cs
├── wwwroot/            index.html  css/  audio/  img/  fonts/
└── _Imports.razor
```

`GameStateStore` est l'unique détenteur de l'état côté client. Les composants s'y abonnent et
n'appellent jamais l'API directement. Sans cette règle, l'état se duplique dans 6 composants et
vous passerez la dernière semaine à chasser des désynchronisations.

### ④ `Naval.Tests` — xUnit (`net10.0`)

```
Naval.Tests/
├── Domain/             BoardTests.cs  PlacementTests.cs  ShotResolutionTests.cs
│                       TurnOrderTests.cs  GameOverTests.cs  AiTests.cs
│                       Powers/SonarTests.cs  Powers/OrbitalStrikeTests.cs  …
├── Api/                GameEndpointsTests.cs  (WebApplicationFactory)
│                       FullGameScenarioTests.cs
├── Contracts/          SerializationTests.cs  (round-trip JSON de chaque DTO)
└── Builders/           GameBuilder.cs  FleetBuilder.cs   (fixtures fluides)
```

Un `GameBuilder` fluide vaut trente lignes de setup dupliquées :

```csharp
var game = GameBuilder.New()
    .WithGrid(10, 10)
    .WithShipAt(ShipType.Destroyer, 2, 3, Orientation.Horizontal)
    .WithEnergy(PlayerSlot.One, 8)
    .ReadyToFight()
    .Build();
```

### Graphe de dépendances

```
Naval.Api ──────┐
                ├──► Naval.Shared
Naval.App ──────┘
Naval.Tests ──► Naval.Shared, Naval.Api
```

Aucune autre flèche. `Naval.App` ne référence jamais `Naval.Api`.

---

## 3. Le serveur fait autorité

Décision structurante, à savoir justifier en soutenance.

Le client **ne reçoit jamais** la grille adverse. L'API construit pour chaque joueur une vue
filtrée : cases inconnues = `.`, découvertes = `o`/`x`/`#`. Un joueur qui ouvre les DevTools ne
voit rien de plus qu'à l'écran.

Corollaires :
- toute action passe par le serveur, y compris en solo ;
- le client est une vue + des intentions, jamais une source de vérité ;
- le mode en ligne ne demande aucune refonte : c'est le même chemin de code, avec deux humains
  au lieu d'un humain et une IA.

C'est précisément pourquoi il faut faire ce choix **dès le mode solo**. Repousser la décision
vous condamne à réécrire le moteur au moment d'ajouter le multijoueur.

### Identité des joueurs

Pas d'authentification. À la création (ou au `join`), le serveur renvoie un `playerToken`
(GUID opaque). Le client le renvoie dans l'en-tête `X-Player-Token` et l'ajoute à la
query string SignalR. Le serveur en déduit quel slot occupe l'appelant, et donc quelle vue lui
servir.

Simple, suffisant, honnête. À documenter comme tel : ce n'est pas un mécanisme de sécurité
(le token est devinable si on le partage), c'est un mécanisme d'identification de session.

---

## 4. Transport : qui fait quoi

| Besoin | Techno | Pourquoi |
|---|---|---|
| Cycle de vie (créer, rejoindre, déployer, tirer, pouvoir) | **REST/JSON** | Testable avec `api.http`, décrit par OpenAPI, sans état |
| Notification temps réel à l'adversaire | **SignalR** | WebSocket avec repli automatique, client Blazor WASM de première classe |
| Relecture d'une partie terminée | **gRPC-Web** *(optionnel, E-29)* | Couvre la partie gRPC du référentiel ; le streaming serveur est le cas d'usage naturel d'un replay |

> gRPC **pur** ne fonctionne pas depuis un navigateur : il faut gRPC-Web côté serveur
> (`app.UseGrpcWeb()`) et `GrpcWebHandler` côté client. Si vous ne voulez pas payer ce coût,
> assumez-le en soutenance : « REST pour les commandes, SignalR pour le push ; gRPC n'apportait
> pas de bénéfice ici. » C'est une réponse acceptable, à condition de l'avoir choisie.

### Contrat du hub

```
Client → Serveur : JoinGame(gameId, token) · Fire(x, y) · UsePower(powerId, target)
                   SetReady() · SendEmote(code)
Serveur → Client : GameStateChanged(GameStateDto) · ShotResolved(ShotResultDto)
                   PowerResolved(PowerResultDto) · TurnChanged(playerId, deadlineUtc)
                   OpponentJoined(name) · OpponentLeft(graceSeconds) · GameOver(GameOverDto)
                   EmoteReceived(playerId, code)
```

Les méthodes du hub appellent le **même** `GameService` que les endpoints REST. Écrire deux
fois la logique de tir est l'erreur qui coûte le plus cher dans ce projet.

### Reconnexion

`playerToken` en `localStorage`. Au démarrage, `GET /api/games/{id}` : si la partie est vivante,
le client reprend. Un `BackgroundService` supprime les parties inactives depuis plus de 2 h.

---

## 5. Stockage

```csharp
public interface IGameStore
{
    Task<Game?>  GetAsync(Guid id, CancellationToken ct);
    Task         SaveAsync(Game game, CancellationToken ct);
    Task<IReadOnlyList<GameSummary>> ListOpenAsync(CancellationToken ct);
    Task<Game?>  FindByJoinCodeAsync(string code, CancellationToken ct);
    Task         RemoveAsync(Guid id, CancellationToken ct);
}
```

Implémentation de départ : `ConcurrentDictionary<Guid, Game>`. Justification : une partie dure
quinze minutes, personne ne reprend une partie le lendemain, et une base ajoute une dépendance
d'installation le jour de la démo. L'interface permet de brancher SQLite plus tard (E-27) sans
toucher au reste.

**Concurrence :** un `SemaphoreSlim` par partie autour de chaque mutation. Deux clics simultanés
sur deux cases différentes, c'est exactement le genre de bug qui apparaît uniquement le jour de
la démo.

---

## 6. Travailler à deux proprement

### Répartition

| | Dev A — « moteur » | Dev B — « expérience » |
|---|---|---|
| Possède | `Naval.Shared/Domain`, `Naval.Api`, tests Domain + API | `Naval.App`, assets, tests de sérialisation |
| Livre | Règles, pouvoirs, IA, endpoints, hub, validation | UI DS, composants, animations, sons, accessibilité |
| Ne touche pas | Les composants Razor | Les handlers de pouvoirs |

`Naval.Shared/Contracts` est **copropriété** : toute modification passe par une PR relue par
l'autre. C'est la frontière ; c'est aussi le seul endroit où vous pouvez vous casser mutuellement.

### Rituel

- **Jour 1, ensemble, avant tout code :** figer `contracts/openapi.yaml` et les DTO. Merger.
  À partir de là, les deux moitiés avancent en parallèle sans se bloquer.
- **Stand-up de 10 minutes** en début de session : ce que je fais, ce qui me bloque.
- **Changement de contrat = message à l'autre**, pas un commit silencieux.
- Dev B travaille sur `FakeGameApiClient` (données en dur conformes aux DTO) tant que l'API
  n'existe pas. Il n'attend jamais Dev A.

### Git

- Branche `main` protégée, jamais de push direct.
- Branches courtes : `feat/S-07-tir`, `fix/E-04-reconnexion`. Durée de vie < 2 jours.
- Commits conventionnels : `feat(S-07): résolution des tirs`, `test(P-01): sonar en bord de grille`.
- 1 PR = 1 ID de fonctionnalité. Relecture obligatoire par l'autre (même rapide : c'est le seul
  moment où chacun découvre le code de l'autre).
- `rebase` avant d'ouvrir la PR, pas de merge commits parasites.

### `global.json` — verrouiller la version du SDK

```json
{ "sdk": { "version": "10.0.100", "rollForward": "latestFeature" } }
```

Sans ça, « ça marche chez moi » arrivera dans la semaine.

### `Directory.Build.props` — règles communes

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

`TreatWarningsAsErrors` fait mal la première semaine et vous sauve la dernière.

### CI (`.github/workflows/ci.yml`)

```yaml
name: ci
on: [push, pull_request]
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '10.0.x' }
      - run: dotnet restore
      - run: dotnet build --no-restore -warnaserror
      - run: dotnet test --no-build --logger "trx;LogFileName=results.trx"
      - uses: actions/upload-artifact@v4
        if: always()
        with: { name: test-results, path: '**/results.trx' }
```

---

## 7. Création de la solution

```bash
mkdir bataille-navale && cd bataille-navale && git init
dotnet new sln -n Naval

dotnet new classlib   -o src/Naval.Shared -n Naval.Shared
dotnet new web        -o src/Naval.Api    -n Naval.Api
dotnet new blazorwasm -o src/Naval.App    -n Naval.App
dotnet new xunit      -o tests/Naval.Tests -n Naval.Tests

dotnet sln add src/Naval.Shared src/Naval.Api src/Naval.App tests/Naval.Tests

dotnet add src/Naval.Api    reference src/Naval.Shared
dotnet add src/Naval.App    reference src/Naval.Shared
dotnet add tests/Naval.Tests reference src/Naval.Shared src/Naval.Api

# Paquets
dotnet add src/Naval.Api package FluentValidation.AspNetCore
dotnet add src/Naval.Api package Microsoft.AspNetCore.OpenApi
dotnet add src/Naval.App package Microsoft.AspNetCore.SignalR.Client
dotnet add tests/Naval.Tests package Microsoft.AspNetCore.Mvc.Testing
dotnet add tests/Naval.Tests package FluentAssertions

dotnet build
```

> `Microsoft.AspNetCore.SignalR.Client` va dans **`Naval.App`**, pas dans `Naval.Shared` :
> la bibliothèque de modèles doit rester sans dépendance.

### CORS et hébergement

Deux serveurs de dev distincts (API sur `https://localhost:7001`, front sur `https://localhost:7002`)
→ CORS obligatoire, avec `AllowCredentials` sinon SignalR échoue :

```csharp
builder.Services.AddCors(o => o.AddPolicy("app", p => p
    .WithOrigins(builder.Configuration["Cors:AppOrigin"]!)
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
```

Alternative plus simple pour la démo : l'API sert aussi le front
(`app.UseBlazorFrameworkFiles(); app.MapFallbackToFile("index.html");`) → une seule origine,
plus de CORS, une seule commande à lancer. **C'est ce que je recommande pour la soutenance.**
Gardez la configuration CORS pour le développement.

---

## 8. Décisions à tracer (ADR)

Un fichier par décision dans `docs/adr/`, format court. Ce sont vos munitions pour la question
« pourquoi ce choix ? ».

| N° | Décision |
|---|---|
| 001 | Serveur autoritaire, vue filtrée par joueur |
| 002 | REST + SignalR ; place de gRPC |
| 003 | Stockage en mémoire derrière `IGameStore` |
| 004 | Grille encodée en lignes de caractères plutôt qu'en tableau d'objets |
| 005 | Taille de grille et composition de flotte retenues |
| 006 | Économie des pouvoirs (énergie / tours de charge) |
| 007 | Quatre projets, `Domain` et `Contracts` dans la même bibliothèque |

Modèle : `docs/adr/ADR-000-template.md`.
