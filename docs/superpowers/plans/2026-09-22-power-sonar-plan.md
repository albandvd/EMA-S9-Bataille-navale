# Premier pouvoir de bout en bout (Sonar) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Faire fonctionner le pouvoir Sonar (P-01) de bout en bout — activation REST,
économie d'énergie, cooldown, front — pour valider le pipeline générique des pouvoirs avant
d'enchaîner sur les 22 autres.

**Architecture:** `IPowerHandler`/`PowerRegistry`/`PowerSlot` dans
`Naval.Shared/Domain/Powers/`, orchestrés par `GameEngine.ActivatePower` (pur, sans DI, même
patron que `GameEngine.ExecuteShot`). `GameService.UsePowerAsync` fait les vérifications de
tour/verrouillage puis délègue. `PowerEndpoints.cs` expose `POST /api/games/{id}/powers`. Le
front (`PowerBar.razor`, `GameStateStore.UsePowerAsync`, `GameApiClient`) est déjà entièrement
câblé — seul `Battle.razor` doit apprendre à capturer une case cible avant d'appeler l'API.

**Tech Stack:** .NET 10, ASP.NET Core Minimal API, Blazor WebAssembly, xUnit + FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-09-22-power-sonar-design.md`

## Global Constraints

- `Nullable` et `TreatWarningsAsErrors` activés : tout nouveau code doit compiler sans
  avertissement.
- `Naval.Shared/Domain` ne référence jamais `Naval.Shared/Contracts` pour le **mapping** —
  mais suit le patron déjà établi dans ce même dossier (`Ship.cs`, `PlayerState.cs`,
  `GameEngine.cs`) qui importe `Naval.Shared.Contracts` pour les enums partagés (`PowerId`,
  `ShipType`…). Ne pas ajouter de référence à `Naval.Shared.Contracts.Mapping` ni à un DTO
  `record` depuis `Domain`.
- Les endpoints ne contiennent aucune règle de jeu ; ≤ 15 lignes de corps.
- Codes d'erreur : uniquement ceux déjà présents dans `ErrorCodes`
  (`src/Naval.Shared/Contracts/Realtime.cs`) — ne pas en ajouter.
- `dotnet build` puis `dotnet test` doivent passer avant chaque commit de tâche qui touche du
  code (pas nécessaire pour une tâche purement documentaire).
- Chemin de l'endpoint et forme du JSON déjà figés dans `api.http` (§"Sonar") : `POST
  /api/games/{gameId}/powers` avec `{ "powerId": "Sonar", "target": { "cell": {...}, ... } }`.

---

### Task 1: Primitives du pouvoir (Domain/Powers)

**Files:**
- Create: `src/Naval.Shared/Domain/Powers/PowerSlot.cs`
- Create: `src/Naval.Shared/Domain/Powers/IPowerHandler.cs`
- Create: `src/Naval.Shared/Domain/Powers/PowerRegistry.cs`
- Test: `tests/Naval.Tests/Domain/Powers/PowerRegistryTests.cs`

**Interfaces:**
- Produces: `PowerSlot { PowerId PowerId; PowerSlotStatus Status; int ChargeRemaining; int
  CooldownRemaining; int UsesLeft; }`, `IPowerHandler { PowerId Id; string? Validate(PlayerState,
  PlayerState, PowerTargetDto); PowerEffectResult Execute(PlayerState, PlayerState,
  PowerTargetDto); }`, `RevealedCell(Coordinate Cell, bool Occupied)`, `PowerEffectResult(int?
  RevealedCount, IReadOnlyList<RevealedCell> RevealedCells, string Message)`, `PowerRegistry(IEnumerable<IPowerHandler>) { IPowerHandler? Find(PowerId); }`.

- [ ] **Step 1: Écrire le test qui échoue pour `PowerRegistry`**

```csharp
// tests/Naval.Tests/Domain/Powers/PowerRegistryTests.cs
using FluentAssertions;
using Naval.Shared.Contracts;
using Naval.Shared.Domain;
using Naval.Shared.Domain.Powers;
using Xunit;

namespace Naval.Tests.Domain.Powers;

public sealed class PowerRegistryTests
{
    private sealed class StubHandler : IPowerHandler
    {
        public PowerId Id => PowerId.Sonar;
        public string? Validate(PlayerState caster, PlayerState target, PowerTargetDto powerTarget) => null;
        public PowerEffectResult Execute(PlayerState caster, PlayerState target, PowerTargetDto powerTarget) =>
            new(RevealedCount: 0, RevealedCells: [], Message: "stub");
    }

    [Fact]
    public void Find_returns_the_registered_handler_for_its_id()
    {
        var registry = new PowerRegistry([new StubHandler()]);

        registry.Find(PowerId.Sonar).Should().BeOfType<StubHandler>();
    }

    [Fact]
    public void Find_returns_null_for_an_unregistered_power()
    {
        var registry = new PowerRegistry([new StubHandler()]);

        registry.Find(PowerId.TripleSalvo).Should().BeNull();
    }
}
```

- [ ] **Step 2: Lancer le test pour vérifier qu'il échoue (types absents)**

Run: `dotnet test --filter FullyQualifiedName~PowerRegistryTests`
Expected: FAIL — `PowerRegistry`/`IPowerHandler`/`PowerEffectResult` n'existent pas encore.

- [ ] **Step 3: Créer `PowerSlot.cs`**

```csharp
// src/Naval.Shared/Domain/Powers/PowerSlot.cs
using Naval.Shared.Contracts;

namespace Naval.Shared.Domain.Powers;

/// <summary>État d'exécution d'un pouvoir équipé, pour un joueur. Le catalogue
/// (<see cref="PowerCatalog"/>) fournit les valeurs statiques ; ce type porte l'état mutable.</summary>
public sealed class PowerSlot
{
    public PowerId PowerId { get; }
    public PowerSlotStatus Status { get; set; } = PowerSlotStatus.Ready;
    public int ChargeRemaining { get; set; }
    public int CooldownRemaining { get; set; }

    /// <summary>-1 = illimité, comme <c>PowerDefinitionDto.MaxUses</c>.</summary>
    public int UsesLeft { get; set; }

    public PowerSlot(PowerId powerId, int usesLeft)
    {
        PowerId = powerId;
        UsesLeft = usesLeft;
    }
}
```

- [ ] **Step 4: Créer `IPowerHandler.cs`**

```csharp
// src/Naval.Shared/Domain/Powers/IPowerHandler.cs
using Naval.Shared.Contracts;

namespace Naval.Shared.Domain.Powers;

