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
}
