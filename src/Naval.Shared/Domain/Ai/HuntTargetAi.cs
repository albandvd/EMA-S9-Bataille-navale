using Naval.Shared.Contracts;

namespace Naval.Shared.Domain.Ai;

/// <summary>
/// S-12 — IA niveau 2 : chasse/traque.
/// Après un Hit, cible les 4 cases adjacentes.
/// Après 2 hits alignés, suit l'axe.
/// </summary>
public sealed class HuntTargetAi : IAiStrategy
{
    private readonly Random _rng;

    public HuntTargetAi(Random? rng = null)
    {
        _rng = rng ?? Random.Shared;
    }

    public Coordinate ChooseTarget(PlayerState aiPlayer, PlayerState opponent)
    {
        var board = aiPlayer.OutgoingBoard;
        int w = board.Width, h = board.Height;

        // Collecte les hits non coulés (les navires coulés ne nous intéressent plus)
        var hits = new List<Coordinate>();
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                var c = new Coordinate(x, y);
                if (!board.HasBeenShot(c)) continue;
                var ship = opponent.Fleet?.FindByCell(c);
                if (ship is { IsSunk: false }) hits.Add(c);
            }

        if (hits.Count >= 2)
        {
            // Détecte l'axe
            bool sameRow = hits.All(c => c.Y == hits[0].Y);
            bool sameCol = hits.All(c => c.X == hits[0].X);

            if (sameRow)
            {
                int minX = hits.Min(c => c.X);
                int maxX = hits.Max(c => c.X);
                int row = hits[0].Y;

                var axisTargets = new List<Coordinate>();
                if (minX - 1 >= 0) axisTargets.Add(new Coordinate(minX - 1, row));
                if (maxX + 1 < w) axisTargets.Add(new Coordinate(maxX + 1, row));

                var valid = axisTargets.Where(c => !board.HasBeenShot(c)).ToList();
                if (valid.Count > 0) return valid[_rng.Next(valid.Count)];
            }
            else if (sameCol)
            {
                int minY = hits.Min(c => c.Y);
                int maxY = hits.Max(c => c.Y);
                int col = hits[0].X;

                var axisTargets = new List<Coordinate>();
                if (minY - 1 >= 0) axisTargets.Add(new Coordinate(col, minY - 1));
                if (maxY + 1 < h) axisTargets.Add(new Coordinate(col, maxY + 1));

                var valid = axisTargets.Where(c => !board.HasBeenShot(c)).ToList();
                if (valid.Count > 0) return valid[_rng.Next(valid.Count)];
            }
        }

        if (hits.Count == 1)
        {
            var hit = hits[0];
            Coordinate[] adjacent =
            [
                new(hit.X - 1, hit.Y),
                new(hit.X + 1, hit.Y),
                new(hit.X, hit.Y - 1),
                new(hit.X, hit.Y + 1),
            ];

            var valid = adjacent
                .Where(c => c.IsWithinBounds(w, h) && !board.HasBeenShot(c))
                .ToList();

            if (valid.Count > 0) return valid[_rng.Next(valid.Count)];
        }

        // Mode chasse : damier pour optimiser (cases paires uniquement si aucun hit en attente)
        var checkerboard = new List<Coordinate>();
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if ((x + y) % 2 == 0)
                {
                    var c = new Coordinate(x, y);
                    if (!board.HasBeenShot(c)) checkerboard.Add(c);
                }

        if (checkerboard.Count > 0) return checkerboard[_rng.Next(checkerboard.Count)];

        // Repli total sur aléatoire
        var fallback = new List<Coordinate>();
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                var c = new Coordinate(x, y);
                if (!board.HasBeenShot(c)) fallback.Add(c);
            }

        return fallback[_rng.Next(fallback.Count)];
    }
}
