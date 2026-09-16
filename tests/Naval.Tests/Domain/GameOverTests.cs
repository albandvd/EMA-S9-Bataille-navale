using FluentAssertions;
using Naval.Shared.Domain;
using Naval.Tests.Builders;

namespace Naval.Tests.Domain;

public sealed class GameOverTests
{
    [Fact]
    public void GameOver_WhenAllOpponentShipsSunk()
    {
        var (shooter, target) = BuildPlayers();

        SinkAllShips(shooter, target);

        GameEngine.IsGameOver(BuildGameFromPlayers(shooter, target)).Should().BeTrue();
    }

    [Fact]
    public void GameNotOver_WhenShipsRemain()
    {
        var (shooter, target) = BuildPlayers();
        GameEngine.ExecuteShot(shooter, target, new Coordinate(0, 0)); // hit only

        GameEngine.IsGameOver(BuildGameFromPlayers(shooter, target)).Should().BeFalse();
    }

    [Fact]
    public void FindWinner_ReturnsShooter_WhenAllOpponentSunk()
    {
        var (shooter, target) = BuildPlayers();
        var game = BuildGameFromPlayers(shooter, target);

        SinkAllShips(shooter, target);

        GameEngine.FindWinner(game).Should().Be(shooter);
    }

    [Fact]
    public void FindWinner_ReturnsNull_WhenNoWinner()
    {
        var (shooter, target) = BuildPlayers();
        var game = BuildGameFromPlayers(shooter, target);

        GameEngine.FindWinner(game).Should().BeNull();
    }

    [Fact]
    public void AllFleet_Sunk_FleetAllSunkTrue()
    {
        var (shooter, target) = BuildPlayers();
        SinkAllShips(shooter, target);
        target.Fleet!.AllSunk.Should().BeTrue();
    }

    private static (PlayerState shooter, PlayerState target) BuildPlayers()
    {
        var p1 = new PlayerState(PlayerId.New(), "P1", Naval.Shared.Contracts.PlayerSlot.One, "t1", 10, 10);
        var p2 = new PlayerState(PlayerId.New(), "P2", Naval.Shared.Contracts.PlayerSlot.Two, "t2", 10, 10);
        p1.Fleet = FleetBuilder.Classic().Build(FleetPresets.Classic);
        p2.Fleet = FleetBuilder.Classic().Build(FleetPresets.Classic);
        return (p1, p2);
    }

    private static Game BuildGameFromPlayers(PlayerState p1, PlayerState p2) =>
        new Game(
            GameId.New(), Naval.Shared.Contracts.GameMode.SinglePlayer, null, "Classic",
            10, 10, false, 0, p1, p2)
        {
            Status = Naval.Shared.Contracts.GameStatus.InProgress,
            TurnNumber = 1,
        };

    private static void SinkAllShips(PlayerState shooter, PlayerState target)
    {
        foreach (var ship in target.Fleet!.Ships)
            foreach (var cell in ship.Cells)
            {
                target.IncomingBoard.MarkShot(cell);
                shooter.OutgoingBoard.MarkShot(cell);
                ship.TryHit(cell);
            }
    }
}
