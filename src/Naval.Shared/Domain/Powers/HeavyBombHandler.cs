using Naval.Shared.Contracts;

namespace Naval.Shared.Domain.Powers;

/// <summary>
/// Bombe lourde — pouvoir de destruction. Tire sur chaque case d'un carré 3×3 centré sur la
/// cible (distance de Chebyshev ≤ 1, contrairement au disque euclidien du Sonar).
/// <list type="bullet">
/// <item>La zone est tronquée au bord de la grille : une bombe en coin ne frappe que 4 cases.</item>
/// <item>Les cases déjà visées sont ignorées, pas refusées ; seule une zone entièrement déjà
/// visée est refusée, pour ne pas faire payer 10 d'énergie pour rien.</item>
/// <item>Les touches ne rapportent pas d'énergie et la bombe consomme le tour de tir.</item>
/// </list>
/// Aucune information supplémentaire ne fuit : seules les cases frappées sont révélées, avec le
/// même résultat qu'un tir normal.
/// </summary>
public sealed class HeavyBombHandler : IPowerHandler
{
    private const int Radius = 1; // carré 3×3

    public PowerId Id => PowerId.HeavyBomb;

    public string? Validate(PlayerState caster, PlayerState target, PowerTargetDto powerTarget)
    {
        if (powerTarget.Cell is null)
            return "La Bombe lourde nécessite une case cible.";

        var center = new Coordinate(powerTarget.Cell.Value.X, powerTarget.Cell.Value.Y);
        if (!center.IsWithinBounds(caster.OutgoingBoard.Width, caster.OutgoingBoard.Height))
            return "La cible est hors de la grille.";

        if (BlastZone(center, caster.OutgoingBoard).All(caster.OutgoingBoard.HasBeenShot))
            return "Toute la zone a déjà été visée.";

        return null;
    }

    public PowerEffectResult Execute(PlayerState caster, PlayerState target, PowerTargetDto powerTarget)
    {
        var center = new Coordinate(powerTarget.Cell!.Value.X, powerTarget.Cell.Value.Y);

        var shots = BlastZone(center, caster.OutgoingBoard)
            .Where(cell => !caster.OutgoingBoard.HasBeenShot(cell))
            .Select(cell => new PowerShot(cell,
                GameEngine.ExecuteShot(caster, target, cell, grantEnergy: false).result))
            .ToList();

        int hits = shots.Count(s => s.Result.Outcome is ShotOutcome.Hit or ShotOutcome.Sunk);
        int sunk = shots.Count(s => s.Result.Outcome == ShotOutcome.Sunk);

        return new PowerEffectResult(
            RevealedCount: null,
            RevealedCells: [],
            Shots: shots,
            ConsumesTurn: true,
            Message: $"Bombardement en secteur {ToLabel(center)} : {hits} touche(s), {sunk} navire(s) coulé(s).");
    }

    private static IEnumerable<Coordinate> BlastZone(Coordinate center, Board board)
    {
        for (int dy = -Radius; dy <= Radius; dy++)
            for (int dx = -Radius; dx <= Radius; dx++)
            {
                var cell = new Coordinate(center.X + dx, center.Y + dy);
                if (cell.IsWithinBounds(board.Width, board.Height))
                    yield return cell;
            }
    }

    private static string ToLabel(Coordinate c) => $"{(char)('A' + c.X)}{c.Y + 1}";
}
