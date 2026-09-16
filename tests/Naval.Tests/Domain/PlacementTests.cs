using FluentAssertions;
using Naval.Shared.Contracts;
using Naval.Shared.Domain;

namespace Naval.Tests.Domain;

public sealed class PlacementTests
{
    private static readonly FleetPreset Classic = FleetPresets.Classic;

    [Fact]
    public void ValidPlacement_ReturnsFleet()
    {
        var placements = ClassicPlacements();
        var (fleet, errors) = GameEngine.ValidateAndBuildFleet(placements, Classic, 10, 10, "own");
        errors.Should().BeEmpty();
        fleet.Should().NotBeNull();
        fleet!.Ships.Should().HaveCount(5);
    }

    [Fact]
    public void ShipOutOfBounds_ReturnsError()
    {
        var placements = new List<ShipPlacementDto>
        {
            new(ShipType.Carrier, new CoordinateDto(8, 0), Orientation.Horizontal), // déborde
            new(ShipType.Battleship, new CoordinateDto(0, 1), Orientation.Horizontal),
            new(ShipType.Cruiser, new CoordinateDto(0, 2), Orientation.Horizontal),
            new(ShipType.Submarine, new CoordinateDto(0, 3), Orientation.Horizontal),
            new(ShipType.Destroyer, new CoordinateDto(0, 4), Orientation.Horizontal),
        };

        var (fleet, errors) = GameEngine.ValidateAndBuildFleet(placements, Classic, 10, 10, "own");
        fleet.Should().BeNull();
        errors.Should().Contain(e => e.Kind == PlacementErrorKind.OutOfBounds);
    }

    [Fact]
    public void OverlappingShips_ReturnsError()
    {
        var placements = new List<ShipPlacementDto>
        {
            new(ShipType.Carrier,    new CoordinateDto(0, 0), Orientation.Horizontal),
            new(ShipType.Battleship, new CoordinateDto(0, 0), Orientation.Horizontal), // superposé
            new(ShipType.Cruiser,    new CoordinateDto(0, 2), Orientation.Horizontal),
            new(ShipType.Submarine,  new CoordinateDto(0, 3), Orientation.Horizontal),
            new(ShipType.Destroyer,  new CoordinateDto(0, 4), Orientation.Horizontal),
        };

        var (fleet, errors) = GameEngine.ValidateAndBuildFleet(placements, Classic, 10, 10, "own");
        fleet.Should().BeNull();
        errors.Should().Contain(e => e.Kind == PlacementErrorKind.OverlappingShips);
    }

    [Fact]
    public void MissingShip_ReturnsFleetIncompleteError()
    {
        var placements = new List<ShipPlacementDto>
        {
            new(ShipType.Carrier, new CoordinateDto(0, 0), Orientation.Horizontal),
            // Battleship manquant
            new(ShipType.Cruiser, new CoordinateDto(0, 2), Orientation.Horizontal),
            new(ShipType.Submarine, new CoordinateDto(0, 3), Orientation.Horizontal),
            new(ShipType.Destroyer, new CoordinateDto(0, 4), Orientation.Horizontal),
        };

        var (fleet, errors) = GameEngine.ValidateAndBuildFleet(placements, Classic, 10, 10, "own");
        fleet.Should().BeNull();
        errors.Should().Contain(e => e.Kind == PlacementErrorKind.FleetIncomplete);
    }

    [Fact]
    public void VerticalShipOutOfBounds_ReturnsError()
    {
        var placements = new List<ShipPlacementDto>
        {
            new(ShipType.Carrier, new CoordinateDto(0, 8), Orientation.Vertical), // dépasse en Y
            new(ShipType.Battleship, new CoordinateDto(2, 0), Orientation.Horizontal),
            new(ShipType.Cruiser, new CoordinateDto(0, 2), Orientation.Horizontal),
            new(ShipType.Submarine, new CoordinateDto(0, 3), Orientation.Horizontal),
            new(ShipType.Destroyer, new CoordinateDto(0, 4), Orientation.Horizontal),
        };

        var (fleet, errors) = GameEngine.ValidateAndBuildFleet(placements, Classic, 10, 10, "own");
        fleet.Should().BeNull();
        errors.Should().Contain(e => e.Kind == PlacementErrorKind.OutOfBounds);
    }

    [Fact]
    public void SkirmishPreset_ThreeShips()
    {
        var preset = FleetPresets.Skirmish;
        var placements = new List<ShipPlacementDto>
        {
            new(ShipType.Cruiser,   new CoordinateDto(0, 0), Orientation.Horizontal),
            new(ShipType.Submarine, new CoordinateDto(0, 1), Orientation.Horizontal),
            new(ShipType.Destroyer, new CoordinateDto(0, 2), Orientation.Horizontal),
        };

        var (fleet, errors) = GameEngine.ValidateAndBuildFleet(placements, preset, 10, 10, "own");
        errors.Should().BeEmpty();
        fleet!.Ships.Should().HaveCount(3);
    }

    [Fact]
    public void RandomPlacement_ProducesValidFleet()
    {
        var rng = new Random(42);
        var placements = GameEngine.GenerateRandomPlacement(FleetPresets.Classic, 10, 10, rng);
        var (fleet, errors) = GameEngine.ValidateAndBuildFleet(placements, FleetPresets.Classic, 10, 10, "own");
        errors.Should().BeEmpty();
        fleet.Should().NotBeNull();
    }

    [Fact]
    public void RandomPlacement_IsDeterministicWithSameSeed()
    {
        var rng1 = new Random(99);
        var rng2 = new Random(99);
        var p1 = GameEngine.GenerateRandomPlacement(FleetPresets.Classic, 10, 10, rng1);
        var p2 = GameEngine.GenerateRandomPlacement(FleetPresets.Classic, 10, 10, rng2);

        p1.Select(p => (p.Type, p.Origin.X, p.Origin.Y, p.Orientation))
          .Should().Equal(p2.Select(p => (p.Type, p.Origin.X, p.Origin.Y, p.Orientation)));
    }

    [Fact]
    public void RandomPlacement_WorksOn8x8Grid()
    {
        var rng = new Random(7);
        var placements = GameEngine.GenerateRandomPlacement(FleetPresets.Skirmish, 8, 8, rng);
        var (fleet, errors) = GameEngine.ValidateAndBuildFleet(placements, FleetPresets.Skirmish, 8, 8, "own");
        errors.Should().BeEmpty();
        fleet.Should().NotBeNull();
    }

    private static IReadOnlyList<ShipPlacementDto> ClassicPlacements() =>
    [
        new(ShipType.Carrier,    new CoordinateDto(0, 0), Orientation.Horizontal),
        new(ShipType.Battleship, new CoordinateDto(0, 1), Orientation.Horizontal),
        new(ShipType.Cruiser,    new CoordinateDto(0, 2), Orientation.Horizontal),
        new(ShipType.Submarine,  new CoordinateDto(0, 3), Orientation.Horizontal),
        new(ShipType.Destroyer,  new CoordinateDto(0, 4), Orientation.Horizontal),
    ];
}