/// <summary>Une case révélée par un pouvoir de reconnaissance qui identifie le contenu exact
/// (ex : Radar tactique). Sonar ne l'utilise jamais — il ne renvoie qu'un décompte.</summary>
public sealed record RevealedCell(Coordinate Cell, bool Occupied);

/// <summary>Résultat brut de l'exécution d'un pouvoir, avant mise en forme en DTO.</summary>
public sealed record PowerEffectResult(
    int? RevealedCount,
    IReadOnlyList<RevealedCell> RevealedCells,
    string Message);

/// <summary>
/// Un pouvoir = une définition statique (<see cref="PowerCatalog"/>) + un handler qui porte le
/// comportement. Voir docs/02-pouvoirs.md §5. L'énergie et le cooldown sont déjà contrôlés par
/// <see cref="GameEngine.ActivatePower"/> avant l'appel : <see cref="Validate"/> ne vérifie que
/// les préconditions propres au pouvoir (ex : case cible dans la grille).
/// </summary>
public interface IPowerHandler
{
    PowerId Id { get; }

    string? Validate(PlayerState caster, PlayerState target, PowerTargetDto powerTarget);

    PowerEffectResult Execute(PlayerState caster, PlayerState target, PowerTargetDto powerTarget);
}
```

- [ ] **Step 5: Créer `PowerRegistry.cs`**

```csharp
// src/Naval.Shared/Domain/Powers/PowerRegistry.cs
using Naval.Shared.Contracts;

namespace Naval.Shared.Domain.Powers;

/// <summary>Indexe les handlers par PowerId. Construite par Naval.Api au démarrage à partir
/// des handlers enregistrés en DI ; ce type lui-même ne dépend d'aucun conteneur DI.</summary>
public sealed class PowerRegistry
{
    private readonly IReadOnlyDictionary<PowerId, IPowerHandler> _handlers;

    public PowerRegistry(IEnumerable<IPowerHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.Id);
    }

    public IPowerHandler? Find(PowerId id) => _handlers.GetValueOrDefault(id);
}
```

- [ ] **Step 6: Lancer le test pour vérifier qu'il passe**

Run: `dotnet test --filter FullyQualifiedName~PowerRegistryTests`
Expected: PASS (2 tests)

- [ ] **Step 7: Commit**

```bash
git add src/Naval.Shared/Domain/Powers/PowerSlot.cs \
        src/Naval.Shared/Domain/Powers/IPowerHandler.cs \
        src/Naval.Shared/Domain/Powers/PowerRegistry.cs \
        tests/Naval.Tests/Domain/Powers/PowerRegistryTests.cs
git commit -m "feat(E-14): primitives du pouvoir — PowerSlot, IPowerHandler, PowerRegistry"
```

---

### Task 2: `PlayerState` porte le loadout et les slots

**Files:**
- Modify: `src/Naval.Shared/Domain/PlayerState.cs`
- Test: `tests/Naval.Tests/Domain/PlayerStateTests.cs`

**Interfaces:**
- Consumes: `PowerSlot(PowerId, int usesLeft)` (Task 1), `PowerCatalog.All` (existant,
  `src/Naval.Shared/Domain/PowerCatalog.cs`) pour lire `MaxUses`.
- Produces: `PlayerState.EquippedPowers : IReadOnlyList<PowerId>`, `PlayerState.PowerSlots :
  List<PowerSlot>`, nouveau paramètre optionnel `equippedPowers` en fin de constructeur (les
  appels existants sans ce paramètre continuent de compiler).

- [ ] **Step 1: Écrire le test qui échoue**

```csharp
// tests/Naval.Tests/Domain/PlayerStateTests.cs
using FluentAssertions;
using Naval.Shared.Contracts;
using Naval.Shared.Domain;
using Xunit;

namespace Naval.Tests.Domain;

public sealed class PlayerStateTests
{
    [Fact]
    public void Equipping_a_power_creates_one_ready_slot_with_its_starting_uses()
    {
        var player = new PlayerState(PlayerId.New(), "P1", PlayerSlot.One, "t1", 10, 10,
            equippedPowers: [PowerId.Sonar]);

        player.EquippedPowers.Should().ContainSingle().Which.Should().Be(PowerId.Sonar);
        var slot = player.PowerSlots.Should().ContainSingle().Subject;
        slot.PowerId.Should().Be(PowerId.Sonar);
        slot.Status.Should().Be(PowerSlotStatus.Ready);
        slot.UsesLeft.Should().Be(-1); // Sonar : MaxUses illimité dans PowerCatalog
    }

