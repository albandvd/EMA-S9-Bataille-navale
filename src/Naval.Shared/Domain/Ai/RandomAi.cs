namespace Naval.Shared.Domain.Ai;

/// <summary>S-11 — IA niveau 1 : tir uniforme sur une case non encore jouée.</summary>
public sealed class RandomAi : IAiStrategy
{
    private readonly Random _rng;

    public RandomAi(Random? rng = null)
    {
        _rng = rng ?? Random.Shared;
    }

    public Coordinate ChooseTarget(PlayerState aiPlayer, PlayerState opponent)
    {
        var board = aiPlayer.OutgoingBoard;
        var candidates = new List<Coordinate>();

        for (int x = 0; x < board.Width; x++)
            for (int y = 0; y < board.Height; y++)
            {
                var c = new Coordinate(x, y);
                if (!board.HasBeenShot(c))
                    candidates.Add(c);
            }

        return candidates[_rng.Next(candidates.Count)];
    }
}
