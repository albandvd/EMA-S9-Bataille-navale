using Naval.Shared.Contracts;

namespace Naval.Shared.Domain;

public sealed record FleetShipEntry(ShipType Type, int Size, int Count);

public sealed record FleetPreset(string Name, string Description, IReadOnlyList<FleetShipEntry> Ships, int MinGridSize);

public static class FleetPresets
{
    public static readonly FleetPreset Classic = new(
        "Classic",
        "La flotte classique : 5 navires, 17 cases totales.",
        [
            new(ShipType.Carrier, 5, 1),
            new(ShipType.Battleship, 4, 1),
            new(ShipType.Cruiser, 3, 1),
            new(ShipType.Submarine, 3, 1),
            new(ShipType.Destroyer, 2, 1),
        ],
        MinGridSize: 10);

    public static readonly FleetPreset Skirmish = new(
        "Skirmish",
        "Flotte légère : 3 navires, 9 cases totales.",
        [
            new(ShipType.Cruiser, 3, 1),
            new(ShipType.Submarine, 3, 1),
            new(ShipType.Destroyer, 2, 1),
        ],
        MinGridSize: 8);

    public static readonly IReadOnlyList<FleetPreset> All = [Classic, Skirmish];

    public static FleetPreset? Find(string name) =>
        All.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
