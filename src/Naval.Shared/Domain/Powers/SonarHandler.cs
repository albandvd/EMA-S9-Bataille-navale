using Naval.Shared.Contracts;

namespace Naval.Shared.Domain.Powers;

/// <summary>
/// P-01 — docs/02-pouvoirs.md §3. Révèle le NOMBRE de cases occupées de la flotte adverse dans
/// un disque de rayon 4 (distance euclidienne, pas Chebyshev — un futur pouvoir de zone carrée
/// comme la Frappe orbitale ne doit pas réutiliser ce calcul de distance).
/// Ne révèle jamais de position : RevealedCells reste toujours vide.
/// </summary>
public sealed class SonarHandler : IPowerHandler
{
    private const int RadiusSquared = 16; // rayon 4 au carré

    public PowerId Id => PowerId.Sonar;

    public string? Validate(PlayerState caster, PlayerState target, PowerTargetDto powerTarget)
    {
        if (powerTarget.Cell is null)
            return "Le Sonar nécessite une case cible.";

        var cell = new Coordinate(powerTarget.Cell.Value.X, powerTarget.Cell.Value.Y);
        if (!cell.IsWithinBounds(caster.OutgoingBoard.Width, caster.OutgoingBoard.Height))
            return "La cible est hors de la grille.";

        return null;
    }

    public PowerEffectResult Execute(PlayerState caster, PlayerState target, PowerTargetDto powerTarget)
    {
        var center = new Coordinate(powerTarget.Cell!.Value.X, powerTarget.Cell.Value.Y);

        int count = target.Fleet?.Ships
            .SelectMany(s => s.Cells)
            .Count(cell => DistanceSquared(center, cell) <= RadiusSquared) ?? 0;

        return new PowerEffectResult(
            RevealedCount: count,
            RevealedCells: [],
            Message: $"Sonar : {count} case(s) détectée(s) en zone.");
    }

    private static int DistanceSquared(Coordinate a, Coordinate b)
    {
        int dx = a.X - b.X;
        int dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }
}
