namespace Naval.Shared.Domain;

public enum PlacementErrorKind
{
    OutOfBounds,
    OverlappingShips,
    FleetIncomplete,
    DuplicateShipType,
}

public sealed record PlacementError(PlacementErrorKind Kind, string Detail);
