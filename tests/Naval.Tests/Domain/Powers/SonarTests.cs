using FluentAssertions;
using Naval.Shared.Contracts;
using Naval.Shared.Domain;
using Naval.Shared.Domain.Powers;
using Xunit;

namespace Naval.Tests.Domain.Powers;

public sealed class SonarTests
{
    [Fact]
    public void Nominal_sonar_reveals_the_exact_count_of_ship_cells_in_radius()
    {
        var (caster, target) = BuildPlayers(casterEnergy: 5);
        // Croiseur vertical (0,0)-(0,1)-(0,2) : les 3 cases sont à distance <= 4 de (0,0).
        target.Fleet = new Fleet([new Ship("t-cruiser", ShipType.Cruiser, 3,
            new Coordinate(0, 0), Orientation.Vertical)]);
        var registry = new PowerRegistry([new SonarHandler()]);
        var powerTarget = new PowerTargetDto(new CoordinateDto(0, 0), null, null, null, null);

        var (result, error) = GameEngine.ActivatePower(caster, target, PowerId.Sonar, powerTarget, registry);

        error.Should().BeNull();
        result!.Effect.RevealedCount.Should().Be(3);
        result.Effect.RevealedCells.Should().BeEmpty(); // Sonar ne révèle jamais de position
        result.EnergySpent.Should().Be(3); // coût Sonar, cf. PowerCatalog
        caster.Energy.Should().Be(2);
        var slot = caster.PowerSlots.Single(s => s.PowerId == PowerId.Sonar);
        slot.Status.Should().Be(PowerSlotStatus.OnCooldown);
        slot.CooldownRemaining.Should().Be(3);
    }

    [Fact]
    public void Refusal_when_energy_is_insufficient_leaves_state_untouched()
    {
        var (caster, target) = BuildPlayers(casterEnergy: 1); // coût Sonar = 3
        target.Fleet = new Fleet([new Ship("t-cruiser", ShipType.Cruiser, 3,
            new Coordinate(0, 0), Orientation.Vertical)]);
        var registry = new PowerRegistry([new SonarHandler()]);
        var powerTarget = new PowerTargetDto(new CoordinateDto(0, 0), null, null, null, null);

        var (result, error) = GameEngine.ActivatePower(caster, target, PowerId.Sonar, powerTarget, registry);

        error.Should().Be(ErrorCodes.InsufficientEnergy);
        result.Should().BeNull();
        caster.Energy.Should().Be(1);
        caster.PowerSlots.Single().Status.Should().Be(PowerSlotStatus.Ready);
    }

    [Fact]
    public void Targeting_a_cell_outside_the_grid_is_rejected_without_throwing()
    {
        var (caster, target) = BuildPlayers(casterEnergy: 5); // grille 10x10
        target.Fleet = new Fleet([new Ship("t-cruiser", ShipType.Cruiser, 3,
            new Coordinate(0, 0), Orientation.Vertical)]);
        var registry = new PowerRegistry([new SonarHandler()]);
        var powerTarget = new PowerTargetDto(new CoordinateDto(10, 10), null, null, null, null);

        var (result, error) = GameEngine.ActivatePower(caster, target, PowerId.Sonar, powerTarget, registry);

        error.Should().Be(ErrorCodes.InvalidTarget);
        result.Should().BeNull();
        caster.Energy.Should().Be(5); // énergie non déduite sur un refus
    }

    private static (PlayerState caster, PlayerState target) BuildPlayers(int casterEnergy)
    {
        var caster = new PlayerState(PlayerId.New(), "P1", PlayerSlot.One, "t1", 10, 10,
            equippedPowers: [PowerId.Sonar]);
        caster.Energy = casterEnergy;
        var target = new PlayerState(PlayerId.New(), "P2", PlayerSlot.Two, "t2", 10, 10);
        return (caster, target);
    }
}
