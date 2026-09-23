namespace Naval.Tests.Api;

using FluentAssertions;
using Naval.Api.Services;
using Naval.Shared.Contracts;
using Naval.Shared.Domain;
using Naval.Shared.Domain.Powers;
using Xunit;

public class GameServiceTests
{
    [Fact]
    public async Task CreateGameAsync_marks_the_ai_opponent_ready_in_single_player()
    {
        var service = new GameService(new InMemoryGameStore(), new PowerRegistry([new SonarHandler()]));
        var request = new CreateGameRequest("Joueur", GameMode.SinglePlayer, 10, 10, "Classic",
            AiLevel.Random, [], 0, null);

        var (game, _) = await service.CreateGameAsync(request, CancellationToken.None);

        game.Player2.IsReady.Should().BeTrue();
    }

    [Fact]
    public async Task CreateGameAsync_reports_powers_enabled_even_when_powers_defaults_to_sonar()
    {
        var service = new GameService(new InMemoryGameStore(), new PowerRegistry([new SonarHandler()]));
        var request = new CreateGameRequest("Joueur", GameMode.SinglePlayer, 10, 10, "Classic",
            AiLevel.Random, [], 0, null); // Powers vide → loadout par défaut [Sonar]

        var (game, _) = await service.CreateGameAsync(request, CancellationToken.None);

        game.PowersEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task PlaceFleetAsync_starts_the_battle_once_the_human_player_is_ready_in_single_player()
    {
        var service = new GameService(new InMemoryGameStore(), new PowerRegistry([new SonarHandler()]));
        var createRequest = new CreateGameRequest("Joueur", GameMode.SinglePlayer, 10, 10, "Classic",
            AiLevel.Random, [], 0, null);
        var (createdGame, token) = await service.CreateGameAsync(createRequest, CancellationToken.None);

        var placements = await service.SuggestRandomFleetAsync(createdGame.Id.Value, token, 42, CancellationToken.None);
        var game = await service.PlaceFleetAsync(createdGame.Id.Value, token,
            new PlaceFleetRequest(placements), CancellationToken.None);

        game.Status.Should().Be(GameStatus.InProgress);
    }

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

    [Fact]
    public async Task UsePowerAsync_rejects_insufficient_energy_as_a_conflict()
    {
        var service = new GameService(new InMemoryGameStore(), new PowerRegistry([new SonarHandler()]));
        var createRequest = new CreateGameRequest("Joueur", GameMode.SinglePlayer, 10, 10, "Classic",
            AiLevel.Random, [PowerId.Sonar], 0, 42);
        var (createdGame, token) = await service.CreateGameAsync(createRequest, CancellationToken.None);
        var placements = await service.SuggestRandomFleetAsync(createdGame.Id.Value, token, 42, CancellationToken.None);
        await service.PlaceFleetAsync(createdGame.Id.Value, token, new PlaceFleetRequest(placements), CancellationToken.None);
        createdGame.Player1.Energy = 0; // insuffisant pour le coût de Sonar

        var target = new PowerTargetDto(new CoordinateDto(5, 5), null, null, null, null);
        var act = () => service.UsePowerAsync(createdGame.Id.Value, token,
            new UsePowerRequest(PowerId.Sonar, target), CancellationToken.None);

        var thrown = (await act.Should().ThrowAsync<GameException>()).Which;
        thrown.Code.Should().Be(ErrorCodes.InsufficientEnergy);
        thrown.IsConflict.Should().BeTrue(); // 409, conformément à contracts/openapi.yaml
    }

    [Fact]
    public async Task UsePowerAsync_rejects_a_missing_target_as_invalid_target()
    {
        var service = new GameService(new InMemoryGameStore(), new PowerRegistry([new SonarHandler()]));
        var createRequest = new CreateGameRequest("Joueur", GameMode.SinglePlayer, 10, 10, "Classic",
            AiLevel.Random, [PowerId.Sonar], 0, 42);
        var (createdGame, token) = await service.CreateGameAsync(createRequest, CancellationToken.None);
        var placements = await service.SuggestRandomFleetAsync(createdGame.Id.Value, token, 42, CancellationToken.None);
        await service.PlaceFleetAsync(createdGame.Id.Value, token, new PlaceFleetRequest(placements), CancellationToken.None);
        createdGame.Player1.Energy = 5;

        var act = () => service.UsePowerAsync(createdGame.Id.Value, token,
            new UsePowerRequest(PowerId.Sonar, null!), CancellationToken.None);

        var thrown = (await act.Should().ThrowAsync<GameException>()).Which;
        thrown.Code.Should().Be(ErrorCodes.InvalidTarget);
        thrown.IsConflict.Should().BeFalse(); // 400 : requête malformée, pas un conflit d'état
    }

    [Fact]
    public async Task UsePowerAsync_heavy_bomb_consumes_the_turn_and_hands_over_to_the_opponent()
    {
        var (service, game, token) = await StartBattleWithHeavyBombAsync();
        game.Player2.Fleet = new Fleet([
            new Ship("ai-destroyer", ShipType.Destroyer, 2, new Coordinate(0, 0), Orientation.Horizontal),
            new Ship("ai-cruiser", ShipType.Cruiser, 3, new Coordinate(9, 0), Orientation.Vertical)]);

        var target = new PowerTargetDto(new CoordinateDto(5, 5), null, null, null, null);
        var (result, after) = await service.UsePowerAsync(game.Id.Value, token,
            new UsePowerRequest(PowerId.HeavyBomb, target), CancellationToken.None);

        result.Shots.Should().HaveCount(9).And.OnlyContain(s => s.Outcome == ShotOutcome.Miss);
        result.NextPlayerId.Should().Be(after.Player2.Id.Value);
        result.GameOver.Should().BeFalse();
        after.CurrentPlayerId.Should().Be(after.Player2.Id);
        after.TurnNumber.Should().Be(2);
    }

    [Fact]
    public async Task UsePowerAsync_heavy_bomb_that_destroys_the_last_ship_ends_the_game()
    {
        var (service, game, token) = await StartBattleWithHeavyBombAsync();
        game.Player2.Fleet = new Fleet([
            new Ship("ai-destroyer", ShipType.Destroyer, 2, new Coordinate(4, 5), Orientation.Horizontal)]);

        var target = new PowerTargetDto(new CoordinateDto(5, 5), null, null, null, null);
        var (result, after) = await service.UsePowerAsync(game.Id.Value, token,
            new UsePowerRequest(PowerId.HeavyBomb, target), CancellationToken.None);

        result.GameOver.Should().BeTrue();
        result.WinnerId.Should().Be(after.Player1.Id.Value);
        result.NextPlayerId.Should().BeNull();
        after.Status.Should().Be(GameStatus.Finished);
    }

    private static async Task<(GameService service, Game game, string token)> StartBattleWithHeavyBombAsync()
    {
        var service = new GameService(new InMemoryGameStore(),
            new PowerRegistry([new SonarHandler(), new HeavyBombHandler()]));
        var createRequest = new CreateGameRequest("Joueur", GameMode.SinglePlayer, 10, 10, "Classic",
            AiLevel.Random, [PowerId.HeavyBomb], 0, 42);
        var (game, token) = await service.CreateGameAsync(createRequest, CancellationToken.None);
        var placements = await service.SuggestRandomFleetAsync(game.Id.Value, token, 42, CancellationToken.None);
        await service.PlaceFleetAsync(game.Id.Value, token, new PlaceFleetRequest(placements), CancellationToken.None);
        game.Player1.Energy = 10; // coût de la Bombe lourde
        return (service, game, token);
    }

    // ─── E-01/E-02 : présence initiale avant qu'un adversaire ne rejoigne ───

    [Fact]
    public async Task CreateGameAsync_leaves_the_placeholder_opponent_disconnected_in_online_modes()
    {
        var service = new GameService(new InMemoryGameStore(), new PowerRegistry([new SonarHandler()]));
        var request = new CreateGameRequest("Hôte", GameMode.PrivateOnline, 10, 10, "Classic",
            null, [], 0, null);

        var (game, _) = await service.CreateGameAsync(request, CancellationToken.None);

        game.Player2.IsConnected.Should().BeFalse();
    }

    // ─── E-07 : timer de tour ───

    private static async Task<(GameService service, IGameStore store, Guid gameId)> CreateInProgressSinglePlayerGameAsync(
        int turnTimeoutSeconds)
    {
        var store = new InMemoryGameStore();
        var service = new GameService(store, new PowerRegistry([new SonarHandler()]));
        var createRequest = new CreateGameRequest("Joueur", GameMode.SinglePlayer, 10, 10, "Classic",
            AiLevel.Random, [], turnTimeoutSeconds, null);
        var (createdGame, token) = await service.CreateGameAsync(createRequest, CancellationToken.None);

        var placements = await service.SuggestRandomFleetAsync(createdGame.Id.Value, token, 7, CancellationToken.None);
        await service.PlaceFleetAsync(createdGame.Id.Value, token, new PlaceFleetRequest(placements), CancellationToken.None);

        return (service, store, createdGame.Id.Value);
    }

    private static async Task ExpireCurrentTurnAsync(IGameStore store, Guid gameId)
    {
        var game = await store.GetAsync(gameId, CancellationToken.None);
        game!.TurnDeadlineUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
        await store.SaveAsync(game, CancellationToken.None);
    }

    [Fact]
    public async Task PlaceFleetAsync_sets_a_turn_deadline_when_a_timeout_is_configured()
    {
        var (_, store, gameId) = await CreateInProgressSinglePlayerGameAsync(turnTimeoutSeconds: 30);

        var game = await store.GetAsync(gameId, CancellationToken.None);

        game!.TurnDeadlineUtc.Should().NotBeNull();
        game.TurnDeadlineUtc.Should().BeCloseTo(DateTimeOffset.UtcNow.AddSeconds(30), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task PlaceFleetAsync_leaves_the_turn_deadline_null_when_the_timeout_is_disabled()
    {
        var (_, store, gameId) = await CreateInProgressSinglePlayerGameAsync(turnTimeoutSeconds: 0);

        var game = await store.GetAsync(gameId, CancellationToken.None);

        game!.TurnDeadlineUtc.Should().BeNull();
    }

    [Fact]
    public async Task PlayTimeoutShotAsync_does_nothing_before_the_deadline()
    {
        var (service, _, gameId) = await CreateInProgressSinglePlayerGameAsync(turnTimeoutSeconds: 30);

        var result = await service.PlayTimeoutShotAsync(gameId, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task PlayTimeoutShotAsync_fires_a_random_shot_once_the_deadline_has_passed()
    {
        var (service, store, gameId) = await CreateInProgressSinglePlayerGameAsync(turnTimeoutSeconds: 30);
        await ExpireCurrentTurnAsync(store, gameId);

        var result = await service.PlayTimeoutShotAsync(gameId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Outcome.Should().NotBe(ShotOutcome.Rejected);
    }

    [Fact]
    public async Task PlayTimeoutShotAsync_never_fires_on_the_ai_s_behalf()
    {
        // En solo, le tour de l'IA se résout de façon synchrone : sa deadline ne devrait
        // jamais être scrutée par le timer, mais on vérifie le garde-fou explicitement.
        var (service, store, gameId) = await CreateInProgressSinglePlayerGameAsync(turnTimeoutSeconds: 30);
        await ExpireCurrentTurnAsync(store, gameId);
        await service.PlayTimeoutShotAsync(gameId, CancellationToken.None); // tour du joueur humain écoulé
        await ExpireCurrentTurnAsync(store, gameId); // le tour est maintenant à l'IA

        var result = await service.PlayTimeoutShotAsync(gameId, CancellationToken.None);

        result.Should().BeNull();
    }

    // ─── E-05 : présence & déconnexion ───

    private static async Task<(GameService service, Guid gameId, Guid hostId, Guid guestId)> CreateOnlineBattleAsync()
    {
        var service = new GameService(new InMemoryGameStore(), new PowerRegistry([new SonarHandler()]));
        var createRequest = new CreateGameRequest("Hôte", GameMode.PrivateOnline, 8, 8, "Skirmish",
            null, [], 0, null);
        var (created, hostToken) = await service.CreateGameAsync(createRequest, CancellationToken.None);

        var joinRequest = new JoinGameRequest("Invité", created.JoinCode, null, []);
        var (joined, guestToken) = await service.JoinGameAsync(joinRequest, CancellationToken.None);

        var hostPlacements = await service.SuggestRandomFleetAsync(joined.Id.Value, hostToken, 1, CancellationToken.None);
        await service.PlaceFleetAsync(joined.Id.Value, hostToken, new PlaceFleetRequest(hostPlacements), CancellationToken.None);
        var guestPlacements = await service.SuggestRandomFleetAsync(joined.Id.Value, guestToken, 2, CancellationToken.None);
        var final = await service.PlaceFleetAsync(joined.Id.Value, guestToken, new PlaceFleetRequest(guestPlacements), CancellationToken.None);

        return (service, final.Id.Value, final.Player1.Id.Value, final.Player2.Id.Value);
    }

    [Fact]
    public async Task SetPresenceAsync_toggles_connection_state()
    {
        var (service, gameId, hostId, _) = await CreateOnlineBattleAsync();

        await service.SetPresenceAsync(gameId, hostId, connected: false, CancellationToken.None);
        var view = await service.GetSpectatorViewAsync(gameId, CancellationToken.None);
        view.Player1.IsConnected.Should().BeFalse();

        await service.SetPresenceAsync(gameId, hostId, connected: true, CancellationToken.None);
        view = await service.GetSpectatorViewAsync(gameId, CancellationToken.None);
        view.Player1.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task AbandonDueToDisconnectionAsync_ends_the_game_when_the_player_is_still_disconnected()
    {
        var (service, gameId, hostId, guestId) = await CreateOnlineBattleAsync();
        await service.SetPresenceAsync(gameId, hostId, connected: false, CancellationToken.None);

        var result = await service.AbandonDueToDisconnectionAsync(gameId, hostId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.WinnerId.Should().Be(guestId);
        result.Reason.Should().Be("Disconnected");
    }

    [Fact]
    public async Task AbandonDueToDisconnectionAsync_does_nothing_once_the_player_has_reconnected()
    {
        var (service, gameId, hostId, _) = await CreateOnlineBattleAsync();
        await service.SetPresenceAsync(gameId, hostId, connected: false, CancellationToken.None);
        await service.SetPresenceAsync(gameId, hostId, connected: true, CancellationToken.None); // reconnexion avant l'échéance

        var result = await service.AbandonDueToDisconnectionAsync(gameId, hostId, CancellationToken.None);

        result.Should().BeNull();
        var view = await service.GetSpectatorViewAsync(gameId, CancellationToken.None);
        view.Status.Should().Be(GameStatus.InProgress);
    }

    // ─── E-09 : spectateur ───

    [Fact]
    public async Task GetSpectatorViewAsync_never_reveals_an_undiscovered_ship()
    {
        var (service, gameId, _, guestId) = await CreateOnlineBattleAsync();

        var view = await service.GetSpectatorViewAsync(gameId, CancellationToken.None);

        view.Player1.Board.Rows.Should().OnlyContain(row => row.All(c => c == '.'));
        view.Player2.Board.Rows.Should().OnlyContain(row => row.All(c => c == '.'));
        view.Player1.SunkShips.Should().BeEmpty();
        view.Player2.SunkShips.Should().BeEmpty();
        view.CurrentPlayerId.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSpectatorViewAsync_reflects_a_shot_once_it_has_been_fired()
    {
        var service = new GameService(new InMemoryGameStore(), new PowerRegistry([new SonarHandler()]));
        var createRequest = new CreateGameRequest("Joueur", GameMode.SinglePlayer, 5, 5, "Skirmish",
            AiLevel.Random, [], 0, null);
        var (created, token) = await service.CreateGameAsync(createRequest, CancellationToken.None);
        var placements = await service.SuggestRandomFleetAsync(created.Id.Value, token, 9, CancellationToken.None);
        await service.PlaceFleetAsync(created.Id.Value, token, new PlaceFleetRequest(placements), CancellationToken.None);

        var (shotResult, _) = await service.FireAsync(created.Id.Value, token, new FireRequest(new CoordinateDto(0, 0)), CancellationToken.None);

        var view = await service.GetSpectatorViewAsync(created.Id.Value, CancellationToken.None);
        var opponentBoard = view.Player2.Board.Rows;
        var markedCells = opponentBoard.Sum(row => row.Count(c => c != '.'));

        markedCells.Should().Be(1);
        shotResult.Outcome.Should().NotBe(ShotOutcome.Rejected);
    }
}
