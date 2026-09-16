using Naval.Shared.Contracts;
using Naval.Shared.Domain;

namespace Naval.Tests.Builders;

public sealed class FleetBuilder
{
    private readonly List<ShipPlacementDto> _placements = [];
    private string _prefix = "own";

    public FleetBuilder WithPrefix(string prefix)
    {
        _prefix = prefix;
        return this;
    }

    public FleetBuilder WithShip(ShipType type, int x, int y, Orientation orientation = Orientation.Horizontal)
    {
        _placements.Add(new ShipPlacementDto(type, new CoordinateDto(x, y), orientation));
        return this;
    }

    public Fleet Build(FleetPreset? preset = null, int gridWidth = 10, int gridHeight = 10)
    {
        if (_placements.Count == 0 && preset is not null)
        {
            var rng = new Random(42);
            _placements.AddRange(GameEngine.GenerateRandomPlacement(preset, gridWidth, gridHeight, rng));
        }

        var usedPreset = preset ?? FleetPresets.Classic;
        var (fleet, errors) = GameEngine.ValidateAndBuildFleet(_placements, usedPreset, gridWidth, gridHeight, _prefix);
        if (errors.Count > 0) throw new InvalidOperationException($"FleetBuilder: {errors[0].Detail}");
        return fleet!;
    }

    public static FleetBuilder Classic() =>
        new FleetBuilder()
            .WithShip(ShipType.Carrier,    0, 0, Orientation.Horizontal)
            .WithShip(ShipType.Battleship, 0, 1, Orientation.Horizontal)
            .WithShip(ShipType.Cruiser,    0, 2, Orientation.Horizontal)
            .WithShip(ShipType.Submarine,  0, 3, Orientation.Horizontal)
            .WithShip(ShipType.Destroyer,  0, 4, Orientation.Horizontal);

    public static FleetBuilder Skirmish() =>
        new FleetBuilder()
            .WithShip(ShipType.Cruiser,   0, 0, Orientation.Horizontal)
            .WithShip(ShipType.Submarine, 0, 1, Orientation.Horizontal)
            .WithShip(ShipType.Destroyer, 0, 2, Orientation.Horizontal);
}
