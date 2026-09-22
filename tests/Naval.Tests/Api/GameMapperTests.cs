using FluentAssertions;
using Naval.Shared.Contracts;
using Naval.Shared.Contracts.Mapping;
using Naval.Shared.Domain;
using Xunit;

namespace Naval.Tests.Api;

public sealed class GameMapperTests
{
    [Fact]
    public void ToSelfViewDto_reports_the_real_status_of_an_equipped_power()
    {
        var player = new PlayerState(PlayerId.New(), "P1", PlayerSlot.One, "t1", 10, 10,
            equippedPowers: [PowerId.Sonar]);
        player.Energy = 5;

        var dto = GameMapper.ToSelfViewDto(player);

        var slot = dto.Powers.Should().ContainSingle().Subject;
        slot.PowerId.Should().Be(PowerId.Sonar);
        slot.Status.Should().Be(PowerSlotStatus.Ready);
        slot.CanAffordNow.Should().BeTrue(); // Energy=5 >= coût Sonar (3)
    }

    [Fact]
    public void ToOpponentViewDto_exposes_only_the_list_of_equipped_powers_not_their_status()
    {
        var opponent = new PlayerState(PlayerId.New(), "P2", PlayerSlot.Two, "t2", 10, 10,
            equippedPowers: [PowerId.Sonar]);
        var viewer = new PlayerState(PlayerId.New(), "P1", PlayerSlot.One, "t1", 10, 10);

        var dto = GameMapper.ToOpponentViewDto(opponent, viewer);

        dto.EquippedPowers.Should().ContainSingle().Which.Should().Be(PowerId.Sonar);
    }
}
