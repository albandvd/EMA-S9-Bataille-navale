using Naval.Shared.Contracts;

namespace Naval.Shared.Domain.Powers;

/// <summary>État d'exécution d'un pouvoir équipé, pour un joueur. Le catalogue
/// (<see cref="PowerCatalog"/>) fournit les valeurs statiques ; ce type porte l'état mutable.</summary>
public sealed class PowerSlot
{
    public PowerId PowerId { get; }
    public PowerSlotStatus Status { get; set; } = PowerSlotStatus.Ready;
    public int ChargeRemaining { get; set; }
    public int CooldownRemaining { get; set; }

    /// <summary>-1 = illimité, comme <c>PowerDefinitionDto.MaxUses</c>.</summary>
    public int UsesLeft { get; set; }

    public PowerSlot(PowerId powerId, int usesLeft)
    {
        PowerId = powerId;
        UsesLeft = usesLeft;
    }
}
