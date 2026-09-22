using Naval.Shared.Contracts;

namespace Naval.Shared.Domain.Powers;

/// <summary>Résultat d'une activation réussie, prêt à être mappé en <c>PowerResultDto</c> par
/// Naval.Shared/Contracts/Mapping/GameMapper.cs.</summary>
public sealed record PowerActivationResult(
    PowerId PowerId,
    int EnergySpent,
    int EnergyRemaining,
    int CooldownApplied,
    PowerEffectResult Effect);
