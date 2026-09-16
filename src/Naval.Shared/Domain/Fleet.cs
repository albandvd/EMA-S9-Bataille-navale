namespace Naval.Shared.Domain;

public sealed class Fleet
{
    public IReadOnlyList<Ship> Ships { get; }
    public bool AllSunk => Ships.All(s => s.IsSunk);
    public int SunkCount => Ships.Count(s => s.IsSunk);

    public Fleet(IReadOnlyList<Ship> ships)
    {
        Ships = ships;
    }

    public Ship? FindByCell(Coordinate cell) => Ships.FirstOrDefault(s => s.Occupies(cell));
    public Ship? FindById(string id) => Ships.FirstOrDefault(s => s.Id == id);
}
