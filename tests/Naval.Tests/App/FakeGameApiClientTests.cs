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
    public async Task GetPowerCatalogAsync_returns_unique_power_definitions()
    {
        var client = new FakeGameApiClient();

        var catalog = await client.GetPowerCatalogAsync(CancellationToken.None);

        catalog.Should().NotBeEmpty();
        catalog.Should().OnlyHaveUniqueItems(p => p.Id);
    }
}
