using Naval.Shared.Contracts;
using Naval.Shared.Contracts.Mapping;
using Naval.Shared.Domain;
using Naval.Shared.Domain.Ai;
using Naval.Shared.Domain.Events;
using Naval.Shared.Domain.Powers;

namespace Naval.Api.Services;

/// <summary>
/// Orchestrateur sans état : reçoit les commandes validées, délègue au GameEngine,
/// persiste et retourne les DTO. Aucune règle de jeu ici.
/// </summary>
public sealed class GameService
{
    private static readonly char[] JoinCodeAlphabet =
        "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

    private readonly IGameStore _store;
    private readonly PowerRegistry _powers;

    public GameService(IGameStore store, PowerRegistry powers)
    {
        _store = store;
        _powers = powers;
    }

    // ─── Création ───

    public async Task<(Game game, string playerToken)> CreateGameAsync(
        CreateGameRequest req, CancellationToken ct)
    {
        var preset = FleetPresets.Find(req.FleetPreset)
            ?? throw new GameException(ErrorCodes.ValidationFailed,
                $"Preset de flotte inconnu : {req.FleetPreset}.");

        var gameId = GameId.New();
        var p1Id = PlayerId.New();
        var p1Token = Guid.NewGuid().ToString();
        string? joinCode = null;

        var rng = req.Seed.HasValue ? new Random(req.Seed.Value) : Random.Shared;

        if (req.Mode == GameMode.PrivateOnline)
            joinCode = GenerateJoinCode(rng);

        var equippedPowers = req.Powers.Count > 0 ? req.Powers : (IReadOnlyList<PowerId>)[PowerId.Sonar];

        var p1 = new PlayerState(p1Id, req.PlayerName, PlayerSlot.One, p1Token,
            req.GridWidth, req.GridHeight, equippedPowers: equippedPowers);

        PlayerState p2;
        if (req.Mode == GameMode.SinglePlayer)
        {
            var aiId = PlayerId.New();
            p2 = new PlayerState(aiId, "IA", PlayerSlot.Two, Guid.NewGuid().ToString(),
                req.GridWidth, req.GridHeight, isAi: true, equippedPowers: equippedPowers);
        }
        else
        {
            var p2Id = PlayerId.New();
            p2 = new PlayerState(p2Id, "En attente…", PlayerSlot.Two,
                Guid.NewGuid().ToString(), req.GridWidth, req.GridHeight, equippedPowers: equippedPowers);
        }

        var game = new Game(
            gameId, req.Mode, joinCode, req.FleetPreset,
            req.GridWidth, req.GridHeight,
            powersEnabled: equippedPowers.Count > 0,
            turnTimeoutSeconds: req.TurnTimeoutSeconds,
            p1, p2);

        if (req.Mode == GameMode.SinglePlayer)
        {
            var aiPlacements = GameEngine.GenerateRandomPlacement(preset, req.GridWidth, req.GridHeight, rng);
            var (aiFleet, _) = GameEngine.ValidateAndBuildFleet(aiPlacements, preset, req.GridWidth, req.GridHeight, "ai");
            p2.Fleet = aiFleet;
            p2.IsReady = true;
        }

        await _store.SaveAsync(game, ct);
        return (game, p1Token);
    }

    // ─── Rejoindre ───

    public async Task<(Game game, string playerToken)> JoinGameAsync(
        JoinGameRequest req, CancellationToken ct)
    {
        Game? game = null;

        if (req.JoinCode is not null)
            game = await _store.FindByJoinCodeAsync(req.JoinCode, ct);
        else if (req.GameId.HasValue)
            game = await _store.GetAsync(req.GameId.Value, ct);

        if (game is null)
            throw new GameException(ErrorCodes.GameNotFound, "Partie introuvable.");

        await game.Lock.WaitAsync(ct);
        try
        {
            if (game.Status != GameStatus.AwaitingOpponent)
                throw new GameException(ErrorCodes.GameFull, "Cette partie n'est plus disponible.");

            // Met à jour le slot 2 avec les infos du joueur rejoignant
            var p2Token = Guid.NewGuid().ToString();
            game.Player2.Name = req.PlayerName;
            game.Player2.Token = p2Token;
            game.Player2.IsConnected = true;

            // Note : req.Powers (le loadout du joueur qui rejoint) n'est pas encore appliqué ici — le
            // joueur 2 garde le loadout par défaut posé à la création (CreateGameAsync). Transférer le
            // vrai loadout du joueur qui rejoint suppose de rendre PlayerState.EquippedPowers modifiable
            // après construction ; à traiter avec E-13 (sélection de loadout complète).

            game.Status = GameStatus.AwaitingDeployment;

            game.AddEvent(new PlayerReadyEvent(
                game.NextSequence(), DateTimeOffset.UtcNow, game.Player2.Id,
                $"{req.PlayerName} a rejoint la partie."));

            await _store.SaveAsync(game, ct);
            return (game, p2Token);
        }
        finally
        {
            game.Lock.Release();
        }
    }

