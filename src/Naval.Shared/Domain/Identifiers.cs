namespace Naval.Shared.Domain;

public readonly record struct PlayerId(Guid Value)
{
    public static PlayerId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct GameId(Guid Value)
{
    public static GameId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct Coordinate(int X, int Y)
{
    public bool IsWithinBounds(int width, int height) => X >= 0 && X < width && Y >= 0 && Y < height;
}
