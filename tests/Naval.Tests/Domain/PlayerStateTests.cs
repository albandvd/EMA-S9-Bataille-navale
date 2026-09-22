using FluentAssertions;
using Naval.Shared.Contracts;
using Naval.Shared.Domain;
using Xunit;

namespace Naval.Tests.Domain;

public sealed class PlayerStateTests
{
    [Fact]
    public void Equipping_a_power_creates_one_ready_slot_with_its_starting_uses()
    {
        var player = new PlayerState(PlayerId.New(), "P1", PlayerSlot.One, "t1", 10, 10,
            equippedPowers: [PowerId.Sonar]);

        player.EquippedPowers.Should().ContainSingle().Which.Should().Be(PowerId.Sonar);
        var slot = player.PowerSlots.Should().ContainSingle().Subject;
        slot.PowerId.Should().Be(PowerId.Sonar);
        slot.Status.Should().Be(PowerSlotStatus.Ready);
        slot.UsesLeft.Should().Be(-1); // Sonar : MaxUses illimité dans PowerCatalog
    }

    [Fact]
    public void No_equipped_powers_by_default()
    {
        var player = new PlayerState(PlayerId.New(), "P1", PlayerSlot.One, "t1", 10, 10);

        player.EquippedPowers.Should().BeEmpty();
        player.PowerSlots.Should().BeEmpty();
    }
}