    [Fact]
    public void No_equipped_powers_by_default()
    {
        var player = new PlayerState(PlayerId.New(), "P1", PlayerSlot.One, "t1", 10, 10);

        player.EquippedPowers.Should().BeEmpty();
        player.PowerSlots.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Lancer le test pour vérifier qu'il échoue**

Run: `dotnet test --filter FullyQualifiedName~PlayerStateTests`
Expected: FAIL — `equippedPowers` n'existe pas sur le constructeur, `EquippedPowers`/`PowerSlots`
n'existent pas.

- [ ] **Step 3: Modifier `PlayerState.cs`**

Ajouter après `public int EnergySpent { get; set; }` :

```csharp
    public IReadOnlyList<PowerId> EquippedPowers { get; }
    public List<Powers.PowerSlot> PowerSlots { get; }
```

Remplacer la signature et le corps du constructeur :

```csharp
    public PlayerState(PlayerId id, string name, PlayerSlot slot, string token, int gridWidth, int gridHeight,
        bool isAi = false, IReadOnlyList<PowerId>? equippedPowers = null)
    {
        Id = id;
        Name = name;
        Slot = slot;
        Token = token;
        IsAi = isAi;
        IsConnected = !isAi;
        IncomingBoard = new Board(gridWidth, gridHeight);
        OutgoingBoard = new Board(gridWidth, gridHeight);

        EquippedPowers = equippedPowers ?? [];
        PowerSlots = EquippedPowers
            .Select(powerId => new Powers.PowerSlot(
                powerId,
                PowerCatalog.All.First(d => d.Id == powerId).MaxUses))
            .ToList();
    }
```

- [ ] **Step 4: Lancer le test pour vérifier qu'il passe**

Run: `dotnet test --filter FullyQualifiedName~PlayerStateTests`
Expected: PASS (2 tests)

- [ ] **Step 5: Lancer toute la suite pour vérifier l'absence de régression**

Run: `dotnet build && dotnet test`
Expected: build 0 avertissement, tous les tests verts (les instanciations existantes de
`PlayerState` compilent sans changement grâce au paramètre optionnel).

- [ ] **Step 6: Commit**

```bash
git add src/Naval.Shared/Domain/PlayerState.cs tests/Naval.Tests/Domain/PlayerStateTests.cs
git commit -m "feat(E-13): PlayerState porte le loadout et les slots de pouvoir"
```

---

### Task 3: `SonarHandler` + `GameEngine.ActivatePower`

**Files:**
- Create: `src/Naval.Shared/Domain/Powers/SonarHandler.cs`
- Create: `src/Naval.Shared/Domain/Powers/PowerActivationResult.cs`
- Modify: `src/Naval.Shared/Domain/GameEngine.cs`
- Test: `tests/Naval.Tests/Domain/Powers/SonarTests.cs`

**Interfaces:**
- Consumes: `IPowerHandler`, `PowerEffectResult`, `RevealedCell`, `PowerRegistry` (Task 1),
  `PlayerState.EquippedPowers`/`PowerSlots` (Task 2), `PowerCatalog.All`, `ErrorCodes`
  (`src/Naval.Shared/Contracts/Realtime.cs`).
- Produces: `PowerActivationResult(PowerId, int EnergySpent, int EnergyRemaining, int
  CooldownApplied, PowerEffectResult Effect)`, `GameEngine.ActivatePower(PlayerState caster,
  PlayerState target, PowerId powerId, PowerTargetDto powerTarget, PowerRegistry registry) :
  (PowerActivationResult? result, string? errorCode)`, `GameEngine.TickPowerCooldowns(PlayerState
  player) : void`.

- [ ] **Step 1: Écrire les 3 tests qui échouent**

```csharp
// tests/Naval.Tests/Domain/Powers/SonarTests.cs
using FluentAssertions;
using Naval.Shared.Contracts;
using Naval.Shared.Domain;
using Naval.Shared.Domain.Powers;
using Xunit;

namespace Naval.Tests.Domain.Powers;

public sealed class SonarTests
{
    [Fact]
    public void Nominal_sonar_reveals_the_exact_count_of_ship_cells_in_radius()
    {
        var (caster, target) = BuildPlayers(casterEnergy: 5);
        // Croiseur vertical (0,0)-(0,1)-(0,2) : les 3 cases sont à distance <= 4 de (0,0).
        target.Fleet = new Fleet([new Ship("t-cruiser", ShipType.Cruiser, 3,
            new Coordinate(0, 0), Orientation.Vertical)]);
        var registry = new PowerRegistry([new SonarHandler()]);
        var powerTarget = new PowerTargetDto(new CoordinateDto(0, 0), null, null, null, null);

        var (result, error) = GameEngine.ActivatePower(caster, target, PowerId.Sonar, powerTarget, registry);

        error.Should().BeNull();
        result!.Effect.RevealedCount.Should().Be(3);
        result.Effect.RevealedCells.Should().BeEmpty(); // Sonar ne révèle jamais de position
        result.EnergySpent.Should().Be(3); // coût Sonar, cf. PowerCatalog
        caster.Energy.Should().Be(2);
        var slot = caster.PowerSlots.Single(s => s.PowerId == PowerId.Sonar);
        slot.Status.Should().Be(PowerSlotStatus.OnCooldown);
        slot.CooldownRemaining.Should().Be(3);
    }

    [Fact]
    public void Refusal_when_energy_is_insufficient_leaves_state_untouched()
    {
        var (caster, target) = BuildPlayers(casterEnergy: 1); // coût Sonar = 3
        target.Fleet = new Fleet([new Ship("t-cruiser", ShipType.Cruiser, 3,
            new Coordinate(0, 0), Orientation.Vertical)]);
        var registry = new PowerRegistry([new SonarHandler()]);
        var powerTarget = new PowerTargetDto(new CoordinateDto(0, 0), null, null, null, null);

        var (result, error) = GameEngine.ActivatePower(caster, target, PowerId.Sonar, powerTarget, registry);

        error.Should().Be(ErrorCodes.InsufficientEnergy);
        result.Should().BeNull();
        caster.Energy.Should().Be(1);
        caster.PowerSlots.Single().Status.Should().Be(PowerSlotStatus.Ready);
    }

    [Fact]
    public void Targeting_a_cell_outside_the_grid_is_rejected_without_throwing()
    {
        var (caster, target) = BuildPlayers(casterEnergy: 5); // grille 10x10
        target.Fleet = new Fleet([new Ship("t-cruiser", ShipType.Cruiser, 3,
            new Coordinate(0, 0), Orientation.Vertical)]);
        var registry = new PowerRegistry([new SonarHandler()]);
        var powerTarget = new PowerTargetDto(new CoordinateDto(10, 10), null, null, null, null);

        var (result, error) = GameEngine.ActivatePower(caster, target, PowerId.Sonar, powerTarget, registry);

        error.Should().Be(ErrorCodes.InvalidTarget);
        result.Should().BeNull();
        caster.Energy.Should().Be(5); // énergie non déduite sur un refus
    }

    private static (PlayerState caster, PlayerState target) BuildPlayers(int casterEnergy)
    {
        var caster = new PlayerState(PlayerId.New(), "P1", PlayerSlot.One, "t1", 10, 10,
            equippedPowers: [PowerId.Sonar]);
        caster.Energy = casterEnergy;
        var target = new PlayerState(PlayerId.New(), "P2", PlayerSlot.Two, "t2", 10, 10);
        return (caster, target);
    }
}
```

- [ ] **Step 2: Lancer les tests pour vérifier qu'ils échouent**

Run: `dotnet test --filter FullyQualifiedName~SonarTests`
Expected: FAIL — `SonarHandler`/`GameEngine.ActivatePower` n'existent pas encore.

- [ ] **Step 3: Créer `PowerActivationResult.cs`**

```csharp
// src/Naval.Shared/Domain/Powers/PowerActivationResult.cs
using Naval.Shared.Contracts;

namespace Naval.Shared.Domain.Powers;

/// <summary>Résultat d'une activation réussie, prêt à être mappé en <c>PowerResultDto</c> par
/// Naval.Shared/Contracts/Mapping/GameMapper.cs.</summary>
public sealed record PowerActivationResult(
    PowerId PowerId,
    int EnergySpent,
    int EnergyRemaining,
    int CooldownApplied,
    PowerEffectResult Effect);
```

- [ ] **Step 4: Créer `SonarHandler.cs`**

```csharp
// src/Naval.Shared/Domain/Powers/SonarHandler.cs
using Naval.Shared.Contracts;

namespace Naval.Shared.Domain.Powers;

/// <summary>
/// P-01 — docs/02-pouvoirs.md §3. Révèle le NOMBRE de cases occupées de la flotte adverse dans
/// un disque de rayon 4 (distance euclidienne, pas Chebyshev — un futur pouvoir de zone carrée
/// comme la Frappe orbitale ne doit pas réutiliser ce calcul de distance).
/// Ne révèle jamais de position : RevealedCells reste toujours vide.
/// </summary>
public sealed class SonarHandler : IPowerHandler
{
    private const int RadiusSquared = 16; // rayon 4 au carré

    public PowerId Id => PowerId.Sonar;

    public string? Validate(PlayerState caster, PlayerState target, PowerTargetDto powerTarget)
    {
        if (powerTarget.Cell is null)
            return "Le Sonar nécessite une case cible.";

        var cell = new Coordinate(powerTarget.Cell.Value.X, powerTarget.Cell.Value.Y);
        if (!cell.IsWithinBounds(caster.OutgoingBoard.Width, caster.OutgoingBoard.Height))
            return "La cible est hors de la grille.";

        return null;
    }

    public PowerEffectResult Execute(PlayerState caster, PlayerState target, PowerTargetDto powerTarget)
    {
        var center = new Coordinate(powerTarget.Cell!.Value.X, powerTarget.Cell.Value.Y);

        int count = target.Fleet?.Ships
            .SelectMany(s => s.Cells)
            .Count(cell => DistanceSquared(center, cell) <= RadiusSquared) ?? 0;

        return new PowerEffectResult(
            RevealedCount: count,
            RevealedCells: [],
            Message: $"Sonar : {count} case(s) détectée(s) en zone.");
    }

    private static int DistanceSquared(Coordinate a, Coordinate b)
    {
        int dx = a.X - b.X;
        int dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }
}
```

- [ ] **Step 5: Ajouter `ActivatePower` et `TickPowerCooldowns` à `GameEngine.cs`**

Ajouter en haut du fichier :

```csharp
using Naval.Shared.Domain.Powers;
```

Ajouter ces deux méthodes publiques (par exemple juste après `ExecuteShot`) :

```csharp
    /// <summary>
    /// Active un pouvoir. Vérifie équipement, statut du slot et énergie ; délègue la
    /// validation spécifique et l'effet au handler. Ne vérifie PAS le tour du joueur ni le
    /// statut de la partie : c'est la responsabilité de l'appelant (GameService), comme pour
    /// ExecuteShot.
    /// </summary>
    public static (PowerActivationResult? result, string? errorCode) ActivatePower(
        PlayerState caster, PlayerState target, PowerId powerId,
        PowerTargetDto powerTarget, PowerRegistry registry)
    {
        if (!caster.EquippedPowers.Contains(powerId))
            return (null, ErrorCodes.PowerNotEquipped);

        var slot = caster.PowerSlots.First(s => s.PowerId == powerId);
        var definition = PowerCatalog.All.First(d => d.Id == powerId);

        if (slot.Status is PowerSlotStatus.Charging or PowerSlotStatus.Armed)
            return (null, ErrorCodes.PowerAlreadyCharging);

        if (slot.Status == PowerSlotStatus.OnCooldown)
            return (null, ErrorCodes.PowerOnCooldown);

        if (slot.Status == PowerSlotStatus.Exhausted)
            return (null, ErrorCodes.PowerExhausted);

        if (caster.Energy < definition.EnergyCost)
            return (null, ErrorCodes.InsufficientEnergy);

        var handler = registry.Find(powerId)
            ?? throw new InvalidOperationException($"Aucun handler enregistré pour {powerId}.");

        if (handler.Validate(caster, target, powerTarget) is not null)
            return (null, ErrorCodes.InvalidTarget);

        caster.Energy -= definition.EnergyCost;
        var effect = handler.Execute(caster, target, powerTarget);

        slot.CooldownRemaining = definition.Cooldown;
        slot.Status = definition.Cooldown > 0 ? PowerSlotStatus.OnCooldown : PowerSlotStatus.Ready;

        if (definition.MaxUses >= 0)
        {
            slot.UsesLeft--;
            if (slot.UsesLeft <= 0) slot.Status = PowerSlotStatus.Exhausted;
        }

        return (new PowerActivationResult(powerId, definition.EnergyCost, caster.Energy,
            slot.CooldownRemaining, effect), null);
    }

    /// <summary>Décrémente les cooldowns en cours d'un joueur d'un tour. Appelé quand ce joueur
    /// redevient actif (E-14).</summary>
    public static void TickPowerCooldowns(PlayerState player)
    {
        foreach (var slot in player.PowerSlots)
        {
            if (slot.Status != PowerSlotStatus.OnCooldown) continue;

            slot.CooldownRemaining--;
            if (slot.CooldownRemaining <= 0)
            {
                slot.CooldownRemaining = 0;
                slot.Status = PowerSlotStatus.Ready;
            }
        }
    }
```

- [ ] **Step 6: Lancer les tests pour vérifier qu'ils passent**

Run: `dotnet test --filter FullyQualifiedName~SonarTests`
Expected: PASS (3 tests)

- [ ] **Step 7: Suite complète**

Run: `dotnet build && dotnet test`
Expected: 0 avertissement, tous verts.

- [ ] **Step 8: Commit**

```bash
git add src/Naval.Shared/Domain/Powers/SonarHandler.cs \
        src/Naval.Shared/Domain/Powers/PowerActivationResult.cs \
        src/Naval.Shared/Domain/GameEngine.cs \
        tests/Naval.Tests/Domain/Powers/SonarTests.cs
git commit -m "feat(E-10,E-14): Sonar — GameEngine.ActivatePower, cooldown, 3 tests requis"
```

---

### Task 4: Événements + `GameMapper`

**Files:**
- Modify: `src/Naval.Shared/Domain/Events/GameEvent.cs`
- Modify: `src/Naval.Shared/Contracts/Mapping/GameMapper.cs`
- Test: `tests/Naval.Tests/Api/GameMapperTests.cs`

**Interfaces:**
- Consumes: `PowerSlot`, `PowerActivationResult`, `PowerEffectResult`, `RevealedCell` (Tasks
  1 & 3), `PlayerState.PowerSlots`/`EquippedPowers` (Task 2), contrats existants
  (`PowerSlotDto`, `PowerResultDto`, `RevealedCellDto` dans `Responses.cs`).
- Produces: `GameMapper.ToPowerResultDto(Game game, PowerActivationResult activation) :
  PowerResultDto`. `ToSelfViewDto`/`ToOpponentViewDto` remplissent désormais `Powers` /
  `EquippedPowers` au lieu de `[]`.

- [ ] **Step 1: Écrire le test qui échoue**

```csharp
// tests/Naval.Tests/Api/GameMapperTests.cs
using FluentAssertions;
using Naval.Shared.Contracts;
using Naval.Shared.Contracts.Mapping;
using Naval.Shared.Domain;
using Xunit;

namespace Naval.Tests.Api;

public sealed class GameMapperTests
{
    [Fact]
    public void ToSelfViewDto_reports_the_real_status_of_an_equipped_power()
    {
        var player = new PlayerState(PlayerId.New(), "P1", PlayerSlot.One, "t1", 10, 10,
            equippedPowers: [PowerId.Sonar]);
        player.Energy = 5;

        var dto = GameMapper.ToSelfViewDto(player);

        var slot = dto.Powers.Should().ContainSingle().Subject;
        slot.PowerId.Should().Be(PowerId.Sonar);
        slot.Status.Should().Be(PowerSlotStatus.Ready);
        slot.CanAffordNow.Should().BeTrue(); // Energy=5 >= coût Sonar (3)
    }

    [Fact]
    public void ToOpponentViewDto_exposes_only_the_list_of_equipped_powers_not_their_status()
    {
        var opponent = new PlayerState(PlayerId.New(), "P2", PlayerSlot.Two, "t2", 10, 10,
            equippedPowers: [PowerId.Sonar]);
        var viewer = new PlayerState(PlayerId.New(), "P1", PlayerSlot.One, "t1", 10, 10);

        var dto = GameMapper.ToOpponentViewDto(opponent, viewer);

        dto.EquippedPowers.Should().ContainSingle().Which.Should().Be(PowerId.Sonar);
    }
}
```

- [ ] **Step 2: Lancer le test pour vérifier qu'il échoue**

Run: `dotnet test --filter FullyQualifiedName~GameMapperTests`
Expected: FAIL — `dto.Powers` est vide (`[]` codé en dur actuellement).

- [ ] **Step 3: Ajouter les 3 nouveaux événements à `GameEvent.cs`**

```csharp
using Naval.Shared.Contracts;
```

en haut du fichier, puis à la fin :

```csharp
public sealed record PowerActivatedEvent(
    int Sequence, DateTimeOffset AtUtc, PlayerId CasterId, PowerId PowerId, string Message)
    : GameEvent(Sequence, AtUtc, CasterId, Message);

public sealed record PowerResolvedEvent(
    int Sequence, DateTimeOffset AtUtc, PlayerId CasterId, PowerId PowerId, int? RevealedCount, string Message)
    : GameEvent(Sequence, AtUtc, CasterId, Message);

public sealed record EnergyChangedEvent(
    int Sequence, DateTimeOffset AtUtc, PlayerId PlayerId, int Delta, int Total, string Message)
    : GameEvent(Sequence, AtUtc, PlayerId, Message);
```

- [ ] **Step 4: Modifier `GameMapper.cs`**

Ajouter en haut :

```csharp
using Naval.Shared.Domain.Powers;
```

Remplacer `Powers: []` dans `ToSelfViewDto` par :

```csharp
            Powers: player.PowerSlots.Select(s => ToPowerSlotDto(s, player.Energy)).ToList());
```

Remplacer `EquippedPowers: []` dans `ToOpponentViewDto` par :

```csharp
            EquippedPowers: opponent.EquippedPowers);
```

Ajouter une nouvelle méthode publique (par ex. après `ToGameOverDto`) :

```csharp
    public static PowerResultDto ToPowerResultDto(Game game, PowerActivationResult activation) =>
        new(
            GameId: game.Id.Value,
            TurnNumber: game.TurnNumber,
            PowerId: activation.PowerId,
            Charging: false,
            ChargeRemaining: 0,
            EnergySpent: activation.EnergySpent,
            EnergyRemaining: activation.EnergyRemaining,
            RevealedCells: activation.Effect.RevealedCells
                .Select(c => new RevealedCellDto(new CoordinateDto(c.Cell.X, c.Cell.Y), c.Occupied))
                .ToList(),
            RevealedCount: activation.Effect.RevealedCount,
            Shots: [],
            Message: activation.Effect.Message,
            NextPlayerId: null,
            GameOver: false,
            WinnerId: null);
```

Ajouter le helper privé (dans la section "Helpers privés") :

```csharp
    private static PowerSlotDto ToPowerSlotDto(PowerSlot slot, int casterEnergy)
    {
        var definition = PowerCatalog.All.First(d => d.Id == slot.PowerId);
        return new PowerSlotDto(
            PowerId: slot.PowerId,
            Status: slot.Status,
            ChargeRemaining: slot.ChargeRemaining,
            CooldownRemaining: slot.CooldownRemaining,
            UsesLeft: slot.UsesLeft,
            CanAffordNow: slot.Status == PowerSlotStatus.Ready && casterEnergy >= definition.EnergyCost);
    }
```

Étendre le `switch` dans `ToEventDto` :

```csharp
        var type = evt switch
        {
            ShotFiredEvent     => "ShotFired",
            TurnChangedEvent   => "TurnChanged",
            GameOverEvent      => "GameOver",
            PlayerReadyEvent   => "PlayerJoined",
            PowerActivatedEvent => "PowerActivated",
            PowerResolvedEvent  => "PowerResolved",
            EnergyChangedEvent  => "EnergyChanged",
            _ => "Unknown"
        };
```

- [ ] **Step 5: Lancer le test pour vérifier qu'il passe**

Run: `dotnet test --filter FullyQualifiedName~GameMapperTests`
Expected: PASS (2 tests)

- [ ] **Step 6: Suite complète**

Run: `dotnet build && dotnet test`
Expected: 0 avertissement, tous verts.

- [ ] **Step 7: Commit**

```bash
git add src/Naval.Shared/Domain/Events/GameEvent.cs \
        src/Naval.Shared/Contracts/Mapping/GameMapper.cs \
        tests/Naval.Tests/Api/GameMapperTests.cs
git commit -m "feat: événements de pouvoir + GameMapper reflète le vrai état des slots"
```

---

### Task 5: `GameService.UsePowerAsync` + énergie/cooldown par tour

**Files:**
- Modify: `src/Naval.Api/Services/GameService.cs`
- Modify: `tests/Naval.Tests/Api/GameServiceTests.cs`

**Interfaces:**
- Consumes: `GameEngine.ActivatePower`/`TickPowerCooldowns` (Task 3), `PowerRegistry` (Task 1),
  `GameMapper.ToPowerResultDto` (Task 4), `ErrorCodes`.
- Produces: `GameService(IGameStore store, PowerRegistry powers)` (nouveau paramètre de
  constructeur — **breaking change intentionnel**, tous les appelants doivent être mis à jour
  dans cette tâche), `GameService.UsePowerAsync(Guid gameId, string playerToken,
  UsePowerRequest req, CancellationToken ct) : Task<(PowerResultDto result, Game game)>`.

- [ ] **Step 1: Écrire le test qui échoue et mettre à jour les instanciations existantes**

Dans `tests/Naval.Tests/Api/GameServiceTests.cs`, ajouter en haut :

```csharp
using Naval.Shared.Domain;
using Naval.Shared.Domain.Powers;
```

Remplacer les deux `new GameService(new InMemoryGameStore())` existants par
`new GameService(new InMemoryGameStore(), new PowerRegistry([new SonarHandler()]))`, puis
ajouter :

```csharp
    [Fact]
    public async Task UsePowerAsync_activates_sonar_and_deducts_its_energy_cost()
    {
        var service = new GameService(new InMemoryGameStore(), new PowerRegistry([new SonarHandler()]));
        var createRequest = new CreateGameRequest("Joueur", GameMode.SinglePlayer, 10, 10, "Classic",
            AiLevel.Random, [PowerId.Sonar], 0, 42);
        var (createdGame, token) = await service.CreateGameAsync(createRequest, CancellationToken.None);

        var placements = await service.SuggestRandomFleetAsync(createdGame.Id.Value, token, 42, CancellationToken.None);
        await service.PlaceFleetAsync(createdGame.Id.Value, token, new PlaceFleetRequest(placements), CancellationToken.None);
        createdGame.Player1.Energy = 5; // au-delà du minimum accordé au début de la bataille

        var target = new PowerTargetDto(new CoordinateDto(5, 5), null, null, null, null);
        var (result, game) = await service.UsePowerAsync(createdGame.Id.Value, token,
            new UsePowerRequest(PowerId.Sonar, target), CancellationToken.None);

        result.PowerId.Should().Be(PowerId.Sonar);
        result.EnergySpent.Should().Be(3);
        result.EnergyRemaining.Should().Be(2);
        game.Player1.PowerSlots.Single(s => s.PowerId == PowerId.Sonar).Status
            .Should().Be(PowerSlotStatus.OnCooldown);
    }

    [Fact]
    public async Task UsePowerAsync_rejects_a_power_that_is_not_equipped()
    {
        var service = new GameService(new InMemoryGameStore(), new PowerRegistry([new SonarHandler()]));
        var createRequest = new CreateGameRequest("Joueur", GameMode.SinglePlayer, 10, 10, "Classic",
            AiLevel.Random, [], 0, 42); // Powers vide → équipe Sonar par défaut, pas TripleSalvo
        var (createdGame, token) = await service.CreateGameAsync(createRequest, CancellationToken.None);
        var placements = await service.SuggestRandomFleetAsync(createdGame.Id.Value, token, 42, CancellationToken.None);
        await service.PlaceFleetAsync(createdGame.Id.Value, token, new PlaceFleetRequest(placements), CancellationToken.None);

        var target = new PowerTargetDto(new CoordinateDto(5, 5), null, null, null, null);
        var act = () => service.UsePowerAsync(createdGame.Id.Value, token,
            new UsePowerRequest(PowerId.TripleSalvo, target), CancellationToken.None);

        (await act.Should().ThrowAsync<GameException>()).Which.Code.Should().Be(ErrorCodes.PowerNotEquipped);
    }
```

- [ ] **Step 2: Lancer les tests pour vérifier qu'ils échouent**

Run: `dotnet test --filter FullyQualifiedName~GameServiceTests`
Expected: FAIL — `GameService` n'a pas de second paramètre de constructeur,
`UsePowerAsync` n'existe pas.

- [ ] **Step 3: Modifier `GameService.cs`**

En haut du fichier, ajouter :

```csharp
using Naval.Shared.Domain.Powers;
```

Remplacer le champ et le constructeur :

```csharp
    private readonly IGameStore _store;
    private readonly PowerRegistry _powers;

    public GameService(IGameStore store, PowerRegistry powers)
    {
        _store = store;
        _powers = powers;
    }
```

Dans `CreateGameAsync`, juste avant `var p1 = new PlayerState(...)`, ajouter :

```csharp
        var equippedPowers = req.Powers.Count > 0 ? req.Powers : (IReadOnlyList<PowerId>)[PowerId.Sonar];
```

puis passer `equippedPowers: equippedPowers` aux trois appels `new PlayerState(...)` de cette
méthode (p1, l'IA en solo, et le p2 "En attente…" des modes en ligne) — dernier argument
nommé, sans toucher au reste de chaque appel.

Remplacer `StartBattle` et `AdvanceTurn` :

```csharp
    private static void StartBattle(Game game)
    {
        game.Status = GameStatus.InProgress;
        game.TurnNumber = 1;
        game.CurrentPlayerId = game.Player1.Id;

        GrantTurnStartBenefits(game, game.Player1);

        game.AddEvent(new TurnChangedEvent(
            game.NextSequence(), DateTimeOffset.UtcNow, game.Player1.Id, 1,
            "La bataille commence. Tour de " + game.Player1.Name + "."));
    }

    private static void AdvanceTurn(Game game, PlayerId currentShooter)
    {
        var next = game.GetOpponent(currentShooter);
        game.CurrentPlayerId = next.Id;

        GrantTurnStartBenefits(game, next);

        game.AddEvent(new TurnChangedEvent(
            game.NextSequence(), DateTimeOffset.UtcNow, next.Id, game.TurnNumber,
            $"Tour de {next.Name}."));
    }

    /// <summary>E-10 : +1 énergie au joueur qui devient actif ; E-14 : ses cooldowns de
    /// pouvoir avancent d'un tour au même moment.</summary>
    private static void GrantTurnStartBenefits(Game game, PlayerState player)
    {
        player.Energy += 1;
        GameEngine.TickPowerCooldowns(player);

        game.AddEvent(new EnergyChangedEvent(
            game.NextSequence(), DateTimeOffset.UtcNow, player.Id, 1, player.Energy,
            $"{player.Name} gagne 1 énergie."));
    }
```

Ajouter la nouvelle méthode publique `UsePowerAsync` (par ex. juste après `ForfeitAsync`) :

```csharp
    // ─── Pouvoirs ───

    public async Task<(PowerResultDto result, Game game)> UsePowerAsync(
        Guid gameId, string playerToken, UsePowerRequest req, CancellationToken ct)
    {
        var game = await RequireGameAsync(gameId, ct);

        await game.Lock.WaitAsync(ct);
        try
        {
            var caster = game.GetPlayerByToken(playerToken)
                ?? throw new GameException(ErrorCodes.NotAPlayer, "Token invalide.");

            if (game.Status != GameStatus.InProgress)
                throw new GameException(ErrorCodes.GameNotInProgress, "La partie n'est pas en cours.",
                    isConflict: true);

            if (game.CurrentPlayerId != caster.Id)
                throw new GameException(ErrorCodes.NotYourTurn, "Ce n'est pas votre tour.",
                    isConflict: true);

            var target = game.GetOpponent(caster.Id);

            var (activation, errorCode) = GameEngine.ActivatePower(
                caster, target, req.PowerId, req.Target, _powers);

            if (errorCode is not null)
                throw new GameException(errorCode, GetPowerErrorMessage(errorCode),
                    isConflict: IsPowerConflictCode(errorCode));

            caster.PowersUsed++;
            caster.EnergySpent += activation!.EnergySpent;

            game.AddEvent(new PowerActivatedEvent(
                game.NextSequence(), DateTimeOffset.UtcNow, caster.Id, activation.PowerId,
                $"{caster.Name} active {activation.PowerId}."));
            game.AddEvent(new PowerResolvedEvent(
                game.NextSequence(), DateTimeOffset.UtcNow, caster.Id, activation.PowerId,
                activation.Effect.RevealedCount, activation.Effect.Message));

            await _store.SaveAsync(game, ct);

            return (GameMapper.ToPowerResultDto(game, activation), game);
        }
        finally
        {
            game.Lock.Release();
        }
    }

    private static string GetPowerErrorMessage(string code) => code switch
    {
        ErrorCodes.PowerNotEquipped     => "Ce pouvoir n'est pas équipé.",
        ErrorCodes.PowerAlreadyCharging => "Ce pouvoir est déjà en cours de charge.",
        ErrorCodes.PowerOnCooldown      => "Ce pouvoir est en recharge.",
        ErrorCodes.PowerExhausted       => "Ce pouvoir n'a plus de charges disponibles.",
        ErrorCodes.InsufficientEnergy   => "Énergie insuffisante.",
        ErrorCodes.InvalidTarget        => "Cible invalide pour ce pouvoir.",
        _ => "Activation de pouvoir refusée."
    };

    private static bool IsPowerConflictCode(string code) =>
        code is ErrorCodes.PowerAlreadyCharging or ErrorCodes.PowerOnCooldown or ErrorCodes.PowerExhausted;
```

- [ ] **Step 4: Lancer les tests pour vérifier qu'ils passent**

Run: `dotnet test --filter FullyQualifiedName~GameServiceTests`
Expected: PASS (tous, y compris les 2 pré-existants mis à jour et les 2 nouveaux)

- [ ] **Step 5: Suite complète**

Run: `dotnet build && dotnet test`
Expected: 0 avertissement, tous verts (47 + nouveaux tests des tâches 1 à 5).

- [ ] **Step 6: Commit**

```bash
git add src/Naval.Api/Services/GameService.cs tests/Naval.Tests/Api/GameServiceTests.cs
git commit -m "feat(E-10,E-13): GameService.UsePowerAsync, loadout par défaut, énergie/tour"
```

---

### Task 6: Endpoint REST + câblage DI

**Files:**
- Create: `src/Naval.Api/Endpoints/PowerEndpoints.cs`
- Modify: `src/Naval.Api/Program.cs`

**Interfaces:**
- Consumes: `GameService.UsePowerAsync` (Task 5), `PlayerTokenAccessor.GetToken`,
  `NavalProblemDetails.Unauthorized` (existants, `src/Naval.Api/Infrastructure/`).
- Produces: `POST /api/games/{gameId}/powers` → `PowerResultDto` (forme déjà documentée dans
  `api.http`).

- [ ] **Step 1: Créer `PowerEndpoints.cs`**

```csharp
// src/Naval.Api/Endpoints/PowerEndpoints.cs
using Naval.Api.Infrastructure;
using Naval.Api.Services;
using Naval.Shared.Contracts;

namespace Naval.Api.Endpoints;

public static class PowerEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/games/{gameId:guid}").WithTags("Powers");
        group.MapPost("/powers", UsePower);
    }

    private static async Task<IResult> UsePower(
        Guid gameId,
        UsePowerRequest req,
        HttpContext ctx,
        GameService svc,
        CancellationToken ct)
    {
        var token = PlayerTokenAccessor.GetToken(ctx);
        if (token is null) return NavalProblemDetails.Unauthorized("En-tête X-Player-Token absent.");

        var (result, _) = await svc.UsePowerAsync(gameId, token, req, ct);
        return Results.Ok(result);
    }
}
```

- [ ] **Step 2: Modifier `Program.cs`**

En haut, avec les autres `using` :

```csharp
using Naval.Shared.Domain.Powers;
```

Après `builder.Services.AddScoped<AiTurnService>();` :

```csharp
builder.Services.AddSingleton<IPowerHandler, SonarHandler>();
builder.Services.AddSingleton(sp => new PowerRegistry(sp.GetServices<IPowerHandler>()));
```

Après `CatalogEndpoints.Map(app);` :

```csharp
PowerEndpoints.Map(app);
```

- [ ] **Step 3: Vérifier la compilation**

Run: `dotnet build`
Expected: 0 avertissement, 0 erreur (la résolution DI de `GameService(IGameStore,
PowerRegistry)` et `SonarHandler` se fait au démarrage, pas à la compilation — vérifiée à
l'étape suivante).

- [ ] **Step 4: Vérifier le démarrage réel de l'API et l'appel de l'endpoint**

```bash
dotnet run --project src/Naval.Api &
sleep 5
curl -s -X POST http://localhost:5119/api/games \
  -H "Content-Type: application/json" \
  -d '{"playerName":"Test","mode":"SinglePlayer","gridWidth":10,"gridHeight":10,"fleetPreset":"Classic","aiLevel":"Random","powers":["Sonar"],"turnTimeoutSeconds":0,"seed":42}'
```

Expected: `201` avec `gameId` + `playerToken`. Puis, avec ces valeurs, déployer la flotte
(`POST /api/games/{id}/fleet/random` puis `POST /api/games/{id}/fleet`) et appeler
`POST /api/games/{id}/powers` avec `{"powerId":"Sonar","target":{"cell":{"x":5,"y":5}}}` en-tête
`X-Player-Token` → `200` avec `revealedCount` renseigné et `revealedCells` vide. Arrêter le
serveur (`kill %1`) une fois vérifié.

- [ ] **Step 5: Commit**

```bash
git add src/Naval.Api/Endpoints/PowerEndpoints.cs src/Naval.Api/Program.cs
git commit -m "feat(E-16): POST /api/games/{id}/powers"
```

---

### Task 7: Front — capturer une case cible pour un pouvoir

**Files:**
- Modify: `src/Naval.App/Pages/Battle.razor`

**Interfaces:**
- Consumes: `GameStateStore.UsePowerAsync(PowerId, PowerTargetDto)` (déjà existant,
  `src/Naval.App/Services/GameStateStore.cs:64`), `GridView.OnCellClicked` (déjà existant,
  utilisé par `HandleFire`).

Contexte : `PowerBar.razor` et `GameStateStore` sont déjà entièrement câblés, mais
`Battle.razor` envoie actuellement un `PowerTargetDto` entièrement `null` quel que soit le
pouvoir (`HandlePower`, ligne 99) — Sonar exige `Target.Cell`, donc l'activation actuelle
échouerait toujours avec `INVALID_TARGET`. Ce correctif fait du clic sur la grille adverse la
façon de cibler un pouvoir en attente.

- [ ] **Step 1: Modifier `Battle.razor`**

Remplacer le bloc `@code` : ajouter un champ d'état et faire bifurquer `HandleFire`, remplacer
`HandlePower`.

```csharp
    private PowerId? _pendingPower;

    private async Task HandleFire(CoordinateDto target)
    {
        if (_pendingPower is { } powerId)
        {
            await ActivatePowerOn(powerId, target);
            return;
        }

        var before = Store.CurrentGame;
        Audio.Play("fire");
        await Store.FireAsync(target);

        PlayFireResult(before, Store.CurrentGame);
        if (Store.CurrentGame?.Status == GameStatus.Finished)
        {
            Navigation.NavigateTo("/result");
        }
    }

    private async Task ActivatePowerOn(PowerId powerId, CoordinateDto target)
    {
        _pendingPower = null;
        Audio.Play("sonar-ping");
        await Store.UsePowerAsync(powerId, new PowerTargetDto(target, null, null, null, null));
    }

    private void HandlePower(PowerId id)
    {
        // TargetKind.None (aucun pouvoir équipé pour l'instant n'utilise ce cas, mais le
        // pipeline générique doit rester correct) : on l'active immédiatement.
        // Tout pouvoir dont le TargetKind exige une case (Sonar : Area) attend le prochain
        // clic sur la grille adverse.
        _pendingPower = id;
    }
```

Ajouter juste avant `<PowerBar ... />`, dans le bloc `.battle-panel__side`, un indice visuel
pour ce ciblage en attente :

```razor
                        @if (_pendingPower is { } pending)
                        {
                            <p class="power-bar__targeting" role="status">
                                Ciblez une case sur la grille adverse pour @pending.
                            </p>
                        }
```

- [ ] **Step 2: Compiler**

Run: `dotnet build`
Expected: 0 avertissement, 0 erreur (Blazor WASM compile la page Razor).

- [ ] **Step 3: Vérification manuelle de bout en bout**

```bash
dotnet run --project src/Naval.Api &
dotnet run --project src/Naval.App &
```

Ouvrir `http://localhost:5018`, créer une partie solo, déployer, atteindre `Battle.razor` (le
back-end équipe Sonar par défaut, cf. Task 5), cliquer le bouton Sonar dans `PowerBar` (le texte
« Ciblez une case… » doit apparaître), cliquer une case de la grille adverse : l'énergie doit
diminuer de 3, le slot doit passer en `OnCooldown`, l'événement « Sonar : N case(s)
détectée(s)… » doit apparaître dans `EventLog`. Arrêter les deux serveurs une fois vérifié.

- [ ] **Step 4: Commit**

```bash
git add src/Naval.App/Pages/Battle.razor
git commit -m "feat(E-16): Battle.razor capture une case cible avant d'activer un pouvoir"
```

---

### Task 8: Vérification finale et journal

**Files:**
- Modify: `PROMPTS.md`

- [ ] **Step 1: Suite complète**

Run: `dotnet build && dotnet test`
Expected: 0 avertissement, tous les tests verts (comptage attendu : 47 existants + 2
(PowerRegistryTests) + 2 (PlayerStateTests) + 3 (SonarTests) + 2 (GameMapperTests) + 2
(GameServiceTests) = 58).

- [ ] **Step 2: `dotnet format` puis re-vérifier**

Run: `dotnet format && dotnet build && dotnet test`
Expected: aucune modification inattendue, toujours 0 avertissement et tous verts.

- [ ] **Step 3: Compléter l'entrée `PROMPTS.md`**

Suivre le format déjà utilisé dans les entrées précédentes (`Décision et justification`,
`Scénario de vérification`, `Résultat observé`, `Statut`). Couvrir au minimum : le choix de la
distance euclidienne pour le disque de Sonar (vs Chebyshev, à ne pas réutiliser pour un futur
pouvoir carré), le choix de faire gagner +1 énergie et de décrémenter les cooldowns au moment où
un joueur redevient actif (`GrantTurnStartBenefits`), et le choix d'équiper Sonar par défaut
quand `req.Powers` est vide plutôt que de bloquer la création de partie.

- [ ] **Step 4: Commit**

```bash
git add PROMPTS.md
git commit -m "docs: journalise l'implémentation du premier pouvoir de bout en bout"
```

---

## Self-Review

**Couverture du spec :** loadout minimal codé en dur (Task 5, `CreateGameAsync`) ✅ ; économie
+1/tour E-10 (Task 5, `GrantTurnStartBenefits`) ✅ ; `IPowerHandler`/`PowerRegistry`/`SonarHandler`
(Tasks 1 et 3) ✅ ; endpoint REST (Task 6) ✅ ; `GameMapper` sans fuite d'info sur le statut adverse
(Task 4) ✅ ; 3 tests Sonar nominal/refus/bord de grille (Task 3) ✅ ; front (Task 7) ✅ ; SignalR
explicitement hors scope, aucune tâche n'y touche ✅.

**Cohérence des types :** `PowerRegistry.Find` (Task 1) → utilisé tel quel dans
`GameEngine.ActivatePower` (Task 3) → `GameService.UsePowerAsync` reçoit `PowerRegistry` par
injection de constructeur (Task 5) → `Program.cs` l'enregistre en DI (Task 6). `PowerActivationResult`
défini Task 3, consommé Task 4 (`ToPowerResultDto`) et Task 5 sans changement de forme.
`PowerEffectResult`/`RevealedCell` définis Task 1, produits par `SonarHandler.Execute` Task 3,
mappés Task 4 — noms identiques partout.
