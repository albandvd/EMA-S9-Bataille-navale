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
}
