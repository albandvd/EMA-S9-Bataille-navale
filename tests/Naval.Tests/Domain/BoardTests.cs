using FluentAssertions;
using Naval.Shared.Domain;

namespace Naval.Tests.Domain;

public sealed class BoardTests
{
    [Fact]
    public void NewBoard_AllCellsUnshot()
    {
        var board = new Board(10, 10);
        board.HasBeenShot(new Coordinate(0, 0)).Should().BeFalse();
        board.HasBeenShot(new Coordinate(9, 9)).Should().BeFalse();
    }

    [Fact]
    public void MarkShot_CellBecomesShot()
    {
        var board = new Board(10, 10);
        board.MarkShot(new Coordinate(3, 4));
        board.HasBeenShot(new Coordinate(3, 4)).Should().BeTrue();
    }

    [Fact]
    public void MarkShot_OtherCellsUnaffected()
    {
        var board = new Board(10, 10);
        board.MarkShot(new Coordinate(3, 4));
        board.HasBeenShot(new Coordinate(3, 5)).Should().BeFalse();
    }

    [Fact]
    public void BuildTargetView_MissShownAsO()
    {
        var board = new Board(5, 5);
        var fleet = BuildFleetAt(new Coordinate(3, 4), 2); // (3,4)→(4,4), dans la grille 5×5
        board.MarkShot(new Coordinate(0, 0));

        var view = board.BuildTargetView(fleet);
        view[0, 0].Should().Be('o');
    }

    [Fact]
    public void BuildTargetView_HitShownAsX()
    {
        var board = new Board(5, 5);
        var fleet = BuildFleetAt(new Coordinate(0, 0), 2);
        board.MarkShot(new Coordinate(0, 0));
        fleet.Ships[0].TryHit(new Coordinate(0, 0));

        var view = board.BuildTargetView(fleet);
        view[0, 0].Should().Be('x');
    }

    [Fact]
    public void BuildTargetView_SunkShownAsHash()
    {
        var board = new Board(5, 5);
        var fleet = BuildFleetAt(new Coordinate(0, 0), 2);
        board.MarkShot(new Coordinate(0, 0));
        board.MarkShot(new Coordinate(1, 0));
        fleet.Ships[0].TryHit(new Coordinate(0, 0));
        fleet.Ships[0].TryHit(new Coordinate(1, 0));

        var view = board.BuildTargetView(fleet);
        view[0, 0].Should().Be('#');
        view[1, 0].Should().Be('#');
    }

    [Fact]
    public void BuildOwnView_IntactShipShownAsS()
    {
        var board = new Board(5, 5);
        var fleet = BuildFleetAt(new Coordinate(0, 0), 3);

        var view = board.BuildOwnView(fleet);
        view[0, 0].Should().Be('S');
        view[1, 0].Should().Be('S');
        view[2, 0].Should().Be('S');
    }

    private static Fleet BuildFleetAt(Coordinate origin, int size)
    {
        var ship = new Ship("test-1", Naval.Shared.Contracts.ShipType.Destroyer, size, origin, Naval.Shared.Contracts.Orientation.Horizontal);
        return new Fleet([ship]);
    }
}
