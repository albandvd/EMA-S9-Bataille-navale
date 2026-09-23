namespace Naval.Tests.App;

using Naval.App.Services;
using Naval.Shared.Contracts;

/// <summary>
/// Double de test pour <see cref="IGameHubClient"/> : les méthodes publiques déclenchent
/// directement les événements, comme le ferait le vrai hub à la réception d'un message
/// SignalR. Permet de tester le câblage de <see cref="GameStateStore"/> sans serveur réel.
/// </summary>
public sealed class FakeGameHubClient : IGameHubClient
{
    public List<(Guid GameId, string Token)> ConnectCalls { get; } = [];
    public int DisconnectCalls { get; private set; }
    public List<(int X, int Y)> FireCalls { get; } = [];
    public List<EmoteCode> EmoteCalls { get; } = [];
    public int ForfeitCalls { get; private set; }

    public event Action<GameStateDto>? GameStateChanged;
    public event Action<ShotResultDto>? ShotResolved;
#pragma warning disable CS0067 // pas encore déclenchés par les tests actuels, mais requis par l'interface.
    public event Action<Guid, DateTimeOffset?, int>? TurnChanged;
    public event Action<string>? OpponentJoined;
#pragma warning restore CS0067
    public event Action<int>? OpponentLeft;
    public event Action? OpponentReconnected;
    public event Action<Guid, EmoteCode>? EmoteReceived;
    public event Action<GameOverDto>? GameOver;
    public event Action<string, string>? ActionRejected;

    public Task ConnectAsync(Guid gameId, string playerToken, CancellationToken ct = default)
    {
        ConnectCalls.Add((gameId, playerToken));
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        DisconnectCalls++;
        return Task.CompletedTask;
    }

    public Task FireAsync(int x, int y)
    {
        FireCalls.Add((x, y));
        return Task.CompletedTask;
    }

    public Task SendEmoteAsync(EmoteCode code)
    {
        EmoteCalls.Add(code);
        return Task.CompletedTask;
    }

    public Task ForfeitAsync()
    {
        ForfeitCalls++;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void RaiseGameStateChanged(GameStateDto state) => GameStateChanged?.Invoke(state);
    public void RaiseShotResolved(ShotResultDto result) => ShotResolved?.Invoke(result);
    public void RaiseOpponentLeft(int graceSeconds) => OpponentLeft?.Invoke(graceSeconds);
    public void RaiseOpponentReconnected() => OpponentReconnected?.Invoke();
    public void RaiseEmoteReceived(Guid playerId, EmoteCode code) => EmoteReceived?.Invoke(playerId, code);
    public void RaiseGameOver(GameOverDto result) => GameOver?.Invoke(result);
    public void RaiseActionRejected(string code, string message) => ActionRejected?.Invoke(code, message);
}
