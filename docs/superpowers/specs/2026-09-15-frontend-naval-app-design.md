# Naval.App — Cycle 1 : socle frontend

- **Date :** 2026-09-15
- **Statut :** validé (brainstorming), en attente de plan d'implémentation
- **Auteurs :** Laurent (binôme "expérience"), assisté de Claude Code

## Contexte

Le rôle de ce binôme se limite au développement du frontend (`src/Naval.App`, Blazor
WebAssembly). Le projet est actuellement au stade "contrats-only" : seul
`src/Naval.Shared/Contracts` existe. Aucun projet `Naval.App` n'a encore été scaffoldé.

Ce document couvre le **premier cycle** de développement front : le socle applicatif, la coque
de console (`Ds/`), les composants de jeu (`Game/`), et les cinq écrans (`Pages/`), le tout
branché sur un `FakeGameApiClient` en attendant que `Naval.Api` existe côté binôme moteur.

**Hors périmètre de ce cycle**, reporté à un cycle "polish" ultérieur :
- `Components/Fx/` (animations : explosion, gerbe d'eau, onde de sonar)
- `AudioService` et les sons
- `GameApiClient` réel (implémentation HTTP), au-delà de son interface

Ces éléments seront traités par un cycle brainstorming/plan séparé, une fois le socle solo
jouable de bout en bout côté API (conformément à l'ordre de construction du `CLAUDE.md`).

## Contraintes héritées du cadrage projet

- `Naval.App` ne référence jamais `Naval.Api` (règle de dépendance absolue).
- Aucune règle de jeu dans les composants Razor : le front affiche l'état reçu et envoie des
  intentions.
- Le catalogue de pouvoirs n'est jamais codé en dur : il vient (via le fake, puis la vraie API)
  de `GET /api/catalog/powers`.
- `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors` activés. Pas de `async void`, pas de
  `.Result`/`.Wait()`.
- `dotnet build` sans nouvel avertissement et `dotnet test` vert avant de clore toute tâche.

## Architecture

```
Naval.App/ (blazorwasm, réf. Naval.Shared)
├── Program.cs                   # DI : IGameApiClient → FakeGameApiClient, GameStateStore
├── App.razor / _Imports.razor
├── Pages/
│   ├── Index.razor
│   ├── Lobby.razor
│   ├── Deploy.razor
│   ├── Battle.razor
│   └── Result.razor
├── Components/
│   ├── Ds/
│   │   ├── DsConsole.razor
│   │   ├── DsTopScreen.razor
│   │   ├── DsBottomScreen.razor
│   │   ├── DsButton.razor
│   │   └── DsErrorBanner.razor
│   └── Game/
│       ├── GridView.razor
│       ├── GridCell.razor
│       ├── ShipTray.razor
│       ├── PowerBar.razor
│       ├── EnergyGauge.razor
│       ├── ChargeMeter.razor
│       └── EventLog.razor
├── Services/
│   ├── IGameApiClient.cs
│   ├── FakeGameApiClient.cs
│   ├── GameApiClient.cs         # squelette non branché dans ce cycle
│   └── GameStateStore.cs
└── wwwroot/
    ├── index.html
    └── css/tokens.css, console.css, grid.css

tests/Naval.Tests/App/
├── GameStateStoreTests.cs
└── FakeGameApiClientTests.cs
```

Le `Program.cs` enregistre `IGameApiClient` → `FakeGameApiClient` par défaut. Basculer vers la
vraie API se fera par un seul changement de registration DI, une fois `GameApiClient` implémenté
dans un cycle ultérieur.

## Flux de données — `GameStateStore`

`GameStateStore` est le **seul** point d'appel vers `IGameApiClient`. Aucune `Page` ni aucun
composant `Game/*` n'injecte `IGameApiClient` directement.

```csharp
public sealed class GameStateStore
{
    public event Action? StateChanged;

    public GameStateDto? CurrentGame { get; private set; }
    public IReadOnlyList<OpenGameDto> OpenGames { get; private set; } = [];
    public ApiProblemDto? LastError { get; private set; }

    private readonly IGameApiClient _api;

    public GameStateStore(IGameApiClient api) => _api = api;

    public async Task CreateGameAsync(CreateGameRequest request) { /* ... */ }
    public async Task JoinGameAsync(JoinGameRequest request) { /* ... */ }
    public async Task LoadOpenGamesAsync() { /* ... */ }
    public async Task PlaceFleetAsync(PlaceFleetRequest request) { /* ... */ }
    public async Task FireAsync(CoordinateDto target) { /* ... */ }
    public async Task UsePowerAsync(PowerId id, PowerTargetDto target) { /* ... */ }
    public async Task ForfeitAsync() { /* ... */ }

    private void Notify() => StateChanged?.Invoke();
}
```

- Une méthode par intention utilisateur, jamais de méthode générique de type `Send(object)`.
- Chaque méthode est `async Task` (jamais `async void`), catch les échecs d'appel API, met à
  jour `LastError` et notifie dans tous les cas (succès ou échec).
- Les composants s'abonnent dans `OnInitialized` (`Store.StateChanged += OnStoreChanged`) et se
  désabonnent dans `Dispose` (implémentent `IDisposable`).
- Aucune règle de jeu dans le store : c'est de la coordination pure (appel API → mise à jour
  d'état → notification). La validation visuelle (ex. griser un bouton pouvoir) se fait en
  lisant des champs déjà calculés côté serveur (`PowerSlotDto.CanAffordNow`), jamais recalculée
  côté front.

## Composants

### `Components/Ds/` — zéro dépendance métier

Ne prennent en paramètre que des primitives ou des `RenderFragment`. Réutilisables tels quels
si le thème visuel change.

| Composant | Rôle |
|---|---|
| `DsConsole.razor` | Cadre + charnière, deux `RenderFragment` (`TopScreen`, `BottomScreen`) |
| `DsTopScreen.razor` / `DsBottomScreen.razor` | Encarts avec glow CSS |
| `DsButton.razor` | Bouton stylé — `Label`, `OnClick`, `Disabled` |
| `DsErrorBanner.razor` | Affiche un message d'erreur à partir d'un code métier traduit localement |

### `Components/Game/` — consomment des DTO en paramètres

Aucun de ces composants n'injecte `GameStateStore` : ils reçoivent des DTO en paramètre et
remontent les interactions via `EventCallback`.

| Composant | Paramètres clés | Rôle |
|---|---|---|
| `GridView.razor` | `BoardViewDto Board`, `EventCallback<CoordinateDto> OnCellClicked` | Itère `Board.Rows`, instancie une `GridCell` par caractère |
| `GridCell.razor` | `char State` | Mappe l'état vers une classe CSS (légende définie dans `BoardViewDto`) |
| `ShipTray.razor` | `IReadOnlyList<ShipStateDto> Fleet`, `EventCallback<ShipPlacementDto>` | Sélection/rotation de navire en déploiement (`Deploy` uniquement) |
| `PowerBar.razor` | `IReadOnlyList<PowerSlotDto> Powers`, `EventCallback<PowerId> OnPowerActivated` | Slots de pouvoirs équipés |
| `EnergyGauge.razor` | `int Energy` | Jauge d'énergie |
| `ChargeMeter.razor` | `OpponentChargeDto Charge` | Charge adverse, si révélée |
| `EventLog.razor` | `IReadOnlyList<GameEventDto> Events` | Journal chronologique |

### `Pages/`

Chaque page injecte `GameStateStore`, s'abonne à `StateChanged`, assemble les composants
`Ds/` + `Game/`. Toute interaction utilisateur remonte via `EventCallback` jusqu'à la page, qui
appelle une méthode du store — jamais l'inverse.

| Page | Rôle |
|---|---|
| `Index.razor` | Écran titre — "Nouvelle partie" / "Rejoindre" |
| `Lobby.razor` | Création (mode, grille, flotte, pouvoirs), salon public, saisie de code |
| `Deploy.razor` | `ShipTray` + `GridView` en mode placement |
| `Battle.razor` | Double `GridView` (soi / adversaire) + `PowerBar` + `EnergyGauge` + `EventLog` |
| `Result.razor` | `GameOverDto` — stats, vainqueur |

## Gestion des erreurs

- `FakeGameApiClient` simule un rejet en levant une `GameApiException(ApiProblemDto Problem)`.
- `GameStateStore` catch cette exception dans chaque méthode, peuple `LastError`, notifie.
- Les pages affichent `LastError` via `DsErrorBanner`, qui lit `Code` (jamais `Detail`, texte
  libre non fiable pour une logique d'affichage) et réagit selon la table `ErrorCodes` déjà
  définie dans `Naval.Shared.Contracts.Realtime`. Le composant traduit localement le code en
  message FR (ex. `NotYourTurn` → "Ce n'est pas votre tour").
- Pas de retry automatique dans ce cycle : un échec affiche l'erreur, l'utilisateur retente
  l'action manuellement.

## Testing

- `tests/Naval.Tests/App/GameStateStoreTests.cs` : chaque méthode du store met à jour l'état
  attendu et déclenche `StateChanged` exactement une fois par appel (succès ou échec) ;
  `LastError` se peuple correctement sur échec simulé.
- `tests/Naval.Tests/App/FakeGameApiClientTests.cs` : les données renvoyées respectent les
  invariants des DTO (aucune position de navire adverse non coulé dans `OpponentViewDto`,
  tailles de grille cohérentes avec le `FleetPreset` demandé, etc.).
- xUnit + FluentAssertions, cohérent avec le reste du projet.
- Composants Razor (`Ds/*`, `Game/*`, `Pages/*`) : vérification manuelle dans le navigateur
  (`dotnet run --project src/Naval.App`). Pas de bUnit dans ce cycle.
- `dotnet build` sans nouvel avertissement, `dotnet test` vert : condition de clôture de toute
  tâche de ce cycle.

## Répartition du travail (approche retenue)

**Étape 1 — socle, séquentiel, réalisé directement (pas par un subagent) :**
scaffold `Naval.App` (`dotnet new blazorwasm`, référence à `Naval.Shared`), vérification /
création de `Directory.Build.props` si absent, `wwwroot/css/tokens.css`, `IGameApiClient`,
`GameStateStore` (squelette complet + un cas d'usage implémenté, `CreateGameAsync`, pour valider
le pattern), `FakeGameApiClient` (données minimales couvrant `CreateGameAsync`),
`GameStateStoreTests` de base. Ce socle doit compiler et ses tests doivent passer avant toute
parallélisation.

**Étape 2 — fan-out parallèle, une fois le socle validé :**
- **Agent Ds** — `Components/Ds/*` (y compris `DsErrorBanner`) + CSS coque/tokens complémentaires
- **Agent Grid/Fleet** — `GridView`, `GridCell`, `ShipTray`
- **Agent Powers/HUD** — `PowerBar`, `EnergyGauge`, `ChargeMeter`, `EventLog`

Ces trois lots ne se recouvrent pas en fichiers et ne dépendent que de l'interface `IGameApiClient`
/ des DTO déjà figés — donc parallélisables sans risque de conflit.

**Étape 3 — assemblage, séquentiel, après le fan-out :**
complétion de `FakeGameApiClient` avec tous les scénarios nécessaires aux 5 écrans, complétion de
`GameStateStore` (toutes les méthodes), assemblage des `Pages/*`, complétion des tests du store.

## Conséquences

- Ce cycle ne livre pas d'animations ni de son : l'expérience sera visuellement "plate" à la fin
  de ce cycle, ce qui est assumé et volontaire (polish reporté).
- `GameApiClient` réel restant un squelette, le front ne sera pas testable end-to-end avec la
  vraie API tant que ce cycle seul est livré — dépendance explicite sur le travail du binôme
  moteur pour un test d'intégration complet.
- Le découpage en 3 lots parallèles à l'étape 2 suppose que l'interface `IGameApiClient` et les
  DTO ne changent plus pendant le fan-out — un changement de contrat pendant cette étape doit
  être communiqué à l'autre binôme et rediffusé aux agents en cours.
