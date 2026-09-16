namespace Naval.Tests.App;

using FluentAssertions;
using Naval.App.Services;
using Naval.Shared.Contracts;
using Xunit;

public class FakeGameApiClientTests
{
    [Fact]
    public async Task CreateGameAsync_returns_board_matching_requested_grid_size()
    {
        var client = new FakeGameApiClient();
        var request = new CreateGameRequest("Joueur", GameMode.SinglePlayer, 12, 8, "Classic", AiLevel.Random, [], 0, null);

        var response = await client.CreateGameAsync(request, CancellationToken.None);
        var game = await client.GetGameAsync(response.GameId, response.PlayerToken, CancellationToken.None);

        game.Self.Board.Width.Should().Be(12);
        game.Self.Board.Height.Should().Be(8);
        game.Self.Board.Rows.Should().HaveCount(8);
        game.Opponent.TargetBoard.Rows.Should().OnlyContain(row => row.All(c => c == '.'));
    }

    [Fact]
    public async Task FireAsync_only_reveals_the_targeted_cell()
    {
        var client = new FakeGameApiClient();
        var createRequest = new CreateGameRequest("Joueur", GameMode.SinglePlayer, 5, 5, "Skirmish", AiLevel.Random, [], 0, null);
        var response = await client.CreateGameAsync(createRequest, CancellationToken.None);
        await client.PlaceFleetAsync(response.GameId, response.PlayerToken,
            new PlaceFleetRequest([new ShipPlacementDto(ShipType.Destroyer, new CoordinateDto(0, 0), Orientation.Horizontal)]),
            CancellationToken.None);

        await client.FireAsync(response.GameId, response.PlayerToken, new FireRequest(new CoordinateDto(2, 2)), CancellationToken.None);
        var game = await client.GetGameAsync(response.GameId, response.PlayerToken, CancellationToken.None);

        var touchedCells = game.Opponent.TargetBoard.Rows
            .SelectMany((row, y) => row.Select((c, x) => (x, y, c)))
            .Count(cell => cell.c != '.');

        touchedCells.Should().Be(1);
    }

    [Fact]
    public async Task FireAsync_marks_the_game_finished_once_the_hit_threshold_is_reached()
    {
        var client = new FakeGameApiClient();
        var createRequest = new CreateGameRequest("Joueur", GameMode.SinglePlayer, 5, 5, "Skirmish", AiLevel.Random, [], 0, null);
        var response = await client.CreateGameAsync(createRequest, CancellationToken.None);
        await client.PlaceFleetAsync(response.GameId, response.PlayerToken,
            new PlaceFleetRequest([new ShipPlacementDto(ShipType.Destroyer, new CoordinateDto(0, 0), Orientation.Horizontal)]),
            CancellationToken.None);

        // Le fake résout un coup sur trois en touché (_shotCounter % 3 == 0) : il faut donc 9
        // tirs distincts pour accumuler les 3 touchés qui déclenchent la victoire simulée.
        var targets = Enumerable.Range(0, 9).Select(i => new CoordinateDto(i % 5, i / 5)).ToList();

        ShotResultDto? lastResult = null;
        foreach (var target in targets)
        {
            lastResult = await client.FireAsync(response.GameId, response.PlayerToken,
                new FireRequest(target), CancellationToken.None);
        }

        var game = await client.GetGameAsync(response.GameId, response.PlayerToken, CancellationToken.None);

        game.Status.Should().Be(GameStatus.Finished);
        game.WinnerId.Should().Be(game.Self.PlayerId);
        lastResult.Should().NotBeNull();
        lastResult!.GameOver.Should().BeTrue();
        lastResult.WinnerId.Should().Be(game.Self.PlayerId);
    }

    [Fact]
    public async Task PlaceFleetAsync_paints_the_ship_cells_onto_the_player_s_own_board()
    {
        var client = new FakeGameApiClient();
        var createRequest = new CreateGameRequest("Joueur", GameMode.SinglePlayer, 5, 5, "Skirmish", AiLevel.Random, [], 0, null);
        var response = await client.CreateGameAsync(createRequest, CancellationToken.None);

        await client.PlaceFleetAsync(response.GameId, response.PlayerToken,
            new PlaceFleetRequest([new ShipPlacementDto(ShipType.Destroyer, new CoordinateDto(1, 2), Orientation.Horizontal)]),
            CancellationToken.None);

        var game = await client.GetGameAsync(response.GameId, response.PlayerToken, CancellationToken.None);

        game.Self.Board.Rows[2][1].Should().Be('S');
        game.Self.Board.Rows[2][2].Should().Be('S');
        game.Self.Board.Rows
            .SelectMany((row, y) => row.Select((c, x) => (x, y, c)))
            .Count(cell => cell.c == 'S')
            .Should().Be(2);
    }

    [Fact]
    public async Task GetPowerCatalogAsync_returns_unique_power_definitions()
    {
        var client = new FakeGameApiClient();

        var catalog = await client.GetPowerCatalogAsync(CancellationToken.None);

        catalog.Should().NotBeEmpty();
        catalog.Should().OnlyHaveUniqueItems(p => p.Id);
    }
}
