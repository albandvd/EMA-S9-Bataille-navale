namespace Naval.Tests.Api;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Naval.Api.Grpc;
using Naval.Api.Services;
using Naval.Shared.Contracts;
using Naval.Shared.Domain;
using Xunit;

/// <summary>
/// E-29 : couvre le volet gRPC du référentiel — un échange gRPC-Web réel (streaming serveur, en
/// passant par le même encodage HTTP qu'un navigateur via <see cref="GrpcWebHandler"/>) et les
/// deux erreurs attendues (partie inconnue, partie pas encore terminée). REST/SignalR gèrent le
/// jeu en direct (ADR-002) ; gRPC ne fait ici que rejouer un journal d'événements déjà produit
/// par <see cref="GameService"/>, sans dupliquer de règle.
/// </summary>
public class NavalReplayServiceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly WebApplicationFactory<Program> _factory;

    public NavalReplayServiceTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private NavalReplay.NavalReplayClient BuildClient() =>
        new(GrpcChannel.ForAddress(_factory.Server.BaseAddress, new GrpcChannelOptions
        {
            HttpHandler = new GrpcWebHandler(GrpcWebMode.GrpcWeb, _factory.Server.CreateHandler())
        }));

    [Fact]
    public async Task StreamReplay_of_a_finished_game_streams_its_full_event_log_over_grpc_web()
    {
        using var http = _factory.CreateClient();

        var createResponse = await http.PostAsJsonAsync("/api/games",
            new CreateGameRequest("Joueur", GameMode.SinglePlayer, 10, 10, "Classic", null, [], 0, null),
            JsonOptions);
        var created = (await createResponse.Content.ReadFromJsonAsync<CreateGameResponse>(JsonOptions))!;

        // Flotte adverse réduite à un seul destroyer connu, pour finir la partie en deux tirs
        // déterministes plutôt qu'en balayant toute la grille (même technique que GameServiceTests).
        var store = _factory.Services.GetRequiredService<IGameStore>();
        var game = (await store.GetAsync(created.GameId, CancellationToken.None))!;
        game.Player2.Fleet = new Fleet([
            new Ship("ai-destroyer", ShipType.Destroyer, 2, new Coordinate(0, 0), Orientation.Horizontal)
        ]);

        await PlaceRandomFleetAsync(http, created.GameId, created.PlayerToken);

        await FireAsync(http, created.GameId, created.PlayerToken, 0, 0);
        var last = await FireAsync(http, created.GameId, created.PlayerToken, 1, 0);
        last.GameOver.Should().BeTrue("le destroyer adverse ne fait que 2 cases");

        var client = BuildClient();
        var call = client.StreamReplay(new ReplayRequest { GameId = created.GameId.ToString() });

        var events = new List<ReplayEvent>();
        await foreach (var evt in call.ResponseStream.ReadAllAsync())
            events.Add(evt);

        events.Should().NotBeEmpty();
        events.Should().BeInAscendingOrder(e => e.Sequence);
        events.Should().Contain(e => e.Type == "ShotFired");
        events.Last().Type.Should().Be("GameOver");
    }

    [Fact]
    public async Task StreamReplay_of_a_game_still_in_progress_fails_with_FailedPrecondition()
    {
        using var http = _factory.CreateClient();

        var createResponse = await http.PostAsJsonAsync("/api/games",
            new CreateGameRequest("Joueur", GameMode.SinglePlayer, 10, 10, "Classic", null, [], 0, null),
            JsonOptions);
        var created = (await createResponse.Content.ReadFromJsonAsync<CreateGameResponse>(JsonOptions))!;

        await PlaceRandomFleetAsync(http, created.GameId, created.PlayerToken);

        var client = BuildClient();
        var call = client.StreamReplay(new ReplayRequest { GameId = created.GameId.ToString() });

        Func<Task> act = async () =>
        {
            await foreach (var _ in call.ResponseStream.ReadAllAsync()) { }
        };

        var exception = await act.Should().ThrowAsync<RpcException>();
        exception.Which.StatusCode.Should().Be(StatusCode.FailedPrecondition);
        exception.Which.Trailers.Get("code")?.Value.Should().Be(ErrorCodes.ReplayNotAvailable);
    }

    [Fact]
    public async Task StreamReplay_of_an_unknown_game_fails_with_NotFound()
    {
        var client = BuildClient();
        var call = client.StreamReplay(new ReplayRequest { GameId = Guid.NewGuid().ToString() });

        Func<Task> act = async () =>
        {
            await foreach (var _ in call.ResponseStream.ReadAllAsync()) { }
        };

        var exception = await act.Should().ThrowAsync<RpcException>();
        exception.Which.StatusCode.Should().Be(StatusCode.NotFound);
        exception.Which.Trailers.Get("code")?.Value.Should().Be(ErrorCodes.GameNotFound);
    }

    private static async Task PlaceRandomFleetAsync(HttpClient http, Guid gameId, string token)
    {
        using var randomRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{gameId}/fleet/random");
        randomRequest.Headers.Add("X-Player-Token", token);
        var randomResponse = await http.SendAsync(randomRequest);
        var random = await randomResponse.Content.ReadFromJsonAsync<RandomFleetResponseDto>(JsonOptions);

        using var placeRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{gameId}/fleet")
        {
            Content = JsonContent.Create(new PlaceFleetRequest(random!.Ships), options: JsonOptions)
        };
        placeRequest.Headers.Add("X-Player-Token", token);
        (await http.SendAsync(placeRequest)).EnsureSuccessStatusCode();
    }

    private static async Task<ShotResultDto> FireAsync(HttpClient http, Guid gameId, string token, int x, int y)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{gameId}/shots")
        {
            Content = JsonContent.Create(new FireRequest(new CoordinateDto(x, y)), options: JsonOptions)
        };
        request.Headers.Add("X-Player-Token", token);
        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ShotResultDto>(JsonOptions))!;
    }

    private sealed record RandomFleetResponseDto(IReadOnlyList<ShipPlacementDto> Ships);
}