    // ─── Placement de flotte ───

    public async Task<Game> PlaceFleetAsync(
        Guid gameId, string playerToken, PlaceFleetRequest req, CancellationToken ct)
    {
        var game = await RequireGameAsync(gameId, ct);
        var preset = FleetPresets.Find(game.FleetPreset)
            ?? throw new GameException(ErrorCodes.ValidationFailed, "Preset introuvable.");

        await game.Lock.WaitAsync(ct);
        try
        {
            var player = game.GetPlayerByToken(playerToken)
                ?? throw new GameException(ErrorCodes.NotAPlayer, "Token invalide.");

            if (player.Fleet is not null)
                throw new GameException(ErrorCodes.FleetAlreadyPlaced, "Flotte déjà déployée.");

            if (game.Status != GameStatus.AwaitingDeployment)
                throw new GameException(ErrorCodes.GameNotInProgress, "La partie n'attend pas de déploiement.");

            var (fleet, errors) = GameEngine.ValidateAndBuildFleet(
                req.Ships, preset, game.GridWidth, game.GridHeight, player.Slot == PlayerSlot.One ? "own" : "own");

            if (errors.Count > 0)
            {
                var code = errors[0].Kind switch
                {
                    PlacementErrorKind.OutOfBounds => ErrorCodes.OutOfBounds,
                    PlacementErrorKind.OverlappingShips => ErrorCodes.OverlappingShips,
                    _ => ErrorCodes.FleetIncomplete
                };
                throw new GameException(code, errors[0].Detail);
            }

            player.Fleet = fleet;
            player.IsReady = true;

            game.AddEvent(new PlayerReadyEvent(
                game.NextSequence(), DateTimeOffset.UtcNow, player.Id,
                $"{player.Name} a déployé sa flotte."));

            if (game.Player1.IsReady && game.Player2.IsReady)
                StartBattle(game);

            await _store.SaveAsync(game, ct);
            return game;
        }
        finally
        {
            game.Lock.Release();
        }
    }

    // ─── Placement aléatoire ───

    public async Task<IReadOnlyList<ShipPlacementDto>> SuggestRandomFleetAsync(
        Guid gameId, string playerToken, int? seed, CancellationToken ct)
    {
        var game = await RequireGameAsync(gameId, ct);
        _ = game.GetPlayerByToken(playerToken)
            ?? throw new GameException(ErrorCodes.NotAPlayer, "Token invalide.");

        var preset = FleetPresets.Find(game.FleetPreset)
            ?? throw new GameException(ErrorCodes.ValidationFailed, "Preset introuvable.");

        var rng = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        return GameEngine.GenerateRandomPlacement(preset, game.GridWidth, game.GridHeight, rng);
    }

    // ─── Tir ───

