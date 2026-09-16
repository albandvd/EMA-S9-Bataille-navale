# Naval.App — Cycle 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up `src/Naval.App` (Blazor WebAssembly) with its console shell, grid/fleet/HUD
components, and five screens (Index, Lobby, Deploy, Battle, Result), fully wired to a
`FakeGameApiClient` so the solo flow is clickable end-to-end without `Naval.Api` existing yet.

**Architecture:** `GameStateStore` is the single point of contact between UI and
`IGameApiClient`; `FakeGameApiClient` implements that interface with deterministic in-memory
data conforming to the DTOs already frozen in `Naval.Shared.Contracts`. Components under
`Components/Ds/` know nothing about the game; components under `Components/Game/` render DTOs
and bubble up `EventCallback`s; `Pages/*` are the only classes that touch `GameStateStore`.

**Tech Stack:** .NET 10 / Blazor WebAssembly, xUnit + FluentAssertions, plain CSS (no UI
framework), C# 12 primary constructors and record `with` expressions.

**Spec:** `docs/superpowers/specs/2026-09-15-frontend-naval-app-design.md`

## Global Constraints

- `Naval.App` never references `Naval.Api`.
- No game rules inside Razor components — they render state and emit intents.
- The power catalog is never hardcoded in the UI — it always comes from
  `IGameApiClient.GetPowerCatalogAsync` (backed by `GET /api/catalog/powers` once the real API
  exists).
- `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors` enabled repo-wide. No `async void`, no
  `.Result`/`.Wait()`.
- `GameStateStore` is the *only* class that calls `IGameApiClient` — no page or `Game/*`
  component injects `IGameApiClient` directly.
- `dotnet build` must produce zero new warnings and `dotnet test` must pass (green) before any
  task is considered done.
- `Components/Fx/`, `AudioService`, and the real `GameApiClient` HTTP implementation are out of
  scope for this cycle — do not build them here.

---

## Phase 0 — Socle (séquentiel)

### Task 1: Bootstrap de la solution et des projets

**Files:**
- Create: `Naval.sln`
- Create: `Directory.Build.props`
- Create: `src/Naval.App/` (scaffoldé par `dotnet new blazorwasm`)
- Create: `tests/Naval.Tests/` (scaffoldé par `dotnet new xunit`)

**Interfaces:**
- Consumes: rien (première tâche)
- Produces: solution `Naval.sln` contenant `Naval.Shared`, `Naval.App`, `Naval.Tests` ;
  `Naval.App` référence `Naval.Shared` ; `Naval.Tests` référence `Naval.Shared` et `Naval.App` ;
  `Naval.Tests` a le package `FluentAssertions`.

- [ ] **Step 1: Créer la solution et y ajouter Naval.Shared**

```bash
cd /home/haricotmer/EMA/3A/EMA-S9-Bataille-navale
dotnet new sln -n Naval
dotnet sln add src/Naval.Shared/Naval.Shared.csproj
```

- [ ] **Step 2: Scaffolder Naval.App et le référencer**

```bash
dotnet new blazorwasm -o src/Naval.App -n Naval.App
dotnet sln add src/Naval.App/Naval.App.csproj
dotnet add src/Naval.App/Naval.App.csproj reference src/Naval.Shared/Naval.Shared.csproj
```

- [ ] **Step 3: Scaffolder Naval.Tests et le référencer**

```bash
dotnet new xunit -o tests/Naval.Tests -n Naval.Tests
dotnet sln add tests/Naval.Tests/Naval.Tests.csproj
dotnet add tests/Naval.Tests/Naval.Tests.csproj reference src/Naval.Shared/Naval.Shared.csproj
dotnet add tests/Naval.Tests/Naval.Tests.csproj reference src/Naval.App/Naval.App.csproj
dotnet add tests/Naval.Tests/Naval.Tests.csproj package FluentAssertions
```

- [ ] **Step 4: Créer `Directory.Build.props`**

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

- [ ] **Step 5: Supprimer les pages d'exemple du scaffold Blazor**

```bash
rm src/Naval.App/Pages/Counter.razor src/Naval.App/Pages/Weather.razor
rm src/Naval.App/Pages/Home.razor
```

(Elles seront remplacées par nos propres pages dans les tâches suivantes ; on les retire tout de
suite pour ne pas laisser de routes mortes pendant les tâches intermédiaires.)

- [ ] **Step 6: Vérifier que tout compile**

