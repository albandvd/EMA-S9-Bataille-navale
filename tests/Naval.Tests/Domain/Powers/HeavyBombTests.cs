using FluentAssertions;
using Naval.Shared.Contracts;
using Naval.Shared.Domain;
using Naval.Shared.Domain.Powers;
using Xunit;

namespace Naval.Tests.Domain.Powers;

public sealed class HeavyBombTests
{
    private readonly PowerRegistry _registry = new([new HeavyBombHandler()]);

    [Fact]
    public void Nominal_bomb_fires_on_the_nine_cells_around_the_center_and_can_sink_a_ship()
    {
        var (caster, target) = BuildPlayers(casterEnergy: 10);
        // Destroyer horizontal (4,5)-(5,5) : entièrement dans le carré 3×3 centré en (5,5).
        target.Fleet = new Fleet([new Ship("t-destroyer", ShipType.Destroyer, 2,
            new Coordinate(4, 5), Orientation.Horizontal)]);

        var (result, error) = GameEngine.ActivatePower(caster, target, PowerId.HeavyBomb, At(5, 5), _registry);

        error.Should().BeNull();
        var effect = result!.Effect;
        effect.Shots.Should().HaveCount(9);
        effect.Shots.Select(s => s.Target).Should().OnlyContain(c =>
            c.X >= 4 && c.X <= 6 && c.Y >= 4 && c.Y <= 6);
        effect.Shots.Count(s => s.Result.Outcome == ShotOutcome.Hit).Should().Be(1);
        effect.Shots.Count(s => s.Result.Outcome == ShotOutcome.Sunk).Should().Be(1);
        effect.ConsumesTurn.Should().BeTrue();
        target.Fleet.AllSunk.Should().BeTrue();

        caster.Energy.Should().Be(0); // 10 - 10, les touches de la bombe ne rapportent rien
        var slot = caster.PowerSlots.Single();
        slot.Status.Should().Be(PowerSlotStatus.OnCooldown);
        slot.CooldownRemaining.Should().Be(11);
    }

    [Fact]
    public void Refusal_when_energy_is_insufficient_fires_nothing()
    {
        var (caster, target) = BuildPlayers(casterEnergy: 9); // coût = 10
        target.Fleet = new Fleet([new Ship("t-destroyer", ShipType.Destroyer, 2,
            new Coordinate(4, 5), Orientation.Horizontal)]);

        var (result, error) = GameEngine.ActivatePower(caster, target, PowerId.HeavyBomb, At(5, 5), _registry);

        error.Should().Be(ErrorCodes.InsufficientEnergy);
        result.Should().BeNull();
        caster.Energy.Should().Be(9);
        caster.OutgoingBoard.HasBeenShot(new Coordinate(5, 5)).Should().BeFalse();
        caster.PowerSlots.Single().Status.Should().Be(PowerSlotStatus.Ready);
    }

    [Fact]
    public void Refusal_when_every_cell_of_the_zone_was_already_targeted_keeps_the_energy()
    {
        var (caster, target) = BuildPlayers(casterEnergy: 10);
        target.Fleet = new Fleet([new Ship("t-destroyer", ShipType.Destroyer, 2,
            new Coordinate(8, 8), Orientation.Horizontal)]);
        foreach (var cell in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
            caster.OutgoingBoard.MarkShot(new Coordinate(cell.Item1, cell.Item2));

        var (result, error) = GameEngine.ActivatePower(caster, target, PowerId.HeavyBomb, At(0, 0), _registry);

        error.Should().Be(ErrorCodes.InvalidTarget);
        result.Should().BeNull();
        caster.Energy.Should().Be(10);
    }

    [Fact]
    public void Center_outside_the_grid_is_rejected_without_throwing()
    {
        var (caster, target) = BuildPlayers(casterEnergy: 10);
        target.Fleet = new Fleet([]);

        var (_, error) = GameEngine.ActivatePower(caster, target, PowerId.HeavyBomb, At(10, 3), _registry);

        error.Should().Be(ErrorCodes.InvalidTarget);
        caster.Energy.Should().Be(10);
    }

    [Fact]
    public void Bomb_in_a_corner_is_truncated_to_the_four_cells_inside_the_grid()
    {
        var (caster, target) = BuildPlayers(casterEnergy: 10);
        target.Fleet = new Fleet([new Ship("t-destroyer", ShipType.Destroyer, 2,
            new Coordinate(8, 9), Orientation.Horizontal)]);

        var (result, error) = GameEngine.ActivatePower(caster, target, PowerId.HeavyBomb, At(9, 9), _registry);

        error.Should().BeNull();
        result!.Effect.Shots.Select(s => s.Target).Should().BeEquivalentTo(new[]
        {
            new Coordinate(8, 8), new Coordinate(9, 8), new Coordinate(8, 9), new Coordinate(9, 9),
        });
        result.Effect.Shots.Count(s => s.Result.Outcome == ShotOutcome.Sunk).Should().Be(1);
    }

    [Fact]
    public void Cells_already_targeted_are_skipped_not_fired_twice()
    {
        var (caster, target) = BuildPlayers(casterEnergy: 10);
        target.Fleet = new Fleet([new Ship("t-destroyer", ShipType.Destroyer, 2,
            new Coordinate(0, 9), Orientation.Horizontal)]);
        caster.OutgoingBoard.MarkShot(new Coordinate(5, 5));

        var (result, _) = GameEngine.ActivatePower(caster, target, PowerId.HeavyBomb, At(5, 5), _registry);

        result!.Effect.Shots.Should().HaveCount(8);
        result.Effect.Shots.Should().NotContain(s => s.Target == new Coordinate(5, 5));
        caster.ShotsFired.Should().Be(8);
    }

    private static PowerTargetDto At(int x, int y) => new(new CoordinateDto(x, y), null, null, null, null);

    private static (PlayerState caster, PlayerState target) BuildPlayers(int casterEnergy)
    {
        var caster = new PlayerState(PlayerId.New(), "P1", PlayerSlot.One, "t1", 10, 10,
            equippedPowers: [PowerId.HeavyBomb]);
        caster.Energy = casterEnergy;
        var target = new PlayerState(PlayerId.New(), "P2", PlayerSlot.Two, "t2", 10, 10);
        return (caster, target);
    }
}
