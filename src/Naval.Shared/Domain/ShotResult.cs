using Naval.Shared.Contracts;

namespace Naval.Shared.Domain;

public sealed record ShotResult(
    ShotOutcome Outcome,
    Ship? SunkShip,
    int EnergyGained);