Run: `dotnet build`
Expected: `Build succeeded.` — zéro avertissement, zéro erreur (les pages supprimées vont casser
`NavMenu.razor` si elle les référence : ouvrir `src/Naval.App/Layout/NavMenu.razor` et retirer
les liens vers `counter` et `weather` s'ils existent).

- [ ] **Step 7: Commit**

```bash
git add Naval.sln Directory.Build.props src/Naval.App tests/Naval.Tests
git commit -m "chore: bootstrap Naval.App et Naval.Tests"
```

---

### Task 2: Palette et tokens CSS

**Files:**
- Create: `src/Naval.App/wwwroot/css/tokens.css`
- Modify: `src/Naval.App/wwwroot/index.html`

**Interfaces:**
- Consumes: rien
- Produces: variables CSS `--sea-deep`, `--sea`, `--sea-light`, `--hull`, `--hull-dark`, `--hit`,
  `--sunk`, `--miss`, `--energy`, `--charge`, `--shell`, `--shell-light`, `--screen-glow`,
  `--text`, utilisables par toute future feuille de style du projet.

- [ ] **Step 1: Créer `tokens.css`**

```css
:root {
  --sea-deep:     #0b2545;
  --sea:          #13315c;
  --sea-light:    #1c4c7c;
  --hull:         #8d99ae;
  --hull-dark:    #5c677d;
  --hit:          #ef233c;
  --sunk:         #6a040f;
  --miss:         #adb5bd;
  --energy:       #ffd166;
  --charge:       #06d6a0;
  --shell:        #2b2d42;
  --shell-light:  #3d405b;
  --screen-glow:  #8ecae6;
  --text:         #edf2f4;
}

body {
  background: var(--shell);
  color: var(--text);
  font-family: 'Silkscreen', 'VT323', monospace;
}
```

- [ ] **Step 2: Lier la feuille de style dans `index.html`**

Ouvrir `src/Naval.App/wwwroot/index.html` et ajouter, avant la balise `<link>` existante vers
`css/app.css` :

```html
<link rel="stylesheet" href="css/tokens.css" />
```

- [ ] **Step 3: Vérifier que le build passe toujours**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/Naval.App/wwwroot/css/tokens.css src/Naval.App/wwwroot/index.html
git commit -m "feat: palette de tokens CSS"
```

---

### Task 3: `IGameApiClient`, `GameApiException`, squelette `GameApiClient`

**Files:**
- Create: `src/Naval.App/Services/IGameApiClient.cs`
- Create: `src/Naval.App/Services/GameApiException.cs`
- Create: `src/Naval.App/Services/GameApiClient.cs`

**Interfaces:**
- Consumes: DTO de `Naval.Shared.Contracts` (`CreateGameRequest`, `CreateGameResponse`,
  `JoinGameRequest`, `JoinGameResponse`, `OpenGameDto`, `GameStateDto`, `PlaceFleetRequest`,
  `FireRequest`, `ShotResultDto`, `UsePowerRequest`, `PowerResultDto`, `PowerDefinitionDto`,
  `ApiProblemDto`)
- Produces: interface `IGameApiClient` avec 9 méthodes async, exception `GameApiException` avec
  propriété `Problem` de type `ApiProblemDto`. Toute tâche suivante qui appelle l'API passe par
  cette interface.

- [ ] **Step 1: Créer `GameApiException`**

```csharp
namespace Naval.App.Services;

using Naval.Shared.Contracts;

/// <summary>Levée par une implémentation de IGameApiClient quand le serveur refuse une action.</summary>
public sealed class GameApiException(ApiProblemDto problem) : Exception(problem.Detail)
{
    public ApiProblemDto Problem { get; } = problem;
}
```

- [ ] **Step 2: Créer `IGameApiClient`**

```csharp
namespace Naval.App.Services;

using Naval.Shared.Contracts;

/// <summary>
/// Abstraction du contrat REST. GameStateStore est le seul consommateur de cette interface.
/// </summary>
public interface IGameApiClient
{
    Task<CreateGameResponse> CreateGameAsync(CreateGameRequest request, CancellationToken ct);
    Task<JoinGameResponse> JoinGameAsync(JoinGameRequest request, CancellationToken ct);
    Task<IReadOnlyList<OpenGameDto>> ListOpenGamesAsync(CancellationToken ct);
    Task<GameStateDto> GetGameAsync(Guid gameId, string playerToken, CancellationToken ct);
    Task<GameStateDto> PlaceFleetAsync(Guid gameId, string playerToken, PlaceFleetRequest request, CancellationToken ct);
    Task<ShotResultDto> FireAsync(Guid gameId, string playerToken, FireRequest request, CancellationToken ct);
    Task<PowerResultDto> UsePowerAsync(Guid gameId, string playerToken, UsePowerRequest request, CancellationToken ct);
    Task<GameStateDto> ForfeitAsync(Guid gameId, string playerToken, CancellationToken ct);
    Task<IReadOnlyList<PowerDefinitionDto>> GetPowerCatalogAsync(CancellationToken ct);
}
```

- [ ] **Step 3: Créer le squelette `GameApiClient` (non branché ce cycle)**

```csharp
namespace Naval.App.Services;

using Naval.Shared.Contracts;

/// <summary>
/// Implémentation HTTP réelle. Pas encore branchée dans Program.cs : Naval.Api n'existe pas
/// encore côté binôme moteur. À compléter dans un cycle ultérieur.
/// </summary>
public sealed class GameApiClient : IGameApiClient
{
    public Task<CreateGameResponse> CreateGameAsync(CreateGameRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<JoinGameResponse> JoinGameAsync(JoinGameRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<OpenGameDto>> ListOpenGamesAsync(CancellationToken ct) => throw new NotImplementedException();
    public Task<GameStateDto> GetGameAsync(Guid gameId, string playerToken, CancellationToken ct) => throw new NotImplementedException();
    public Task<GameStateDto> PlaceFleetAsync(Guid gameId, string playerToken, PlaceFleetRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<ShotResultDto> FireAsync(Guid gameId, string playerToken, FireRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<PowerResultDto> UsePowerAsync(Guid gameId, string playerToken, UsePowerRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<GameStateDto> ForfeitAsync(Guid gameId, string playerToken, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<PowerDefinitionDto>> GetPowerCatalogAsync(CancellationToken ct) => throw new NotImplementedException();
}
```

- [ ] **Step 4: Vérifier que le build passe**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/Naval.App/Services/IGameApiClient.cs src/Naval.App/Services/GameApiException.cs src/Naval.App/Services/GameApiClient.cs
git commit -m "feat: interface IGameApiClient et squelette GameApiClient"
```

---

### Task 4: `GameStateStore` + `FakeGameApiClient` (cas `CreateGame`) + tests

**Files:**
- Create: `src/Naval.App/Services/GameStateStore.cs`
- Create: `src/Naval.App/Services/FakeGameApiClient.cs`
- Test: `tests/Naval.Tests/App/GameStateStoreTests.cs`
- Test: `tests/Naval.Tests/App/FakeGameApiClientTests.cs`

**Interfaces:**
- Consumes: `IGameApiClient`, `GameApiException` (Task 3)
- Produces: `GameStateStore(IGameApiClient api)` avec `event Action? StateChanged`,
  `GameStateDto? CurrentGame`, `ApiProblemDto? LastError`, `Task CreateGameAsync(CreateGameRequest)`.
  `FakeGameApiClient` implémentant `IGameApiClient` (les 7 autres méthodes lèvent
  `NotImplementedException` jusqu'à la Task 10).

- [ ] **Step 1: Écrire le test `GameStateStoreTests` (échoue, la classe n'existe pas)**

```csharp
namespace Naval.Tests.App;

using FluentAssertions;
using Naval.App.Services;
using Naval.Shared.Contracts;
using Xunit;

public class GameStateStoreTests
{
    [Fact]
    public async Task CreateGameAsync_sets_current_game_and_notifies_once()
    {
        var store = new GameStateStore(new FakeGameApiClient());
        var notifications = 0;
        store.StateChanged += () => notifications++;

        var request = new CreateGameRequest("Joueur", GameMode.SinglePlayer, 10, 10, "Classic", AiLevel.Random, [], 0, null);
        await store.CreateGameAsync(request);

        store.CurrentGame.Should().NotBeNull();
        store.CurrentGame!.Status.Should().Be(GameStatus.AwaitingDeployment);
        store.LastError.Should().BeNull();
        notifications.Should().Be(1);
    }
}
```

- [ ] **Step 2: Run pour vérifier l'échec**

Run: `dotnet test --filter FullyQualifiedName~GameStateStoreTests`
Expected: FAIL — `GameStateStore` et `FakeGameApiClient` n'existent pas encore (erreur de
compilation).

- [ ] **Step 3: Écrire `FakeGameApiClient` (implémentation minimale)**

```csharp
namespace Naval.App.Services;

using Naval.Shared.Contracts;

public sealed class FakeGameApiClient : IGameApiClient
{
    private const string PlayerToken = "fake-player-token";
    private GameStateDto? _game;

    public Task<CreateGameResponse> CreateGameAsync(CreateGameRequest request, CancellationToken ct)
    {
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var opponentId = Guid.NewGuid();

        var emptyRows = Enumerable.Repeat(new string('.', request.GridWidth), request.GridHeight).ToList();
        var selfBoard = new BoardViewDto(request.GridWidth, request.GridHeight, emptyRows);
        var opponentBoard = new BoardViewDto(request.GridWidth, request.GridHeight, emptyRows);

        var powers = request.Powers
            .Select(id => new PowerSlotDto(id, PowerSlotStatus.Ready, 0, 0, -1, true))
            .ToList();

        var self = new SelfViewDto(playerId, request.PlayerName, PlayerSlot.One, 0, selfBoard, [], powers);
        var opponent = new OpponentViewDto(opponentId, "IA", true, true, 0, 0, 0, opponentBoard,
            [], new OpponentChargeDto(false, null, null, null), []);

        _game = new GameStateDto(gameId, request.Mode, GameStatus.AwaitingDeployment, null, 0,
            null, null, null, self, opponent, [], 0);

        return Task.FromResult(new CreateGameResponse(gameId, PlayerToken, null, GameStatus.AwaitingDeployment));
    }

    public Task<GameStateDto> GetGameAsync(Guid gameId, string playerToken, CancellationToken ct)
    {
        EnsureGame(gameId);
        return Task.FromResult(_game!);
    }

    private void EnsureGame(Guid gameId)
    {
        if (_game is null || _game.GameId != gameId)
        {
            throw new GameApiException(new ApiProblemDto(
                "about:blank", "Partie introuvable", 404,
                "Aucune partie active ne correspond à cet identifiant.",
                ErrorCodes.GameNotFound, null));
        }
    }

    public Task<JoinGameResponse> JoinGameAsync(JoinGameRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<OpenGameDto>> ListOpenGamesAsync(CancellationToken ct) => throw new NotImplementedException();
    public Task<GameStateDto> PlaceFleetAsync(Guid gameId, string playerToken, PlaceFleetRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<ShotResultDto> FireAsync(Guid gameId, string playerToken, FireRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<PowerResultDto> UsePowerAsync(Guid gameId, string playerToken, UsePowerRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<GameStateDto> ForfeitAsync(Guid gameId, string playerToken, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<PowerDefinitionDto>> GetPowerCatalogAsync(CancellationToken ct) => throw new NotImplementedException();
}
```

- [ ] **Step 4: Écrire `GameStateStore` (implémentation minimale)**

```csharp
namespace Naval.App.Services;

using Naval.Shared.Contracts;

public sealed class GameStateStore(IGameApiClient api)
{
    public event Action? StateChanged;

    public GameStateDto? CurrentGame { get; private set; }
    public ApiProblemDto? LastError { get; private set; }

    private string _playerToken = string.Empty;

    public Task CreateGameAsync(CreateGameRequest request) => RunAsync(async () =>
    {
        var response = await api.CreateGameAsync(request, CancellationToken.None);
        _playerToken = response.PlayerToken;
        CurrentGame = await api.GetGameAsync(response.GameId, response.PlayerToken, CancellationToken.None);
    });

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
            LastError = null;
        }
        catch (GameApiException ex)
        {
            LastError = ex.Problem;
        }
        finally
        {
            StateChanged?.Invoke();
        }
    }
}
```

- [ ] **Step 5: Run pour vérifier que le test passe**

Run: `dotnet test --filter FullyQualifiedName~GameStateStoreTests`
Expected: PASS

- [ ] **Step 6: Écrire `FakeGameApiClientTests`**

```csharp
namespace Naval.Tests.App;

using FluentAssertions;
using Naval.App.Services;
using Naval.Shared.Contracts;
using Xunit;

public class FakeGameApiClientTests
{
    [Fact]
    public async Task CreateGameAsync_returns_board_matching_requested_grid_size()
    {
        var client = new FakeGameApiClient();
        var request = new CreateGameRequest("Joueur", GameMode.SinglePlayer, 12, 8, "Classic", AiLevel.Random, [], 0, null);

        var response = await client.CreateGameAsync(request, CancellationToken.None);
        var game = await client.GetGameAsync(response.GameId, response.PlayerToken, CancellationToken.None);

        game.Self.Board.Width.Should().Be(12);
        game.Self.Board.Height.Should().Be(8);
        game.Self.Board.Rows.Should().HaveCount(8);
        game.Opponent.TargetBoard.Rows.Should().OnlyContain(row => row.All(c => c == '.'));
    }
}
```

- [ ] **Step 7: Run pour vérifier que tout passe**

Run: `dotnet test`
Expected: PASS (tous les tests, y compris ceux de `Naval.Shared`)

- [ ] **Step 8: Commit**

```bash
git add src/Naval.App/Services/GameStateStore.cs src/Naval.App/Services/FakeGameApiClient.cs tests/Naval.Tests/App
git commit -m "feat: GameStateStore et FakeGameApiClient (CreateGame)"
```

---

### Task 5: Câblage `Program.cs` + `_Imports.razor` + vérification de démarrage

**Files:**
- Modify: `src/Naval.App/Program.cs`
- Modify: `src/Naval.App/_Imports.razor`

**Interfaces:**
- Consumes: `IGameApiClient`, `FakeGameApiClient`, `GameStateStore` (Task 3, 4)
- Produces: application qui démarre (`dotnet run`) avec `GameStateStore` et `FakeGameApiClient`
  disponibles par injection de dépendances dans toute page/composant.

- [ ] **Step 1: Ajouter les enregistrements DI dans `Program.cs`**

Ouvrir `src/Naval.App/Program.cs` et ajouter avant `await builder.Build().RunAsync();` :

```csharp
builder.Services.AddScoped<IGameApiClient, FakeGameApiClient>();
builder.Services.AddScoped<GameStateStore>();
```

- [ ] **Step 2: Ajouter les usings globaux dans `_Imports.razor`**

Ajouter à la fin du fichier :

```razor
@using Naval.App.Components.Ds
@using Naval.App.Components.Game
@using Naval.App.Services
@using Naval.Shared.Contracts
```

- [ ] **Step 3: Vérifier que le build passe**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 4: Vérifier que l'application démarre**

Run: `dotnet run --project src/Naval.App` (dans un terminal séparé, puis arrêter avec Ctrl+C)
Expected: le serveur de dev démarre sans exception sur `https://localhost:7002` (ou le port
affiché). Ouvrir l'URL dans un navigateur : la page par défaut du scaffold s'affiche sans erreur
console — c'est la dernière fois qu'on voit le scaffold par défaut, il sera remplacé dans les
tâches d'assemblage.

- [ ] **Step 5: Commit**

```bash
git add src/Naval.App/Program.cs src/Naval.App/_Imports.razor
git commit -m "chore: câblage DI de GameStateStore et FakeGameApiClient"
```

---

## Phase 1 — Fan-out parallèle (3 lots indépendants, aucun fichier partagé)

Ces trois tâches ne dépendent que de Task 1 (le projet compile) et de Task 5 (les usings
globaux). Elles ne se touchent pas entre elles et peuvent être exécutées par trois subagents en
parallèle. Aucune n'a de test automatisé (vérification manuelle dans le navigateur, comme
convenu dans la spec) — chaque tâche se termine par un `dotnet build` propre et un commit.

### Task 6: Composants `Ds/` (coque de console + bannière d'erreur)

**Files:**
- Create: `src/Naval.App/Components/Ds/DsConsole.razor`
- Create: `src/Naval.App/Components/Ds/DsTopScreen.razor`
- Create: `src/Naval.App/Components/Ds/DsBottomScreen.razor`
- Create: `src/Naval.App/Components/Ds/DsButton.razor`
- Create: `src/Naval.App/Components/Ds/DsErrorBanner.razor`
- Create: `src/Naval.App/wwwroot/css/console.css`
- Modify: `src/Naval.App/wwwroot/index.html`

**Interfaces:**
- Consumes: `ApiProblemDto` et `ErrorCodes` (déjà dans `Naval.Shared.Contracts`)
- Produces: `<DsConsole TopScreen="..." BottomScreen="..." />`,
  `<DsTopScreen>`/`<DsBottomScreen>` (ChildContent), `<DsButton Label OnClick Disabled />`,
  `<DsErrorBanner Problem="ApiProblemDto?" />` — utilisés par toutes les Pages en Phase 2.

- [ ] **Step 1: `DsTopScreen.razor` / `DsBottomScreen.razor`**

```razor
<div class="ds-screen ds-screen--top">
    @ChildContent
</div>

@code {
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
```

Créer `DsBottomScreen.razor` à l'identique avec la classe `ds-screen--bottom`.

- [ ] **Step 2: `DsConsole.razor`**

```razor
<div class="ds-console">
    <div class="ds-console__hinge"></div>
    @TopScreen
    @BottomScreen
</div>

@code {
    [Parameter] public RenderFragment? TopScreen { get; set; }
    [Parameter] public RenderFragment? BottomScreen { get; set; }
}
```

- [ ] **Step 3: `DsButton.razor`**

```razor
<button class="ds-button" disabled="@Disabled" @onclick="OnClick">
    @Label
</button>

@code {
    [Parameter, EditorRequired] public string Label { get; set; } = string.Empty;
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public EventCallback OnClick { get; set; }
}
```

- [ ] **Step 4: `DsErrorBanner.razor`**

```razor
@if (Problem is not null)
{
    <div class="ds-error-banner" role="alert">
        @GetMessage(Problem.Code)
    </div>
}

@code {
    [Parameter] public ApiProblemDto? Problem { get; set; }

    private static string GetMessage(string code) => code switch
    {
        ErrorCodes.NotYourTurn => "Ce n'est pas votre tour.",
        ErrorCodes.CellAlreadyTargeted => "Cette case a déjà été visée.",
        ErrorCodes.InsufficientEnergy => "Énergie insuffisante pour ce pouvoir.",
        ErrorCodes.PowerOnCooldown => "Ce pouvoir est en recharge.",
        ErrorCodes.PowerExhausted => "Ce pouvoir n'a plus de charge disponible.",
        ErrorCodes.InvalidTarget => "Cible invalide pour cette action.",
        ErrorCodes.CarrierRequired => "Ce pouvoir nécessite un navire porteur.",
        ErrorCodes.GameNotInProgress => "La partie n'est pas en cours.",
        ErrorCodes.GameNotFound => "Cette partie n'existe pas ou plus.",
        _ => "Une erreur est survenue."
    };
}
```

- [ ] **Step 5: `console.css` (coque, cent pour cent CSS)**

```css
.ds-console {
    display: flex;
    flex-direction: column;
    gap: 12px;
    padding: 16px;
    background: var(--shell);
    border-radius: 24px;
    box-shadow: inset 0 0 12px rgba(0, 0, 0, .5);
}

.ds-console__hinge {
    height: 24px;
    background: repeating-linear-gradient(90deg, var(--shell-light) 0 4px, var(--shell) 4px 8px);
    border-radius: 4px;
}

.ds-screen {
    background: var(--sea-deep);
    border-radius: 12px;
    padding: 12px;
    box-shadow: inset 0 0 8px rgba(0, 0, 0, .6);
}

.ds-button {
    background: var(--hull);
    color: var(--shell);
    border: none;
    border-radius: 999px;
    padding: 8px 16px;
    font-family: inherit;
    cursor: pointer;
}

.ds-button:disabled {
    background: var(--hull-dark);
    cursor: not-allowed;
}

.ds-error-banner {
    background: var(--hit);
    color: var(--text);
    padding: 8px 12px;
    border-radius: 8px;
}
```

Lier la feuille dans `index.html`, après `tokens.css` :

```html
<link rel="stylesheet" href="css/console.css" />
```

- [ ] **Step 6: Vérifier que le build passe**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 7: Commit**

```bash
git add src/Naval.App/Components/Ds src/Naval.App/wwwroot/css/console.css src/Naval.App/wwwroot/index.html
git commit -m "feat: composants Ds (coque de console)"
```

---

### Task 7: `GridView` + `GridCell`

**Files:**
- Create: `src/Naval.App/Components/Game/GridCell.razor`
- Create: `src/Naval.App/Components/Game/GridView.razor`
- Create: `src/Naval.App/wwwroot/css/grid.css`
- Modify: `src/Naval.App/wwwroot/index.html`

**Interfaces:**
- Consumes: `BoardViewDto`, `CoordinateDto` (`Naval.Shared.Contracts`)
- Produces: `<GridView Board="BoardViewDto" OnCellClicked="EventCallback<CoordinateDto>" />`,
  utilisé par `Deploy.razor` et `Battle.razor` en Phase 2.

- [ ] **Step 1: `GridCell.razor`**

```razor
<div class="grid-cell @CssClass" @onclick="HandleClick" role="button" aria-label="@AriaLabel">
</div>

@code {
    [Parameter] public char State { get; set; }
    [Parameter] public EventCallback OnClick { get; set; }

    private string CssClass => State switch
    {
        '.' => "grid-cell--unknown",
        'o' => "grid-cell--miss",
        'x' => "grid-cell--hit",
        '#' => "grid-cell--sunk",
        '~' => "grid-cell--revealed-empty",
        '?' => "grid-cell--revealed-occupied",
        'f' => "grid-cell--fogged",
        'S' => "grid-cell--ship",
        'X' => "grid-cell--ship-hit",
        'D' => "grid-cell--decoy",
        'M' => "grid-cell--mine",
        'B' => "grid-cell--shielded",
        _ => "grid-cell--unknown"
    };

    private string AriaLabel => CssClass.Replace("grid-cell--", string.Empty).Replace('-', ' ');

    private Task HandleClick() => OnClick.HasDelegate ? OnClick.InvokeAsync() : Task.CompletedTask;
}
```

- [ ] **Step 2: `GridView.razor`**

```razor
<div class="grid-view" style="grid-template-columns: repeat(@Board.Width, 1fr);">
    @for (var y = 0; y < Board.Rows.Count; y++)
    {
        var row = Board.Rows[y];
        for (var x = 0; x < row.Length; x++)
        {
            var coordinate = new CoordinateDto(x, y);
            var state = row[x];
            <GridCell State="state" OnClick="() => HandleCellClicked(coordinate)" />
        }
    }
</div>

@code {
    [Parameter, EditorRequired] public BoardViewDto Board { get; set; } = null!;
    [Parameter] public EventCallback<CoordinateDto> OnCellClicked { get; set; }

    private Task HandleCellClicked(CoordinateDto coordinate) =>
        OnCellClicked.HasDelegate ? OnCellClicked.InvokeAsync(coordinate) : Task.CompletedTask;
}
```

- [ ] **Step 3: `grid.css`**

```css
.grid-view {
    display: grid;
    gap: 2px;
}

.grid-cell {
    aspect-ratio: 1;
    border: 1px solid rgba(255, 255, 255, .06);
    cursor: pointer;
}

.grid-cell--unknown { background: var(--sea-deep); }
.grid-cell--miss { background: var(--sea); }
.grid-cell--hit { background: var(--hit); }
.grid-cell--sunk { background: var(--sunk); }
.grid-cell--revealed-empty { background: var(--sea-light); }
.grid-cell--revealed-occupied { outline: 2px solid var(--screen-glow); }
.grid-cell--fogged { backdrop-filter: blur(3px); background: rgba(255, 255, 255, .1); }
.grid-cell--ship { background: var(--hull); }
.grid-cell--ship-hit { background: var(--hit); }
.grid-cell--decoy { background: var(--hull-dark); opacity: .6; }
.grid-cell--mine { background: var(--energy); }
.grid-cell--shielded { outline: 2px solid var(--charge); }
```

Lier dans `index.html`, après `console.css` :

```html
<link rel="stylesheet" href="css/grid.css" />
```

- [ ] **Step 4: Vérifier que le build passe**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/Naval.App/Components/Game/GridCell.razor src/Naval.App/Components/Game/GridView.razor src/Naval.App/wwwroot/css/grid.css src/Naval.App/wwwroot/index.html
git commit -m "feat: composants GridView et GridCell"
```

---

### Task 8: `ShipTray`

**Files:**
- Create: `src/Naval.App/Components/Game/ShipTray.razor`

**Interfaces:**
- Consumes: `ShipStateDto`, `ShipPlacementDto`, `Orientation`, `CoordinateDto`
- Produces: `<ShipTray @ref Fleet="IReadOnlyList<ShipStateDto>" OnShipPlaced="EventCallback<ShipPlacementDto>" />`
  avec méthode publique `Task PlaceSelectedAtAsync(CoordinateDto origin)`, appelée par
  `Deploy.razor` (Phase 2) quand l'utilisateur clique une case de la grille.

- [ ] **Step 1: `ShipTray.razor`**

```razor
<div class="ship-tray">
    @foreach (var ship in Fleet)
    {
        <div class="ship-tray__item @(ship == _selected ? "ship-tray__item--selected" : "")"
             @onclick="() => Select(ship)">
            @ship.Type (@ship.Size)
        </div>
    }
    <button class="ship-tray__rotate" @onclick="ToggleOrientation">
        Orientation : @_orientation
    </button>
</div>

@code {
    [Parameter, EditorRequired] public IReadOnlyList<ShipStateDto> Fleet { get; set; } = [];
    [Parameter] public EventCallback<ShipPlacementDto> OnShipPlaced { get; set; }

    private ShipStateDto? _selected;
    private Orientation _orientation = Orientation.Horizontal;

    private void Select(ShipStateDto ship) => _selected = ship;

    private void ToggleOrientation() =>
        _orientation = _orientation == Orientation.Horizontal ? Orientation.Vertical : Orientation.Horizontal;

    public Task PlaceSelectedAtAsync(CoordinateDto origin)
    {
        if (_selected is null)
        {
            return Task.CompletedTask;
        }

        var placement = new ShipPlacementDto(_selected.Type, origin, _orientation);
        _selected = null;
        return OnShipPlaced.HasDelegate ? OnShipPlaced.InvokeAsync(placement) : Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Vérifier que le build passe**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/Naval.App/Components/Game/ShipTray.razor
git commit -m "feat: composant ShipTray"
```

---

### Task 9: HUD — `EnergyGauge`, `ChargeMeter`, `PowerBar`, `EventLog`

**Files:**
- Create: `src/Naval.App/Components/Game/EnergyGauge.razor`
- Create: `src/Naval.App/Components/Game/ChargeMeter.razor`
- Create: `src/Naval.App/Components/Game/PowerBar.razor`
- Create: `src/Naval.App/Components/Game/EventLog.razor`

**Interfaces:**
- Consumes: `PowerSlotDto`, `OpponentChargeDto`, `GameEventDto`, `PowerId`, `PowerSlotStatus`
- Produces: `<EnergyGauge Energy="int" />`, `<ChargeMeter Charge="OpponentChargeDto" />`,
  `<PowerBar Powers="IReadOnlyList<PowerSlotDto>" OnPowerActivated="EventCallback<PowerId>" />`,
  `<EventLog Events="IReadOnlyList<GameEventDto>" />`, utilisés par `Battle.razor` (Phase 2).

- [ ] **Step 1: `EnergyGauge.razor`**

```razor
<div class="energy-gauge" aria-label="Énergie : @Energy">
    <span class="energy-gauge__value">@Energy</span>
</div>

@code {
    [Parameter] public int Energy { get; set; }
}
```

- [ ] **Step 2: `ChargeMeter.razor`**

```razor
@if (Charge.IsCharging)
{
    <div class="charge-meter" role="status">
        @if (Charge.RevealedPowerId is { } powerId)
        {
            <span>Pouvoir adverse : @powerId (@Charge.TurnsRemaining tours)</span>
        }
        else
        {
            <span>L'adversaire charge un pouvoir inconnu (@Charge.TurnsRemaining tours)</span>
        }
        @if (Charge.RevealedColumn is { } column)
        {
            <span>Colonne révélée : @column</span>
        }
    </div>
}

@code {
    [Parameter, EditorRequired] public OpponentChargeDto Charge { get; set; } = null!;
}
```

- [ ] **Step 3: `PowerBar.razor`**

```razor
<div class="power-bar">
    @foreach (var slot in Powers)
    {
        <button class="power-bar__slot power-bar__slot--@slot.Status.ToString().ToLowerInvariant()"
                disabled="@(!slot.CanAffordNow || slot.Status != PowerSlotStatus.Ready)"
                @onclick="() => Activate(slot.PowerId)">
            <span>@slot.PowerId</span>
            @if (slot.Status == PowerSlotStatus.Charging)
            {
                <span>Charge : @slot.ChargeRemaining</span>
            }
            @if (slot.CooldownRemaining > 0)
            {
                <span>CD : @slot.CooldownRemaining</span>
            }
            @if (slot.UsesLeft >= 0)
            {
                <span>x@slot.UsesLeft</span>
            }
        </button>
    }
</div>

@code {
    [Parameter, EditorRequired] public IReadOnlyList<PowerSlotDto> Powers { get; set; } = [];
    [Parameter] public EventCallback<PowerId> OnPowerActivated { get; set; }

    private Task Activate(PowerId id) =>
        OnPowerActivated.HasDelegate ? OnPowerActivated.InvokeAsync(id) : Task.CompletedTask;
}
```

- [ ] **Step 4: `EventLog.razor`**

```razor
<ul class="event-log">
    @foreach (var evt in Events.OrderByDescending(e => e.Sequence))
    {
        <li class="event-log__entry">
            <span class="event-log__time">@evt.AtUtc.ToLocalTime().ToString("HH:mm:ss")</span>
            <span class="event-log__message">@evt.Message</span>
        </li>
    }
</ul>

@code {
    [Parameter, EditorRequired] public IReadOnlyList<GameEventDto> Events { get; set; } = [];
}
```

- [ ] **Step 5: Vérifier que le build passe**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add src/Naval.App/Components/Game/EnergyGauge.razor src/Naval.App/Components/Game/ChargeMeter.razor src/Naval.App/Components/Game/PowerBar.razor src/Naval.App/Components/Game/EventLog.razor
git commit -m "feat: composants HUD (energie, charge, pouvoirs, journal)"
```

---

## Phase 2 — Assemblage (séquentiel)

### Task 10: Compléter `GameStateStore` et `FakeGameApiClient`

**Files:**
- Modify: `src/Naval.App/Services/GameStateStore.cs`
- Modify: `src/Naval.App/Services/FakeGameApiClient.cs`
- Test: `tests/Naval.Tests/App/GameStateStoreTests.cs`
- Test: `tests/Naval.Tests/App/FakeGameApiClientTests.cs`

**Interfaces:**
- Consumes: `IGameApiClient` (Task 3), `GameStateStore`/`FakeGameApiClient` existants (Task 4)
- Produces: `GameStateStore` avec `OpenGames`, `PowerCatalog`,
  `LoadOpenGamesAsync()`, `LoadPowerCatalogAsync()`, `PlaceFleetAsync(PlaceFleetRequest)`,
  `FireAsync(CoordinateDto)`, `UsePowerAsync(PowerId, PowerTargetDto)`, `ForfeitAsync()` —
  utilisés par toutes les Pages restantes.

- [ ] **Step 1: Écrire le test `FireAsync_only_reveals_the_targeted_cell` (échoue)**

Ajouter à `FakeGameApiClientTests.cs` :

```csharp
    [Fact]
    public async Task FireAsync_only_reveals_the_targeted_cell()
    {
        var client = new FakeGameApiClient();
        var createRequest = new CreateGameRequest("Joueur", GameMode.SinglePlayer, 5, 5, "Skirmish", AiLevel.Random, [], 0, null);
        var response = await client.CreateGameAsync(createRequest, CancellationToken.None);
        await client.PlaceFleetAsync(response.GameId, response.PlayerToken,
            new PlaceFleetRequest([new ShipPlacementDto(ShipType.Destroyer, new CoordinateDto(0, 0), Orientation.Horizontal)]),
            CancellationToken.None);

        await client.FireAsync(response.GameId, response.PlayerToken, new FireRequest(new CoordinateDto(2, 2)), CancellationToken.None);
        var game = await client.GetGameAsync(response.GameId, response.PlayerToken, CancellationToken.None);

        var touchedCells = game.Opponent.TargetBoard.Rows
            .SelectMany((row, y) => row.Select((c, x) => (x, y, c)))
            .Count(cell => cell.c != '.');

        touchedCells.Should().Be(1);
    }

    [Fact]
    public async Task GetPowerCatalogAsync_returns_unique_power_definitions()
    {
        var client = new FakeGameApiClient();

        var catalog = await client.GetPowerCatalogAsync(CancellationToken.None);

        catalog.Should().NotBeEmpty();
        catalog.Should().OnlyHaveUniqueItems(p => p.Id);
    }
```

- [ ] **Step 2: Run pour vérifier l'échec**

Run: `dotnet test --filter FullyQualifiedName~FakeGameApiClientTests`
Expected: FAIL — `PlaceFleetAsync`, `FireAsync`, `GetPowerCatalogAsync` lèvent
`NotImplementedException`.

- [ ] **Step 3: Compléter `FakeGameApiClient`**

Remplacer les 7 méthodes encore en `NotImplementedException` (garder `CreateGameAsync` et
`GetGameAsync` inchangées) :

```csharp
    private static readonly IReadOnlyList<PowerDefinitionDto> Catalog =
    [
        new(PowerId.Sonar, "Sonar", PowerCategory.Recon,
            "Renvoie le nombre de cases occupées dans un disque de rayon 4.",
            3, 2, 3, -1, TargetKind.Cell, 4, false, "sonar"),
        new(PowerId.TripleSalvo, "Salve triple", PowerCategory.Offense,
            "3 tirs consécutifs alignés.",
            4, 0, 3, -1, TargetKind.Line, null, false, "triple-salvo")
    ];

    private int _shotCounter;

    public Task<JoinGameResponse> JoinGameAsync(JoinGameRequest request, CancellationToken ct) =>
        throw new GameApiException(new ApiProblemDto(
            "about:blank", "Non disponible", 501,
            "Le multijoueur n'est pas encore simulé par le client de test.",
            ErrorCodes.GameNotFound, null));

    public Task<IReadOnlyList<OpenGameDto>> ListOpenGamesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OpenGameDto>>([]);

    public Task<GameStateDto> PlaceFleetAsync(Guid gameId, string playerToken, PlaceFleetRequest request, CancellationToken ct)
    {
        EnsureGame(gameId);

        var fleet = request.Ships
            .Select((ship, i) => new ShipStateDto(
                $"ship-{i}", ship.Type, ShipSize(ship.Type), 0, false,
                Cells(ship.Origin, ship.Orientation, ShipSize(ship.Type))))
            .ToList();

        var self = _game!.Self with { Fleet = fleet };
        _game = _game with
        {
            Status = GameStatus.InProgress,
            CurrentPlayerId = self.PlayerId,
            TurnNumber = 1,
            Self = self
        };

        return Task.FromResult(_game);
    }

    public Task<ShotResultDto> FireAsync(Guid gameId, string playerToken, FireRequest request, CancellationToken ct)
    {
        EnsureGame(gameId);

        _shotCounter++;
        var outcome = _shotCounter % 3 == 0 ? ShotOutcome.Hit : ShotOutcome.Miss;
        var symbol = outcome == ShotOutcome.Hit ? 'x' : 'o';

        var rows = _game!.Opponent.TargetBoard.Rows.ToList();
        var chars = rows[request.Target.Y].ToCharArray();
        chars[request.Target.X] = symbol;
        rows[request.Target.Y] = new string(chars);

        var opponent = _game.Opponent with { TargetBoard = _game.Opponent.TargetBoard with { Rows = rows } };
        _game = _game with { Opponent = opponent, TurnNumber = _game.TurnNumber + 1 };

        return Task.FromResult(new ShotResultDto(
            gameId, _game.TurnNumber, _game.Self.PlayerId, request.Target, outcome,
            null, outcome == ShotOutcome.Hit ? 2 : 0, _game.Self.PlayerId, false, null));
    }

    public Task<PowerResultDto> UsePowerAsync(Guid gameId, string playerToken, UsePowerRequest request, CancellationToken ct)
    {
        EnsureGame(gameId);

        return Task.FromResult(new PowerResultDto(
            gameId, _game!.TurnNumber, request.PowerId, false, 0, 3, _game.Self.Energy,
            [], 2, [], "Sonar : 2 cases occupées détectées.", _game.Self.PlayerId, false, null));
    }

    public Task<GameStateDto> ForfeitAsync(Guid gameId, string playerToken, CancellationToken ct)
    {
        EnsureGame(gameId);
        _game = _game! with { Status = GameStatus.Abandoned, WinnerId = _game.Opponent.PlayerId };
        return Task.FromResult(_game);
    }

    public Task<IReadOnlyList<PowerDefinitionDto>> GetPowerCatalogAsync(CancellationToken ct) =>
        Task.FromResult(Catalog);

    private static int ShipSize(ShipType type) => type switch
    {
        ShipType.Carrier => 5,
        ShipType.Battleship => 4,
        ShipType.Cruiser => 3,
        ShipType.Submarine => 3,
        ShipType.Destroyer => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static List<CoordinateDto> Cells(CoordinateDto origin, Orientation orientation, int size) =>
        Enumerable.Range(0, size)
            .Select(i => orientation == Orientation.Horizontal
                ? origin with { X = origin.X + i }
                : origin with { Y = origin.Y + i })
            .ToList();
```

- [ ] **Step 4: Compléter `GameStateStore`**

Ajouter à la classe existante :

```csharp
    public IReadOnlyList<OpenGameDto> OpenGames { get; private set; } = [];
    public IReadOnlyList<PowerDefinitionDto> PowerCatalog { get; private set; } = [];

    public Task LoadOpenGamesAsync() => RunAsync(async () =>
    {
        OpenGames = await api.ListOpenGamesAsync(CancellationToken.None);
    });

    public Task LoadPowerCatalogAsync() => RunAsync(async () =>
    {
        PowerCatalog = await api.GetPowerCatalogAsync(CancellationToken.None);
    });

    public Task PlaceFleetAsync(PlaceFleetRequest request) => RunAsync(async () =>
    {
        EnsureGameLoaded();
        CurrentGame = await api.PlaceFleetAsync(CurrentGame!.GameId, _playerToken, request, CancellationToken.None);
    });

    public Task FireAsync(CoordinateDto target) => RunAsync(async () =>
    {
        EnsureGameLoaded();
        await api.FireAsync(CurrentGame!.GameId, _playerToken, new FireRequest(target), CancellationToken.None);
        CurrentGame = await api.GetGameAsync(CurrentGame.GameId, _playerToken, CancellationToken.None);
    });

    public Task UsePowerAsync(PowerId id, PowerTargetDto target) => RunAsync(async () =>
    {
        EnsureGameLoaded();
        await api.UsePowerAsync(CurrentGame!.GameId, _playerToken, new UsePowerRequest(id, target), CancellationToken.None);
        CurrentGame = await api.GetGameAsync(CurrentGame.GameId, _playerToken, CancellationToken.None);
    });

    public Task ForfeitAsync() => RunAsync(async () =>
    {
        EnsureGameLoaded();
        CurrentGame = await api.ForfeitAsync(CurrentGame!.GameId, _playerToken, CancellationToken.None);
    });

    private void EnsureGameLoaded()
    {
        if (CurrentGame is null)
        {
            throw new InvalidOperationException("Aucune partie chargée.");
        }
    }
```

- [ ] **Step 5: Run pour vérifier que tout passe**

Run: `dotnet test`
Expected: PASS (tous les tests)

- [ ] **Step 6: Commit**

```bash
git add src/Naval.App/Services/GameStateStore.cs src/Naval.App/Services/FakeGameApiClient.cs tests/Naval.Tests/App
git commit -m "feat: complète GameStateStore et FakeGameApiClient (placement, tir, pouvoirs, abandon)"
```

---

### Task 11: `Index.razor` + `Lobby.razor`

**Files:**
- Create: `src/Naval.App/Pages/Index.razor`
- Create: `src/Naval.App/Pages/Lobby.razor`

**Interfaces:**
- Consumes: `GameStateStore.CreateGameAsync(CreateGameRequest)` (Task 4/10), `DsConsole`,
  `DsButton`, `DsErrorBanner` (Task 6)
- Produces: routes `/` et `/lobby`, point d'entrée du flux `Index → Lobby → Deploy`.

- [ ] **Step 1: `Index.razor`**

```razor
@page "/"
@inject NavigationManager Navigation

<DsConsole>
    <TopScreen>
        <DsTopScreen>
            <h1>Bataille Navale</h1>
        </DsTopScreen>
    </TopScreen>
    <BottomScreen>
        <DsBottomScreen>
            <DsButton Label="Nouvelle partie" OnClick="GoToLobby" />
        </DsBottomScreen>
    </BottomScreen>
</DsConsole>

@code {
    private void GoToLobby() => Navigation.NavigateTo("/lobby");
}
```

- [ ] **Step 2: `Lobby.razor`**

```razor
@page "/lobby"
@inject GameStateStore Store
@inject NavigationManager Navigation
@implements IDisposable

<DsConsole>
    <TopScreen>
        <DsTopScreen>
            <h2>Configurer la partie</h2>
            <DsErrorBanner Problem="Store.LastError" />
        </DsTopScreen>
    </TopScreen>
    <BottomScreen>
        <DsBottomScreen>
            <label>
                Pseudo
                <input @bind="_playerName" />
            </label>
            <label>
                Taille de grille
                <input type="number" @bind="_gridSize" min="8" max="16" />
            </label>
            <label>
                Flotte
                <select @bind="_fleetPreset">
                    <option value="Classic">Classic</option>
                    <option value="Skirmish">Skirmish</option>
                </select>
            </label>
            <DsButton Label="Démarrer" OnClick="StartGame" />
        </DsBottomScreen>
    </BottomScreen>
</DsConsole>

@code {
    private string _playerName = "Joueur";
    private int _gridSize = 10;
    private string _fleetPreset = "Classic";

    protected override void OnInitialized() => Store.StateChanged += OnStoreChanged;

    private async Task StartGame()
    {
        var request = new CreateGameRequest(_playerName, GameMode.SinglePlayer, _gridSize, _gridSize,
            _fleetPreset, AiLevel.Random, [], 0, null);
        await Store.CreateGameAsync(request);

        if (Store.CurrentGame is not null)
        {
            Navigation.NavigateTo("/deploy");
        }
    }

    private void OnStoreChanged() => InvokeAsync(StateHasChanged);

    public void Dispose() => Store.StateChanged -= OnStoreChanged;
}
```

- [ ] **Step 3: Vérifier que le build passe**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 4: Vérification manuelle**

Run: `dotnet run --project src/Naval.App`
Ouvrir `/` dans le navigateur, cliquer "Nouvelle partie" → arrive sur `/lobby`, remplir le
formulaire, cliquer "Démarrer" → doit naviguer vers `/deploy` (page pas encore créée : une
erreur de routing 404 est attendue à ce stade, elle sera résolue par la Task 12).

- [ ] **Step 5: Commit**

```bash
git add src/Naval.App/Pages/Index.razor src/Naval.App/Pages/Lobby.razor
git commit -m "feat: pages Index et Lobby"
```

---

### Task 12: `Deploy.razor`

**Files:**
- Create: `src/Naval.App/Pages/Deploy.razor`

**Interfaces:**
- Consumes: `GameStateStore.CurrentGame`, `GameStateStore.PlaceFleetAsync(PlaceFleetRequest)`
  (Task 10), `GridView`, `ShipTray` (Task 7, 8)
- Produces: route `/deploy`.

- [ ] **Step 1: `Deploy.razor`**

```razor
@page "/deploy"
@inject GameStateStore Store
@inject NavigationManager Navigation
@implements IDisposable

<DsConsole>
    <TopScreen>
        <DsTopScreen>
            @if (Store.CurrentGame is { } game)
            {
                <GridView Board="game.Self.Board" OnCellClicked="HandleCellClicked" />
            }
        </DsTopScreen>
    </TopScreen>
    <BottomScreen>
        <DsBottomScreen>
            <DsErrorBanner Problem="Store.LastError" />
            @if (Store.CurrentGame is { } current)
            {
                <ShipTray @ref="_shipTray" Fleet="current.Self.Fleet" OnShipPlaced="HandleShipPlaced" />
            }
            <DsButton Label="Valider la flotte" OnClick="ConfirmFleet" />
        </DsBottomScreen>
    </BottomScreen>
</DsConsole>

@code {
    private ShipTray? _shipTray;
    private readonly List<ShipPlacementDto> _placements = [];

    protected override void OnInitialized()
    {
        Store.StateChanged += OnStoreChanged;
        if (Store.CurrentGame is null)
        {
            Navigation.NavigateTo("/lobby");
        }
    }

    private Task HandleCellClicked(CoordinateDto coordinate) =>
        _shipTray?.PlaceSelectedAtAsync(coordinate) ?? Task.CompletedTask;

    private void HandleShipPlaced(ShipPlacementDto placement) => _placements.Add(placement);

    private async Task ConfirmFleet()
    {
        await Store.PlaceFleetAsync(new PlaceFleetRequest(_placements));
        if (Store.CurrentGame?.Status == GameStatus.InProgress)
        {
            Navigation.NavigateTo("/battle");
        }
    }

    private void OnStoreChanged() => InvokeAsync(StateHasChanged);

    public void Dispose() => Store.StateChanged -= OnStoreChanged;
}
```

- [ ] **Step 2: Vérifier que le build passe**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 3: Vérification manuelle**

Run: `dotnet run --project src/Naval.App`
Depuis `/lobby`, créer une partie → arrive sur `/deploy`, la grille s'affiche vide. Cliquer sur
un navire dans `ShipTray`, puis sur une case de la grille → pas d'erreur console. Cliquer
"Valider la flotte" → navigue vers `/battle` (page pas encore créée : 404 attendu, résolu par la
Task 13).

- [ ] **Step 4: Commit**

```bash
git add src/Naval.App/Pages/Deploy.razor
git commit -m "feat: page Deploy"
```

---

### Task 13: `Battle.razor`

**Files:**
- Create: `src/Naval.App/Pages/Battle.razor`

**Interfaces:**
- Consumes: `GameStateStore.FireAsync`, `GameStateStore.UsePowerAsync` (Task 10), `GridView`
  (Task 7), `EnergyGauge`, `ChargeMeter`, `PowerBar`, `EventLog` (Task 9)
- Produces: route `/battle`.

- [ ] **Step 1: `Battle.razor`**

```razor
@page "/battle"
@inject GameStateStore Store
@inject NavigationManager Navigation
@implements IDisposable

<DsConsole>
    <TopScreen>
        <DsTopScreen>
            @if (Store.CurrentGame is { } game)
            {
                <GridView Board="game.Opponent.TargetBoard" OnCellClicked="HandleFire" />
            }
        </DsTopScreen>
    </TopScreen>
    <BottomScreen>
        <DsBottomScreen>
            <DsErrorBanner Problem="Store.LastError" />
            @if (Store.CurrentGame is { } current)
            {
                <GridView Board="current.Self.Board" />
                <EnergyGauge Energy="current.Self.Energy" />
                <ChargeMeter Charge="current.Opponent.Charge" />
                <PowerBar Powers="current.Self.Powers" OnPowerActivated="HandlePower" />
                <EventLog Events="current.RecentEvents" />
            }
        </DsBottomScreen>
    </BottomScreen>
</DsConsole>

@code {
    protected override void OnInitialized()
    {
        Store.StateChanged += OnStoreChanged;
        if (Store.CurrentGame is null)
        {
            Navigation.NavigateTo("/lobby");
        }
    }

    private async Task HandleFire(CoordinateDto target)
    {
        await Store.FireAsync(target);
        if (Store.CurrentGame?.Status == GameStatus.Finished)
        {
            Navigation.NavigateTo("/result");
        }
    }

    private Task HandlePower(PowerId id) =>
        Store.UsePowerAsync(id, new PowerTargetDto(null, null, null, null, null));

    private void OnStoreChanged() => InvokeAsync(StateHasChanged);

    public void Dispose() => Store.StateChanged -= OnStoreChanged;
}
```

- [ ] **Step 2: Vérifier que le build passe**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 3: Vérification manuelle**

Run: `dotnet run --project src/Naval.App`
Depuis `/deploy`, valider la flotte → arrive sur `/battle`. Cliquer une case de la grille
adverse → la case change de couleur (miss ou hit selon `FakeGameApiClient`), le journal se met à
jour, aucune erreur console.

- [ ] **Step 4: Commit**

```bash
git add src/Naval.App/Pages/Battle.razor
git commit -m "feat: page Battle"
```

---

### Task 14: `Result.razor`

**Files:**
- Create: `src/Naval.App/Pages/Result.razor`

**Interfaces:**
- Consumes: `GameStateStore.CurrentGame` (`WinnerId`, `Self.PlayerId`)
- Produces: route `/result`, ferme la boucle `Index → Lobby → Deploy → Battle → Result`.

- [ ] **Step 1: `Result.razor`**

```razor
@page "/result"
@inject GameStateStore Store
@inject NavigationManager Navigation

<DsConsole>
    <TopScreen>
        <DsTopScreen>
            @if (Store.CurrentGame is { WinnerId: { } winnerId } game)
            {
                <h2>@(winnerId == game.Self.PlayerId ? "Victoire" : "Défaite")</h2>
            }
        </DsTopScreen>
    </TopScreen>
    <BottomScreen>
        <DsBottomScreen>
            <DsButton Label="Rejouer" OnClick="GoToLobby" />
        </DsBottomScreen>
    </BottomScreen>
</DsConsole>

@code {
    protected override void OnInitialized()
    {
        if (Store.CurrentGame is null)
        {
            Navigation.NavigateTo("/lobby");
        }
    }

    private void GoToLobby() => Navigation.NavigateTo("/lobby");
}
```

- [ ] **Step 2: Vérifier que le build passe**

Run: `dotnet build`
Expected: `Build succeeded.`

- [ ] **Step 3: Vérifier la suite complète de tests**

Run: `dotnet test`
Expected: PASS (tous les tests, `Naval.Shared` + `Naval.App`)

- [ ] **Step 4: Vérification manuelle du flux complet**

Run: `dotnet run --project src/Naval.App`
Parcourir `/` → `/lobby` → `/deploy` → `/battle` → cliquer jusqu'à ce que le
`FakeGameApiClient` renvoie un statut `Finished` (nécessite de compléter `FireAsync` dans le fake
pour déclencher une victoire après un nombre de touches suffisant — si ce n'est pas déjà le cas,
c'est un signal que `FakeGameApiClient.FireAsync` (Task 10) doit être ajusté pour permettre de
tester ce chemin ; documenter l'ajustement dans le commit s'il a lieu). Vérifier que `/result`
affiche "Victoire" ou "Défaite" sans erreur console.

- [ ] **Step 5: Commit**

```bash
git add src/Naval.App/Pages/Result.razor
git commit -m "feat: page Result — flux solo complet Index a Result"
```

---

## Self-Review

**Couverture de la spec :**
- Bootstrap solution/projets → Task 1
- `tokens.css` → Task 2
- `IGameApiClient` / `GameApiException` / `GameApiClient` squelette → Task 3
- `GameStateStore` (event `StateChanged`, `LastError`) + `FakeGameApiClient` → Task 4, complétés
  Task 10
- `Components/Ds/*` (`DsConsole`, `DsTopScreen`, `DsBottomScreen`, `DsButton`, `DsErrorBanner`) →
  Task 6
- `GridView` / `GridCell` → Task 7
- `ShipTray` → Task 8
- `EnergyGauge` / `ChargeMeter` / `PowerBar` / `EventLog` → Task 9
- Gestion des erreurs par `Code` via `DsErrorBanner` → Task 6, branché dans toutes les Pages
  (Task 11-14)
- Tests `GameStateStoreTests` / `FakeGameApiClientTests` → Task 4, Task 10
- `Pages/Index`, `Lobby`, `Deploy`, `Battle`, `Result` → Tasks 11-14
- Répartition socle / fan-out / assemblage → Phases 0/1/2 de ce document

Tout ce qui est dans la spec du cycle 1 est couvert. `Components/Fx/`, `AudioService`,
`GameApiClient` réel restent hors périmètre, comme prévu.

**Cohérence des types :** vérifié que `GameStateStore` (Task 4/10), `IGameApiClient` (Task 3) et
`FakeGameApiClient` (Task 4/10) utilisent les mêmes signatures partout ; que les composants
`Game/*` (Task 7-9) exposent exactement les paramètres consommés par les `Pages/*` (Task 11-14) ;
que `ShipTray.PlaceSelectedAtAsync` (Task 8) est bien la méthode appelée par `Deploy.razor`
(Task 12).

**Pas de placeholder :** chaque étape contient du code complet et exécutable ; les
`NotImplementedException` de la Task 3/4 sont un état intentionnel et transitoire, explicitement
résolu à la Task 10, pas un TODO oublié.
