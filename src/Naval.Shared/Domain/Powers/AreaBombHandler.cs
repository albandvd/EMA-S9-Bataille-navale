using Naval.Shared.Contracts;

namespace Naval.Shared.Domain.Powers;

/// <summary>
/// Base des pouvoirs de destruction de zone (Bombe lourde, Tsar Bomba). Tire sur chaque case
/// d'un carré centré sur la cible, de demi-côté <c>Radius</c> lu dans <see cref="PowerCatalog"/>
/// (distance de Chebyshev, contrairement au disque euclidien du Sonar).
/// <list type="bullet">
/// <item>La zone est tronquée au bord de la grille.</item>
/// <item>Les cases déjà visées sont ignorées, pas refusées ; seule une zone entièrement déjà
/// visée est refusée, pour ne pas faire payer l'énergie pour rien.</item>
/// <item>Les touches ne rapportent pas d'énergie et la bombe consomme le tour de tir.</item>
/// </list>
/// Aucune information supplémentaire ne fuit : seules les cases frappées sont révélées, avec le
/// même résultat qu'un tir normal.
/// </summary>
public abstract class AreaBombHandler : IPowerHandler
{
    public abstract PowerId Id { get; }

    private PowerDefinitionDto Definition => PowerCatalog.Find(Id)!;

    private int Radius => Definition.Radius
        ?? throw new InvalidOperationException($"{Id} doit définir un rayon dans PowerCatalog.");

    public string? Validate(PlayerState caster, PlayerState target, PowerTargetDto powerTarget)
    {
        if (powerTarget.Cell is null)
            return $"{Definition.Name} nécessite une case cible.";

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
            Message: $"{Definition.Name} en secteur {ToLabel(center)} : {hits} touche(s), {sunk} navire(s) coulé(s).");
    }

    private IEnumerable<Coordinate> BlastZone(Coordinate center, Board board)
    {
        int radius = Radius;
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                var cell = new Coordinate(center.X + dx, center.Y + dy);
                if (cell.IsWithinBounds(board.Width, board.Height))
                    yield return cell;
            }
    }

    private static string ToLabel(Coordinate c) => $"{(char)('A' + c.X)}{c.Y + 1}";
}

/// <summary>Bombe lourde — carré 3×3, coût 10, cooldown 11.</summary>
public sealed class HeavyBombHandler : AreaBombHandler
{
    public override PowerId Id => PowerId.HeavyBomb;
}

/// <summary>Tsar Bomba — carré 5×5, coût 40, une seule utilisation par partie.</summary>
public sealed class TsarBombaHandler : AreaBombHandler
{
    public override PowerId Id => PowerId.TsarBomba;
}
