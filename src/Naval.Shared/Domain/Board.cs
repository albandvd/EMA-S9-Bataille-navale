namespace Naval.Shared.Domain;

/// <summary>
/// Grille de tirs : suit les coups tirés SUR la flotte adverse.
/// </summary>
public sealed class Board
{
    private readonly bool[,] _shots;

    public int Width { get; }
    public int Height { get; }

    public Board(int width, int height)
    {
        Width = width;
        Height = height;
        _shots = new bool[width, height];
    }

    public bool HasBeenShot(Coordinate c) => _shots[c.X, c.Y];

    public void MarkShot(Coordinate c) => _shots[c.X, c.Y] = true;

    public char[,] BuildTargetView(Fleet opponentFleet)
    {
        var view = new char[Width, Height];
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                view[x, y] = '.';

        foreach (var ship in opponentFleet.Ships)
        {
            foreach (var cell in ship.Cells)
            {
                if (!_shots[cell.X, cell.Y]) continue;
                view[cell.X, cell.Y] = ship.IsSunk ? '#' : 'x';
            }
        }

        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (_shots[x, y] && view[x, y] == '.')
                    view[x, y] = 'o';

        return view;
    }

    public char[,] BuildOwnView(Fleet ownFleet)
    {
        var view = new char[Width, Height];
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                view[x, y] = '.';

        foreach (var ship in ownFleet.Ships)
        {
            foreach (var cell in ship.Cells)
            {
                var hasBeenShot = _shots[cell.X, cell.Y];
                view[cell.X, cell.Y] = hasBeenShot
                    ? (ship.IsSunk ? '#' : 'X')
                    : 'S';
            }
        }

        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (_shots[x, y] && view[x, y] == '.')
                    view[x, y] = 'o';

        return view;
    }
}
