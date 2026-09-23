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

    // ─── E-04 : connexion au hub selon le mode ───

    [Fact]
    public async Task CreateGameAsync_in_an_online_mode_connects_the_hub()
    {
        var hub = new FakeGameHubClient();
        var store = new GameStateStore(new FakeGameApiClient(), hub);

        var request = new CreateGameRequest("Joueur", GameMode.PrivateOnline, 10, 10, "Classic", null, [], 30, null);
        await store.CreateGameAsync(request);

        hub.ConnectCalls.Should().ContainSingle();
        hub.ConnectCalls[0].GameId.Should().Be(store.CurrentGame!.GameId);
    }

    [Fact]
    public async Task CreateGameAsync_in_solo_mode_never_connects_the_hub()
    {
        var hub = new FakeGameHubClient();
        var store = new GameStateStore(new FakeGameApiClient(), hub);

        var request = new CreateGameRequest("Joueur", GameMode.SinglePlayer, 10, 10, "Classic", AiLevel.Random, [], 0, null);
        await store.CreateGameAsync(request);

        hub.ConnectCalls.Should().BeEmpty();
    }

    // ─── E-04/E-05/E-08 : réaction aux événements poussés par le hub ───

    private static async Task<(GameStateStore store, FakeGameHubClient hub)> CreateStoreWithOnlineGameAsync()
    {
        var hub = new FakeGameHubClient();
        var store = new GameStateStore(new FakeGameApiClient(), hub);
        var request = new CreateGameRequest("Joueur", GameMode.PrivateOnline, 10, 10, "Classic", null, [], 30, null);
        await store.CreateGameAsync(request);
        return (store, hub);
    }

    [Fact]
    public async Task Hub_GameStateChanged_replaces_CurrentGame_and_notifies()
    {
        var (store, hub) = await CreateStoreWithOnlineGameAsync();
        var notifications = 0;
        store.StateChanged += () => notifications++;

        var pushed = store.CurrentGame! with { TurnNumber = 99 };
        hub.RaiseGameStateChanged(pushed);

        store.CurrentGame!.TurnNumber.Should().Be(99);
        notifications.Should().Be(1);
    }

    [Fact]
    public async Task Hub_EmoteReceived_sets_and_clears_the_last_emote()
    {
        var (store, hub) = await CreateStoreWithOnlineGameAsync();

        hub.RaiseEmoteReceived(Guid.NewGuid(), EmoteCode.WellPlayed);
        store.LastReceivedEmote.Should().Be(EmoteCode.WellPlayed);

        store.ClearLastReceivedEmote();
        store.LastReceivedEmote.Should().BeNull();
    }

    [Fact]
    public async Task Hub_OpponentLeft_then_OpponentReconnected_toggles_the_grace_countdown()
    {
        var (store, hub) = await CreateStoreWithOnlineGameAsync();

        hub.RaiseOpponentLeft(60);
        store.OpponentGraceSecondsRemaining.Should().Be(60);

        hub.RaiseOpponentReconnected();
        store.OpponentGraceSecondsRemaining.Should().BeNull();
    }

    [Fact]
    public async Task Hub_ActionRejected_populates_LastError()
    {
        var (store, hub) = await CreateStoreWithOnlineGameAsync();

        hub.RaiseActionRejected(ErrorCodes.NotYourTurn, "Ce n'est pas votre tour.");

        store.LastError.Should().NotBeNull();
        store.LastError!.Code.Should().Be(ErrorCodes.NotYourTurn);
    }

    [Fact]
    public async Task Hub_ShotResolved_raises_the_store_s_ShotResolved_event()
    {
        var (store, hub) = await CreateStoreWithOnlineGameAsync();
        ShotResultDto? received = null;
        store.ShotResolved += r => received = r;

        var shot = new ShotResultDto(store.CurrentGame!.GameId, 1, Guid.NewGuid(),
            new CoordinateDto(0, 0), ShotOutcome.Hit, null, 2, null, false, null);
        hub.RaiseShotResolved(shot);

        received.Should().Be(shot);
    }

    [Fact]
    public async Task FireAsync_raises_the_ShotResolved_event_on_success()
    {
        var store = new GameStateStore(new FakeGameApiClient());
        await store.CreateGameAsync(new CreateGameRequest("Joueur", GameMode.SinglePlayer, 5, 5, "Skirmish",
            AiLevel.Random, [], 0, null));
        await store.PlaceFleetAsync(new PlaceFleetRequest(
            [new ShipPlacementDto(ShipType.Destroyer, new CoordinateDto(0, 0), Orientation.Horizontal)]));

        var raised = 0;
        store.ShotResolved += _ => raised++;

        await store.FireAsync(new CoordinateDto(2, 2));

        raised.Should().Be(1);
    }
}
