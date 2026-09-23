using FluentAssertions;
using Naval.Shared.Contracts;
using Naval.Shared.Domain;
using Naval.Shared.Domain.Powers;
using Xunit;

namespace Naval.Tests.Domain.Powers;

public sealed class TsarBombaTests
{
    private readonly PowerRegistry _registry = new([new TsarBombaHandler()]);

    [Fact]
    public void Nominal_tsar_bomba_fires_on_the_25_cells_around_the_center_and_is_then_exhausted()
    {
        var (caster, target) = BuildPlayers(casterEnergy: 40);
        // Porte-avions horizontal (2,4)→(6,4) : entièrement dans le carré 5×5 centré en (4,4).
        target.Fleet = new Fleet([new Ship("t-carrier", ShipType.Carrier, 5,
            new Coordinate(2, 4), Orientation.Horizontal)]);

        var (result, error) = GameEngine.ActivatePower(caster, target, PowerId.TsarBomba, At(4, 4), _registry);

        error.Should().BeNull();
        var effect = result!.Effect;
        effect.Shots.Should().HaveCount(25);
        effect.Shots.Select(s => s.Target).Should().OnlyContain(c =>
            c.X >= 2 && c.X <= 6 && c.Y >= 2 && c.Y <= 6);
        effect.Shots.Count(s => s.Result.Outcome == ShotOutcome.Sunk).Should().Be(1);
        effect.ConsumesTurn.Should().BeTrue();
        target.Fleet.AllSunk.Should().BeTrue();

        caster.Energy.Should().Be(0); // 40 - 40, les touches ne rapportent rien
        var slot = caster.PowerSlots.Single();
        slot.UsesLeft.Should().Be(0);
        slot.Status.Should().Be(PowerSlotStatus.Exhausted);
    }

    [Fact]
    public void Refusal_on_second_use_because_it_can_only_be_used_once()
    {
        var (caster, target) = BuildPlayers(casterEnergy: 80);
        target.Fleet = new Fleet([new Ship("t-destroyer", ShipType.Destroyer, 2,
            new Coordinate(0, 0), Orientation.Horizontal)]);
        GameEngine.ActivatePower(caster, target, PowerId.TsarBomba, At(7, 7), _registry);

        var (result, error) = GameEngine.ActivatePower(caster, target, PowerId.TsarBomba, At(2, 2), _registry);

        error.Should().Be(ErrorCodes.PowerExhausted);
        result.Should().BeNull();
        caster.Energy.Should().Be(40); // seul le premier usage a été payé
        caster.OutgoingBoard.HasBeenShot(new Coordinate(0, 0)).Should().BeFalse();
    }

    [Fact]
    public void Refusal_when_energy_is_below_40()
    {
        var (caster, target) = BuildPlayers(casterEnergy: 39);
        target.Fleet = new Fleet([]);

        var (_, error) = GameEngine.ActivatePower(caster, target, PowerId.TsarBomba, At(4, 4), _registry);

        error.Should().Be(ErrorCodes.InsufficientEnergy);
        caster.Energy.Should().Be(39);
        caster.PowerSlots.Single().UsesLeft.Should().Be(1);
    }

    [Fact]
    public void Tsar_bomba_in_a_corner_is_truncated_to_the_nine_cells_inside_the_grid()
    {
        var (caster, target) = BuildPlayers(casterEnergy: 40);
        target.Fleet = new Fleet([]);

        var (result, error) = GameEngine.ActivatePower(caster, target, PowerId.TsarBomba, At(0, 0), _registry);

        error.Should().BeNull();
        result!.Effect.Shots.Should().HaveCount(9);
        result.Effect.Shots.Select(s => s.Target).Should().OnlyContain(c => c.X <= 2 && c.Y <= 2);
    }

    private static PowerTargetDto At(int x, int y) => new(new CoordinateDto(x, y), null, null, null, null);

    private static (PlayerState caster, PlayerState target) BuildPlayers(int casterEnergy)
    {
        var caster = new PlayerState(PlayerId.New(), "P1", PlayerSlot.One, "t1", 10, 10,
            equippedPowers: [PowerId.TsarBomba]);
        caster.Energy = casterEnergy;
        var target = new PlayerState(PlayerId.New(), "P2", PlayerSlot.Two, "t2", 10, 10);
        return (caster, target);
    }
}
