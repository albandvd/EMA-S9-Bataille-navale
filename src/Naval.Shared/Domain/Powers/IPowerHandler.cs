using Naval.Shared.Contracts;

namespace Naval.Shared.Domain.Powers;

/// <summary>Une case révélée par un pouvoir de reconnaissance qui identifie le contenu exact
/// (ex : Radar tactique). Sonar ne l'utilise jamais — il ne renvoie qu'un décompte.</summary>
public sealed record RevealedCell(Coordinate Cell, bool Occupied);

/// <summary>Résultat brut de l'exécution d'un pouvoir, avant mise en forme en DTO.</summary>
public sealed record PowerEffectResult(
    int? RevealedCount,
    IReadOnlyList<RevealedCell> RevealedCells,
    string Message);

/// <summary>
/// Un pouvoir = une définition statique (<see cref="PowerCatalog"/>) + un handler qui porte le
/// comportement. Voir docs/02-pouvoirs.md §5. L'énergie et le cooldown sont déjà contrôlés par
/// <see cref="GameEngine.ActivatePower"/> avant l'appel : <see cref="Validate"/> ne vérifie que
/// les préconditions propres au pouvoir (ex : case cible dans la grille).
/// </summary>
public interface IPowerHandler
{
    PowerId Id { get; }

    string? Validate(PlayerState caster, PlayerState target, PowerTargetDto powerTarget);

    PowerEffectResult Execute(PlayerState caster, PlayerState target, PowerTargetDto powerTarget);
}
