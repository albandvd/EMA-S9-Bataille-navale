namespace Naval.Shared.Domain.Events;

public abstract record GameEvent(
    int            Sequence,
    DateTimeOffset AtUtc,
    PlayerId?      ActorId,
    string         Message);

public sealed record ShotFiredEvent(
    int Sequence, DateTimeOffset AtUtc, PlayerId ShooterId,
    Coordinate Target, ShotResult Result, string Message)
    : GameEvent(Sequence, AtUtc, ShooterId, Message);

public sealed record TurnChangedEvent(
    int Sequence, DateTimeOffset AtUtc, PlayerId CurrentPlayerId, int TurnNumber, string Message)
    : GameEvent(Sequence, AtUtc, CurrentPlayerId, Message);

public sealed record GameOverEvent(
    int Sequence, DateTimeOffset AtUtc, PlayerId? WinnerId, string Reason, string Message)
    : GameEvent(Sequence, AtUtc, WinnerId, Message);

public sealed record PlayerReadyEvent(
    int Sequence, DateTimeOffset AtUtc, PlayerId PlayerId, string Message)
    : GameEvent(Sequence, AtUtc, PlayerId, Message);
