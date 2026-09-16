using FluentAssertions;
using Naval.Shared.Contracts;
using Naval.Shared.Domain;
using Naval.Tests.Builders;

namespace Naval.Tests.Domain;

public sealed class ShotResolutionTests
{
    [Fact]
    public void Shot_OnEmptyCell_IsMiss()
    {
        var (shooter, target) = BuildPlayers();
        var coord = new Coordinate(9, 9); // hors flotte
        var (result, error) = GameEngine.ExecuteShot(shooter, target, coord);
        error.Should().BeNull();
        result.Outcome.Should().Be(ShotOutcome.Miss);
        result.EnergyGained.Should().Be(0);
    }

    [Fact]
    public void Shot_OnShipCell_IsHit()
    {
        var (shooter, target) = BuildPlayers();
        var coord = new Coordinate(0, 0); // Carrier row 0
        var (result, error) = GameEngine.ExecuteShot(shooter, target, coord);
        error.Should().BeNull();
        result.Outcome.Should().Be(ShotOutcome.Hit);
        result.EnergyGained.Should().Be(2);
    }

    [Fact]
    public void Shot_OnAlreadyTargeted_IsRejected()
    {
        var (shooter, target) = BuildPlayers();
        var coord = new Coordinate(0, 0);
        GameEngine.ExecuteShot(shooter, target, coord);

        var (result, error) = GameEngine.ExecuteShot(shooter, target, coord);
        error.Should().Be(ErrorCodes.CellAlreadyTargeted);
        result.Outcome.Should().Be(ShotOutcome.Rejected);
    }

    [Fact]
    public void Shot_OutOfBounds_IsRejected()
    {
        var (shooter, target) = BuildPlayers();
        var (result, error) = GameEngine.ExecuteShot(shooter, target, new Coordinate(10, 10));
        error.Should().Be(ErrorCodes.OutOfBounds);
        result.Outcome.Should().Be(ShotOutcome.Rejected);
    }

    [Fact]
    public void SinkingShip_OutcomeSunk_PlusThreeEnergy()
    {
        var (shooter, target) = BuildPlayers();
        // Destroyer est en (0,4)→(1,4), taille 2
        GameEngine.ExecuteShot(shooter, target, new Coordinate(0, 4));
        var (result, _) = GameEngine.ExecuteShot(shooter, target, new Coordinate(1, 4));

        result.Outcome.Should().Be(ShotOutcome.Sunk);
        result.SunkShip.Should().NotBeNull();
        result.EnergyGained.Should().Be(3);
    }

    [Fact]
    public void ShotsFired_TrackedOnShooter()
    {
        var (shooter, target) = BuildPlayers();
        GameEngine.ExecuteShot(shooter, target, new Coordinate(0, 0));
        GameEngine.ExecuteShot(shooter, target, new Coordinate(1, 1));
        shooter.ShotsFired.Should().Be(2);
    }

    [Fact]
    public void Hits_TrackedOnShooter()
    {
        var (shooter, target) = BuildPlayers();
        GameEngine.ExecuteShot(shooter, target, new Coordinate(0, 0)); // hit
        GameEngine.ExecuteShot(shooter, target, new Coordinate(9, 9)); // miss
        shooter.Hits.Should().Be(1);
    }

    // ── helpers ──

    private static (PlayerState shooter, PlayerState target) BuildPlayers()
    {
        var p1 = new PlayerState(PlayerId.New(), "P1", PlayerSlot.One, "t1", 10, 10);
        var p2 = new PlayerState(PlayerId.New(), "P2", PlayerSlot.Two, "t2", 10, 10);

        var fleet = FleetBuilder.Classic().Build(FleetPresets.Classic, 10, 10);
        p2.Fleet = fleet;
        return (p1, p2);
    }
}
