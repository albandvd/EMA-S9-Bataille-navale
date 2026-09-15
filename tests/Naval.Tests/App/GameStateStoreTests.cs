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
