using Naval.Shared.Contracts;

namespace Naval.Shared.Domain;

public sealed class Ship
{
    private int _hitCount;

    public string Id { get; }
    public ShipType Type { get; }
    public int Size { get; }
    public Coordinate Origin { get; }
    public Orientation Orientation { get; }
    public IReadOnlyList<Coordinate> Cells { get; }
    public int HitCount => _hitCount;
    public bool IsSunk => _hitCount >= Size;

    public Ship(string id, ShipType type, int size, Coordinate origin, Orientation orientation)
    {
        Id = id;
        Type = type;
        Size = size;
        Origin = origin;
        Orientation = orientation;
        Cells = BuildCells(origin, orientation, size);
    }

    public bool Occupies(Coordinate cell) => Cells.Contains(cell);

    public bool TryHit(Coordinate cell)
    {
        if (!Occupies(cell)) return false;
        _hitCount++;
        return true;
    }

    private static IReadOnlyList<Coordinate> BuildCells(Coordinate origin, Orientation orientation, int size)
    {
        var cells = new Coordinate[size];
        for (int i = 0; i < size; i++)
            cells[i] = orientation == Orientation.Horizontal
                ? new Coordinate(origin.X + i, origin.Y)
                : new Coordinate(origin.X, origin.Y + i);
        return cells;
    }
}
