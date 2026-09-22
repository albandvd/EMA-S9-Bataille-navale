# Premier pouvoir de bout en bout — Sonar (P-01)

Statut : approuvé le 2026-09-22.

## Contexte

`docs/01-fonctionnalites.md` (E-10 à E-16) et `docs/02-pouvoirs.md` décrivent le système de
pouvoirs. Les contrats (`Naval.Shared/Contracts/{Requests,Responses,Enums,Realtime}.cs`) et le
front (`PowerBar.razor`, `GameStateStore.UsePowerAsync`, `GameApiClient` → `POST
/api/games/{id}/powers`) sont déjà entièrement câblés en attente d'un backend — y compris les
codes d'erreur (`INSUFFICIENT_ENERGY`, `POWER_ON_COOLDOWN`, `POWER_NOT_EQUIPPED`,
`POWER_EXHAUSTED`, `POWER_ALREADY_CHARGING`, `INVALID_TARGET`, `CARRIER_REQUIRED`,
`SHIP_ALREADY_DAMAGED`). Rien de tout ça n'existe côté `Naval.Shared/Domain` ni `Naval.Api`.

Objectif de cette tâche : implémenter le pipeline générique (économie d'énergie, cycle de vie
d'un slot de pouvoir, `IPowerHandler`, endpoint) avec **un seul pouvoir**, Sonar — le plus simple
du catalogue (`ChargeTurns = 0`, pas de porteur) — pour valider l'architecture avant d'enchaîner
sur les 22 autres pouvoirs dans des tâches séparées.

Hors scope : SignalR (pas de hub pour l'instant, REST seulement, cohérent avec l'existant),
sélection de loadout par l'UI (E-13 — loadout codé en dur pour cette tâche), tout pouvoir avec
`ChargeTurns ≥ 1` ou `RequiresCarrier`.

## Domain (`Naval.Shared/Domain/Powers/`)

- `IPowerHandler` — tel que spécifié dans `02-pouvoirs.md` §5 :
  ```csharp
  public interface IPowerHandler
  {
      PowerId Id { get; }
      string? Validate(Game game, PlayerState caster, PowerTargetDto target);
      IReadOnlyList<GameEvent> Execute(Game game, PlayerState caster, PlayerState target, PowerTargetDto powerTarget, int sequenceStart);
  }
  ```
  (Signature adaptée aux types déjà existants du repo — `GameState` n'existe pas, c'est `Game`.)

- `SonarHandler` : `TargetKind.Area`, `Radius = 4`. `Validate` exige `target.Cell` non nul et
  dans la grille. `Execute` compte les cases occupées de la flotte adverse dont la distance
  euclidienne au centre est ≤ 4 (cases hors grille simplement exclues du comptage, pas
  d'erreur) ; retourne un événement `PowerResolvedEvent` avec `RevealedCount` renseigné et
  `RevealedCells` vide.

- `PowerRegistry` : `sealed class` construite depuis `IEnumerable<IPowerHandler>`, indexée par
  `PowerId`. Pas de référence à `Microsoft.Extensions.DependencyInjection` dans
  `Naval.Shared` — c'est `Naval.Api/Program.cs` qui enregistre les handlers et construit le
  registre.

- `PlayerState` gagne :
  - `IReadOnlyList<PowerId> EquippedPowers` (fixé au constructeur, ≤ 3 conformément à
    `02-pouvoirs.md` §4.1 — non validé au-delà de la longueur pour cette tâche, la validation
    complète du loadout est E-13).
  - `List<PowerSlot> PowerSlots` — nouvelle classe Domain `PowerSlot { PowerId, PowerSlotStatus
    Status, int ChargeRemaining, int CooldownRemaining, int UsesLeft }`, un par pouvoir équipé,
    initialisé `Ready` / `0` / `0` / `MaxUses` (depuis `PowerCatalog`).

- `GameEngine.ActivatePower(Game game, PlayerState caster, PlayerState target, PowerId powerId, PowerTargetDto powerTarget, PowerRegistry registry)` :
  1. Vérifie que c'est le tour du joueur (`GameNotInProgress` / `NotYourTurn`, mêmes codes que
     `Fire`).
  2. Vérifie `EquippedPowers.Contains(powerId)` → sinon `PowerNotEquipped`.
  3. Récupère le slot ; vérifie `Status == Ready` (sinon `PowerAlreadyCharging` si `Charging`,
     `PowerOnCooldown` si cooldown > 0, `PowerExhausted` si `UsesLeft == 0`).
  4. Vérifie `caster.Energy >= definition.EnergyCost` → sinon `InsufficientEnergy`.
  5. Appelle `handler.Validate` (erreurs spécifiques au pouvoir → `InvalidTarget` /
     `CarrierRequired` / `ShipAlreadyDamaged` selon le message).
  6. Déduit l'énergie, appelle `handler.Execute`, pose `CooldownRemaining = definition.Cooldown`,
     décrémente `UsesLeft` si `MaxUses >= 0`. Pour `ChargeTurns == 0` (le seul cas couvert ici),
     le slot reste `Ready` après résolution (pas de passage par `Charging`/`Armed`) et **le tour
     n'est pas consommé** — le joueur peut encore tirer. Le cas `ChargeTurns ≥ 1` (cycle
     `Charging → Armed`, porteur, tour consommé) est explicitement hors scope, à traiter pouvoir
     par pouvoir.
  7. Émet `PowerActivatedEvent` puis `PowerResolvedEvent`.

