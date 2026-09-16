namespace Naval.Tests.Api;

using FluentAssertions;
using Naval.Api.Services;
using Naval.Shared.Contracts;
using Xunit;

public class GameServiceTests
{
    [Fact]
    public async Task CreateGameAsync_marks_the_ai_opponent_ready_in_single_player()
    {
        var service = new GameService(new InMemoryGameStore());
        var request = new CreateGameRequest("Joueur", GameMode.SinglePlayer, 10, 10, "Classic",
            AiLevel.Random, [], 0, null);

        var (game, _) = await service.CreateGameAsync(request, CancellationToken.None);

        game.Player2.IsReady.Should().BeTrue();
    }

    [Fact]
    public async Task PlaceFleetAsync_starts_the_battle_once_the_human_player_is_ready_in_single_player()
    {
        var service = new GameService(new InMemoryGameStore());
        var createRequest = new CreateGameRequest("Joueur", GameMode.SinglePlayer, 10, 10, "Classic",
            AiLevel.Random, [], 0, null);
        var (createdGame, token) = await service.CreateGameAsync(createRequest, CancellationToken.None);

        var placements = await service.SuggestRandomFleetAsync(createdGame.Id.Value, token, 42, CancellationToken.None);
        var game = await service.PlaceFleetAsync(createdGame.Id.Value, token,
            new PlaceFleetRequest(placements), CancellationToken.None);

        game.Status.Should().Be(GameStatus.InProgress);
    }
}
