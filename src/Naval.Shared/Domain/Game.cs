using Naval.Shared.Contracts;
using Naval.Shared.Domain.Events;

namespace Naval.Shared.Domain;

public sealed class Game
{
    private int _eventSequence;
    private readonly List<GameEvent> _events = [];

    public GameId Id { get; }
    public GameMode Mode { get; }
    public GameStatus Status { get; set; }
    public string? JoinCode { get; }
    public string FleetPreset { get; }
    public int GridWidth { get; }
    public int GridHeight { get; }
    public bool PowersEnabled { get; }
    public int TurnTimeoutSeconds { get; }
    public DateTimeOffset CreatedAtUtc { get; }

    public PlayerState Player1 { get; }
    public PlayerState Player2 { get; }

    public int TurnNumber { get; set; }
    public PlayerId? CurrentPlayerId { get; set; }
    public DateTimeOffset? TurnDeadlineUtc { get; set; }
    public Guid? WinnerId { get; set; }

    /// <summary>Sémaphore pour sérialiser les mutations concurrentes de cette partie.</summary>
    public SemaphoreSlim Lock { get; } = new(1, 1);

    public IReadOnlyList<GameEvent> Events => _events;

    public Game(
        GameId id,
        GameMode mode,
        string? joinCode,
        string fleetPreset,
        int gridWidth,
        int gridHeight,
        bool powersEnabled,
        int turnTimeoutSeconds,
        PlayerState player1,
        PlayerState player2)
    {
        Id = id;
        Mode = mode;
        JoinCode = joinCode;
        FleetPreset = fleetPreset;
        GridWidth = gridWidth;
        GridHeight = gridHeight;
        PowersEnabled = powersEnabled;
        TurnTimeoutSeconds = turnTimeoutSeconds;
        Player1 = player1;
        Player2 = player2;
        CreatedAtUtc = DateTimeOffset.UtcNow;

        Status = mode == GameMode.SinglePlayer
            ? GameStatus.AwaitingDeployment
            : GameStatus.AwaitingOpponent;
    }

    public PlayerState? GetPlayerByToken(string token)
    {
        if (Player1.Token == token) return Player1;
        if (Player2.Token == token) return Player2;
        return null;
    }

    public PlayerState? GetPlayerById(PlayerId id)
    {
        if (Player1.Id == id) return Player1;
        if (Player2.Id == id) return Player2;
        return null;
    }

    public PlayerState GetOpponent(PlayerId id) =>
        Player1.Id == id ? Player2 : Player1;

    public GameEvent AddEvent(GameEvent evt)
    {
        _events.Add(evt);
        return evt;
    }

    public int NextSequence() => ++_eventSequence;

    public IReadOnlyList<GameEvent> EventsSince(int sequence) =>
        _events.Where(e => e.Sequence > sequence).ToList();
}
