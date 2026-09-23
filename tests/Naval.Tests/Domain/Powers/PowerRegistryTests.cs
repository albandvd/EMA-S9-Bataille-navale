using FluentAssertions;
using Naval.Shared.Contracts;
using Naval.Shared.Domain;
using Naval.Shared.Domain.Powers;
using Xunit;

namespace Naval.Tests.Domain.Powers;

public sealed class PowerRegistryTests
{
    private sealed class StubHandler : IPowerHandler
    {
        public PowerId Id => PowerId.Sonar;
        public string? Validate(PlayerState caster, PlayerState target, PowerTargetDto powerTarget) => null;
        public PowerEffectResult Execute(PlayerState caster, PlayerState target, PowerTargetDto powerTarget) =>
            new(RevealedCount: 0, RevealedCells: [], Shots: [], ConsumesTurn: false, Message: "stub");
    }

    [Fact]
    public void Find_returns_the_registered_handler_for_its_id()
    {
        var registry = new PowerRegistry([new StubHandler()]);

        registry.Find(PowerId.Sonar).Should().BeOfType<StubHandler>();
    }

    [Fact]
    public void Find_returns_null_for_an_unregistered_power()
    {
        var registry = new PowerRegistry([new StubHandler()]);

        registry.Find(PowerId.TripleSalvo).Should().BeNull();
    }
}