    public async Task<(ShotResultDto result, Game game)> FireAsync(
        Guid gameId, string playerToken, FireRequest req, CancellationToken ct)
    {
        var game = await RequireGameAsync(gameId, ct);

        await game.Lock.WaitAsync(ct);
        try
        {
            var shooter = game.GetPlayerByToken(playerToken)
                ?? throw new GameException(ErrorCodes.NotAPlayer, "Token invalide.");

            if (game.Status != GameStatus.InProgress)
                throw new GameException(ErrorCodes.GameNotInProgress, "La partie n'est pas en cours.",
                    isConflict: true);

            if (game.CurrentPlayerId != shooter.Id)
                throw new GameException(ErrorCodes.NotYourTurn, "Ce n'est pas votre tour.",
                    isConflict: true);

            var target = game.GetOpponent(shooter.Id);
            var coord = new Coordinate(req.Target.X, req.Target.Y);

            var (shotResult, errorCode) = GameEngine.ExecuteShot(shooter, target, coord);

            if (errorCode is not null)
                throw new GameException(errorCode, GetShotErrorMessage(errorCode),
                    isConflict: errorCode == ErrorCodes.CellAlreadyTargeted);

            game.TurnNumber++;
            game.AddEvent(new ShotFiredEvent(
                game.NextSequence(), DateTimeOffset.UtcNow, shooter.Id, coord, shotResult,
                FormatShotMessage(shooter.Name, coord, shotResult.Outcome)));

            Guid? nextPlayerId = null;
            if (GameEngine.IsGameOver(game))
            {
                var winner = GameEngine.FindWinner(game)!;
                game.Status = GameStatus.Finished;
                game.WinnerId = winner.Id.Value;
                game.CurrentPlayerId = null;

                game.AddEvent(new GameOverEvent(
                    game.NextSequence(), DateTimeOffset.UtcNow, winner.Id,
                    "FleetDestroyed",
                    $"Partie terminée. {winner.Name} remporte la victoire."));
            }
            else
            {
                AdvanceTurn(game, shooter.Id);
                nextPlayerId = game.CurrentPlayerId?.Value;
            }

            await _store.SaveAsync(game, ct);

            var dto = GameMapper.ToShotResultDto(game, shooter, coord, shotResult, nextPlayerId);
            return (dto, game);
        }
        finally
        {
            game.Lock.Release();
        }
    }

    // ─── Tour IA ───

    public async Task<ShotResultDto?> PlayAiTurnAsync(Guid gameId, CancellationToken ct)
    {
        var game = await _store.GetAsync(gameId, ct);
        if (game is null || game.Status != GameStatus.InProgress) return null;
        if (game.CurrentPlayerId != game.Player2.Id) return null;
        if (!game.Player2.IsAi) return null;

        await game.Lock.WaitAsync(ct);
        try
        {
            if (game.Status != GameStatus.InProgress) return null;
            if (game.CurrentPlayerId != game.Player2.Id) return null;

            IAiStrategy ai = new HuntTargetAi();
            var aiPlayer = game.Player2;
            var humanPlayer = game.Player1;

            var coord = ai.ChooseTarget(aiPlayer, humanPlayer);
            var (shotResult, _) = GameEngine.ExecuteShot(aiPlayer, humanPlayer, coord);

            game.TurnNumber++;
            game.AddEvent(new ShotFiredEvent(
                game.NextSequence(), DateTimeOffset.UtcNow, aiPlayer.Id, coord, shotResult,
                FormatShotMessage("IA", coord, shotResult.Outcome)));

            Guid? nextPlayerId = null;
            if (GameEngine.IsGameOver(game))
            {
                var winner = GameEngine.FindWinner(game)!;
                game.Status = GameStatus.Finished;
                game.WinnerId = winner.Id.Value;
                game.CurrentPlayerId = null;

                game.AddEvent(new GameOverEvent(
                    game.NextSequence(), DateTimeOffset.UtcNow, winner.Id,
                    "FleetDestroyed",
                    $"Partie terminée. {winner.Name} remporte la victoire."));
            }
            else
            {
                AdvanceTurn(game, aiPlayer.Id);
                nextPlayerId = game.CurrentPlayerId?.Value;
            }

            await _store.SaveAsync(game, ct);
            return GameMapper.ToShotResultDto(game, aiPlayer, coord, shotResult, nextPlayerId);
        }
        finally
        {
            game.Lock.Release();
        }
    }

    // ─── Abandon ───

    public async Task<GameOverDto> ForfeitAsync(Guid gameId, string playerToken, CancellationToken ct)
    {
        var game = await RequireGameAsync(gameId, ct);

        await game.Lock.WaitAsync(ct);
        try
        {
            var quitter = game.GetPlayerByToken(playerToken)
                ?? throw new GameException(ErrorCodes.NotAPlayer, "Token invalide.");

            if (game.Status == GameStatus.Finished || game.Status == GameStatus.Abandoned)
                throw new GameException(ErrorCodes.GameNotInProgress, "La partie est déjà terminée.", isConflict: true);

            var winner = game.GetOpponent(quitter.Id);
            game.Status = GameStatus.Abandoned;
            game.WinnerId = winner.Id.Value;
            game.CurrentPlayerId = null;

            game.AddEvent(new GameOverEvent(
                game.NextSequence(), DateTimeOffset.UtcNow, winner.Id,
                "Forfeit",
                $"{quitter.Name} a abandonné. {winner.Name} remporte la victoire."));

            await _store.SaveAsync(game, ct);
            return GameMapper.ToGameOverDto(game, "Forfeit");
        }
        finally
        {
            game.Lock.Release();
        }
    }

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

