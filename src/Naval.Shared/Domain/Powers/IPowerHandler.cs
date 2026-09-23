using Naval.Shared.Contracts;

namespace Naval.Shared.Domain.Powers;

/// <summary>Une case révélée par un pouvoir de reconnaissance qui identifie le contenu exact
/// (ex : Radar tactique). Sonar ne l'utilise jamais — il ne renvoie qu'un décompte.</summary>
public sealed record RevealedCell(Coordinate Cell, bool Occupied);

/// <summary>Un tir réellement appliqué par un pouvoir offensif sur la flotte adverse.</summary>
public sealed record PowerShot(Coordinate Target, ShotResult Result);

/// <summary>Résultat brut de l'exécution d'un pouvoir, avant mise en forme en DTO.</summary>
/// <param name="Shots">Tirs appliqués ; vide pour un pouvoir de reconnaissance.</param>
/// <param name="ConsumesTurn">true si le pouvoir remplace le tir du tour : l'appelant vérifie
/// alors la fin de partie et passe la main, comme après un tir normal.</param>
public sealed record PowerEffectResult(
    int? RevealedCount,
    IReadOnlyList<RevealedCell> RevealedCells,
    IReadOnlyList<PowerShot> Shots,
    bool ConsumesTurn,
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
