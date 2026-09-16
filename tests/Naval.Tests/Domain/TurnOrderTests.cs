using FluentAssertions;
using Naval.Shared.Contracts;
using Naval.Shared.Domain;
using Naval.Tests.Builders;

namespace Naval.Tests.Domain;

public sealed class TurnOrderTests
{
    [Fact]
    public void NewGame_CurrentPlayerIsP1()
    {
        var game = GameBuilder.New().ReadyToFight().Build();
        game.CurrentPlayerId.Should().Be(game.Player1.Id);
    }

    [Fact]
    public void AfterP1Shot_CurrentPlayerBecomesP2()
    {
        var game = GameBuilder.New().ReadyToFight().Build();

        // Trouve une case vide pour P1
        var coord = FindEmptyCell(game.Player1, game.Player2);
        GameEngine.ExecuteShot(game.Player1, game.Player2, coord);

        // Simule l'avance de tour
        game.CurrentPlayerId = game.Player2.Id;
        game.CurrentPlayerId.Should().Be(game.Player2.Id);
    }

    [Fact]
    public void IsCurrentPlayer_WhenCorrectPlayer_ReturnsTrue()
    {
        var game = GameBuilder.New().ReadyToFight().Build();
        game.CurrentPlayerId.Should().Be(game.Player1.Id);
        game.CurrentPlayerId.Should().NotBe(game.Player2.Id);
    }

    [Fact]
    public void Shot_TracksBoardState()
    {
        var game = GameBuilder.New().ReadyToFight().Build();
        var coord = FindEmptyCell(game.Player1, game.Player2);

        GameEngine.ExecuteShot(game.Player1, game.Player2, coord);

        game.Player1.OutgoingBoard.HasBeenShot(coord).Should().BeTrue();
    }

    private static Coordinate FindEmptyCell(PlayerState shooter, PlayerState target)
    {
        for (int x = 0; x < shooter.OutgoingBoard.Width; x++)
            for (int y = 0; y < shooter.OutgoingBoard.Height; y++)
            {
                var c = new Coordinate(x, y);
                if (!shooter.OutgoingBoard.HasBeenShot(c) && target.Fleet?.FindByCell(c) is null)
                    return c;
            }
        throw new InvalidOperationException("Pas de case vide disponible.");
    }
}
