namespace Naval.Tests.Api;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Naval.Shared.Contracts;
using Xunit;

/// <summary>
/// E-04 : vérifie le hub de bout en bout contre un vrai serveur en mémoire (TestServer), pas
/// seulement contre GameService directement — c'est le seul moyen de prouver que le hub est
/// réellement câblé (groupes, DI, sérialisation SignalR) et pas seulement que la logique de jeu
/// qu'il délègue est correcte.
/// </summary>
public class GameHubTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly WebApplicationFactory<Program> _factory;

    public GameHubTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HubConnection BuildConnection() =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, GameHubMethods.Path), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

    [Fact]
    public async Task Fire_over_the_hub_notifies_both_players_of_the_shot_and_the_next_turn()
    {
        using var http = _factory.CreateClient();

        var createResponse = await http.PostAsJsonAsync("/api/games",
            new CreateGameRequest("Hôte", GameMode.PrivateOnline, 8, 8, "Skirmish", null, [], 0, null), JsonOptions);
        var created = (await createResponse.Content.ReadFromJsonAsync<CreateGameResponse>(JsonOptions))!;

        var joinResponse = await http.PostAsJsonAsync("/api/games/join",
            new JoinGameRequest("Invité", created.JoinCode, null, []), JsonOptions);
        var joined = (await joinResponse.Content.ReadFromJsonAsync<JoinGameResponse>(JsonOptions))!;

        await PlaceRandomFleetAsync(http, created.GameId, created.PlayerToken);
        await PlaceRandomFleetAsync(http, created.GameId, joined.PlayerToken);

        await using var hostHub = BuildConnection();
        await using var guestHub = BuildConnection();

        var guestSawShot = new TaskCompletionSource<ShotResultDto>();
        guestHub.On<ShotResultDto>(nameof(IGameClient.ShotResolved), r => guestSawShot.TrySetResult(r));

        var guestSawTurnChange = new TaskCompletionSource<int>();
        guestHub.On<Guid, DateTimeOffset?, int>(nameof(IGameClient.TurnChanged),
            (_, _, turnNumber) => guestSawTurnChange.TrySetResult(turnNumber));

        await hostHub.StartAsync();
        await guestHub.StartAsync();
        await hostHub.InvokeAsync(GameHubMethods.JoinGame, created.GameId, created.PlayerToken);
        await guestHub.InvokeAsync(GameHubMethods.JoinGame, created.GameId, joined.PlayerToken);

        await hostHub.InvokeAsync(GameHubMethods.Fire, 0, 0);

        var shot = await WaitAsync(guestSawShot.Task);
        shot.ShooterId.Should().NotBeEmpty();
        shot.Outcome.Should().NotBe(ShotOutcome.Rejected);

        var turnNumber = await WaitAsync(guestSawTurnChange.Task);
        turnNumber.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task SendEmote_over_the_hub_is_received_by_the_opponent()
    {
        using var http = _factory.CreateClient();

        var createResponse = await http.PostAsJsonAsync("/api/games",
            new CreateGameRequest("Hôte", GameMode.PrivateOnline, 8, 8, "Skirmish", null, [], 0, null), JsonOptions);
        var created = (await createResponse.Content.ReadFromJsonAsync<CreateGameResponse>(JsonOptions))!;

        var joinResponse = await http.PostAsJsonAsync("/api/games/join",
            new JoinGameRequest("Invité", created.JoinCode, null, []), JsonOptions);
        var joined = (await joinResponse.Content.ReadFromJsonAsync<JoinGameResponse>(JsonOptions))!;

        await using var hostHub = BuildConnection();
        await using var guestHub = BuildConnection();

        var guestSawEmote = new TaskCompletionSource<EmoteCode>();
        guestHub.On<Guid, EmoteCode>(nameof(IGameClient.EmoteReceived), (_, code) => guestSawEmote.TrySetResult(code));

        await hostHub.StartAsync();
        await guestHub.StartAsync();
        await hostHub.InvokeAsync(GameHubMethods.JoinGame, created.GameId, created.PlayerToken);
        await guestHub.InvokeAsync(GameHubMethods.JoinGame, created.GameId, joined.PlayerToken);

        await hostHub.InvokeAsync(GameHubMethods.SendEmote, EmoteCode.GoodLuck);

        var code = await WaitAsync(guestSawEmote.Task);
        code.Should().Be(EmoteCode.GoodLuck);
    }

    [Fact]
    public async Task Fire_over_the_hub_before_JoinGame_is_rejected_instead_of_crashing_the_connection()
    {
        await using var hub = BuildConnection();

        var rejection = new TaskCompletionSource<string>();
        hub.On<string, string>(nameof(IGameClient.ActionRejected), (code, _) => rejection.TrySetResult(code));

        await hub.StartAsync();
        await hub.InvokeAsync(GameHubMethods.Fire, 0, 0);

        var code = await WaitAsync(rejection.Task);
        code.Should().Be(ErrorCodes.NotAPlayer);
    }

    private static async Task PlaceRandomFleetAsync(HttpClient http, Guid gameId, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{gameId}/fleet/random");
        request.Headers.Add("X-Player-Token", token);
        var response = await http.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<RandomFleetSuggestion>(JsonOptions);

        using var placeRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{gameId}/fleet")
        {
            Content = JsonContent.Create(new PlaceFleetRequest(body!.Ships), options: JsonOptions)
        };
        placeRequest.Headers.Add("X-Player-Token", token);
        await http.SendAsync(placeRequest);
    }

    private sealed record RandomFleetSuggestion(IReadOnlyList<ShipPlacementDto> Ships);

    private static async Task<T> WaitAsync<T>(Task<T> task)
    {
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10)));
        completed.Should().Be(task, "l'événement SignalR attendu n'est jamais arrivé sous 10 s");
        return await task;
    }
}