            if (req.Target is null)
                throw new GameException(ErrorCodes.InvalidTarget, "Cible du pouvoir manquante.");

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

    // ─── Lecture ───

    public async Task<GameStateDto> GetGameStateAsync(Guid gameId, string playerToken, CancellationToken ct)
    {
        var game = await RequireGameAsync(gameId, ct);
        var viewer = game.GetPlayerByToken(playerToken)
            ?? throw new GameException(ErrorCodes.NotAPlayer, "Token invalide.", isForbidden: true);

        return GameMapper.ToGameStateDto(game, viewer);
    }

    public async Task<IReadOnlyList<GameEventDto>> GetEventsAsync(
        Guid gameId, string playerToken, int since, CancellationToken ct)
    {
        var game = await RequireGameAsync(gameId, ct);
        _ = game.GetPlayerByToken(playerToken)
            ?? throw new GameException(ErrorCodes.NotAPlayer, "Token invalide.", isForbidden: true);

        return game.EventsSince(since)
            .Select(GameMapper.ToEventDto)
            .ToList();
    }

    public async Task<IReadOnlyList<OpenGameDto>> ListOpenGamesAsync(int limit, CancellationToken ct)
    {
        var summaries = await _store.ListOpenAsync(ct);
        return summaries.Take(limit).Select(GameMapper.ToOpenGameDto).ToList();
    }

    // ─── Helpers privés ───

    private async Task<Game> RequireGameAsync(Guid id, CancellationToken ct)
    {
        var game = await _store.GetAsync(id, ct);
        if (game is null)
            throw new GameException(ErrorCodes.GameNotFound, "Partie introuvable.");
        return game;
    }

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

    private static string GenerateJoinCode(Random rng)
    {
        return new string(Enumerable.Range(0, 6)
            .Select(_ => JoinCodeAlphabet[rng.Next(JoinCodeAlphabet.Length)])
            .ToArray());
    }

    private static string GetShotErrorMessage(string code) => code switch
    {
        ErrorCodes.OutOfBounds => "La coordonnée est hors de la grille.",
        ErrorCodes.CellAlreadyTargeted => "Vous avez déjà tiré sur cette case.",
        _ => "Tir invalide."
    };

    private static string GetPowerErrorMessage(string code) => code switch
    {
        ErrorCodes.PowerNotEquipped => "Ce pouvoir n'est pas équipé.",
        ErrorCodes.PowerAlreadyCharging => "Ce pouvoir est déjà en cours de charge.",
        ErrorCodes.PowerOnCooldown => "Ce pouvoir est en recharge.",
        ErrorCodes.PowerExhausted => "Ce pouvoir n'a plus de charges disponibles.",
        ErrorCodes.InsufficientEnergy => "Énergie insuffisante.",
        ErrorCodes.InvalidTarget => "Cible invalide pour ce pouvoir.",
        _ => "Activation de pouvoir refusée."
    };

    private static bool IsPowerConflictCode(string code) =>
        code is ErrorCodes.PowerAlreadyCharging or ErrorCodes.PowerOnCooldown or ErrorCodes.PowerExhausted
            or ErrorCodes.InsufficientEnergy;

    private static string FormatShotMessage(string shooterName, Coordinate coord, ShotOutcome outcome)
    {
        char col = (char)('A' + coord.X);
        int row = coord.Y + 1;
        return outcome switch
        {
            ShotOutcome.Miss => $"{shooterName} tire en {col}{row} — à l'eau.",
            ShotOutcome.Hit => $"{shooterName} tire en {col}{row} — touché !",
            ShotOutcome.Sunk => $"{shooterName} tire en {col}{row} — coulé !",
            _ => $"{shooterName} tire en {col}{row}."
        };
    }
}
