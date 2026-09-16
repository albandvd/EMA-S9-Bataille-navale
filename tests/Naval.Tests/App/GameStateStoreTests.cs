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

    [Fact]
    public async Task JoinGameAsync_populates_LastError_on_failure_and_notifies_once()
    {
        var store = new GameStateStore(new FakeGameApiClient());
        var notifications = 0;
        store.StateChanged += () => notifications++;

        var request = new JoinGameRequest("Joueur", "ABC123", null, []);
        await store.JoinGameAsync(request);

        store.LastError.Should().NotBeNull();
        store.LastError!.Code.Should().Be(ErrorCodes.GameNotFound);
        notifications.Should().Be(1);
    }

    [Fact]
    public async Task FireAsync_refreshes_CurrentGame_on_success()
    {
        var store = new GameStateStore(new FakeGameApiClient());

        var createRequest = new CreateGameRequest("Joueur", GameMode.SinglePlayer, 5, 5, "Skirmish", AiLevel.Random, [], 0, null);
        await store.CreateGameAsync(createRequest);

        var placeRequest = new PlaceFleetRequest(
            [new ShipPlacementDto(ShipType.Destroyer, new CoordinateDto(0, 0), Orientation.Horizontal)]);
        await store.PlaceFleetAsync(placeRequest);

        var turnBeforeFire = store.CurrentGame!.TurnNumber;
        await store.FireAsync(new CoordinateDto(2, 2));

        store.LastError.Should().BeNull();
        store.CurrentGame.Should().NotBeNull();
        store.CurrentGame!.TurnNumber.Should().BeGreaterThan(turnBeforeFire);
    }
}