- Énergie **+1/tour** (E-10, jamais implémenté jusqu'ici — actuellement seuls les +2/+3 sur
  touche/coulé de `GameEngine.ExecuteShot` existent) : ajoutée dans
  `GameService.AdvanceTurn`/`StartBattle`, au joueur qui devient actif, avec un
  `EnergyChangedEvent`.

- Nouveaux types dans `Naval.Shared/Domain/Events/GameEvent.cs` :
  `PowerActivatedEvent(PowerId, ChargeTurns)`, `PowerResolvedEvent(PowerId, RevealedCount,
  Message)`, `EnergyChangedEvent(PlayerId, int Delta, int Total)`.

## API

- `Naval.Api/Endpoints/PowerEndpoints.cs` : `POST /api/games/{gameId}/powers` avec
  `UsePowerRequest`, même patron que `ShotEndpoints.Fire` (token, délégation à
  `GameService`, ≤ 15 lignes). Retourne `PowerResultDto`.
- `GameService.UsePowerAsync(Guid gameId, string playerToken, UsePowerRequest req, CancellationToken ct)` :
  verrou `game.Lock`, résout joueur/adversaire, appelle `GameEngine.ActivatePower`, persiste,
  mappe en `PowerResultDto` via `GameMapper`.
- `GameMapper.ToSelfViewDto` / `ToOpponentViewDto` : remplacent les `Powers: []` /
  `EquippedPowers: []` actuels par le vrai état (`PowerSlotDto` avec `CanAffordNow` calculé côté
  mapper : `Status == Ready && Energy >= EnergyCost && UsesLeft != 0`). Vue adverse : uniquement
  la liste des `PowerId` équipés, jamais leur statut (pas de fuite d'information sur le
  cooldown/charge adverse au-delà de ce que `OpponentChargeDto` prévoit déjà).
- `GameService.CreateGameAsync` : si `req.Powers` est vide, équipe `[PowerId.Sonar]` par défaut
  pour les deux joueurs (y compris l'IA) ; sinon utilise `req.Powers` tel quel (déjà transmis,
  jamais stocké jusqu'ici).
- `Program.cs` : `AddSingleton<IPowerHandler, SonarHandler>()`, `AddSingleton<PowerRegistry>()`.

## Erreurs

Tous les codes utilisés existent déjà dans `ErrorCodes` (`Realtime.cs`) — aucun ajout de
contrat nécessaire. `GameException` existant suffit ; pas de nouveau cas HTTP (409 pour
`NotYourTurn`/cooldown/charge en cours par cohérence avec le tir, 400 pour le reste).

## Tests (`tests/Naval.Tests/Domain/Powers/SonarTests.cs`)

Les 3 cas exigés par `CLAUDE.md` :
1. **Nominal** — `GameBuilder` en bataille, flotte adverse avec 3 cases dans le disque de rayon
   4 autour de la cible → `RevealedCount == 3`, énergie du lanceur diminuée de 3 (coût), slot en
   cooldown 3, `RevealedCells` vide.
2. **Refus** — énergie insuffisante (`Energy < 3`) → `GameException` avec
   `ErrorCodes.InsufficientEnergy`, aucun état modifié (énergie, cooldown, board inchangés).
3. **Bord de grille** — cible en coin `(0,0)` sur une grille 10×10, rayon 4 : seules les cases
   in-bounds sont comptées, pas d'exception d'index hors bornes.

Complété par un test d'intégration léger dans `GameServiceTests.cs` : `POST` implicite via
`GameService.UsePowerAsync` de bout en bout (création → déploiement → activation Sonar),
vérifiant que `GameMapper` reflète le nouveau statut du slot.

## Risques / décisions à trancher pendant l'implémentation

- Distance euclidienne vs Chebyshev pour le "disque de rayon 4" : la spec parle de "disque",
  donc euclidienne (`dx² + dy² ≤ radius²}`) — à documenter en commentaire XML sur
  `SonarHandler` pour que le prochain pouvoir de zone (Frappe orbitale, carré 5×5) ne réutilise
  pas la même fonction par erreur.
- Gain d'énergie au tout premier tour (`StartBattle`) : les deux joueurs gagnent +1 avant même
  d'avoir joué, pour rester symétrique avec `AdvanceTurn`. À confirmer que ça ne casse aucun
  test existant sur `TurnOrderTests`/`GameOverTests` qui vérifierait `Energy == 0` en début de
  partie.
