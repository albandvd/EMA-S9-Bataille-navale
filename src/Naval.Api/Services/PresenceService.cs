using System.Collections.Concurrent;

namespace Naval.Api.Services;

/// <summary>
/// Suivi des connexions SignalR actives (E-04) et de la grâce de déconnexion de 60 s (E-05).
/// Singleton : les timers de grâce doivent survivre à la portée d'une seule invocation de hub.
/// </summary>
public sealed class PresenceService
{
    public const int DisconnectGraceSeconds = 60;

    public sealed record ConnectionInfo(Guid GameId, Guid PlayerId, string Token);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<string, ConnectionInfo> _connections = new();
    private readonly ConcurrentDictionary<(Guid GameId, Guid PlayerId), CancellationTokenSource> _graceTimers = new();

    public PresenceService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void Register(string connectionId, Guid gameId, Guid playerId, string token) =>
        _connections[connectionId] = new ConnectionInfo(gameId, playerId, token);

    public ConnectionInfo? Get(string connectionId) => _connections.GetValueOrDefault(connectionId);

    public ConnectionInfo? Remove(string connectionId)
    {
        _connections.TryRemove(connectionId, out var info);
        return info;
    }

    public void CancelGrace(Guid gameId, Guid playerId)
    {
        if (_graceTimers.TryRemove((gameId, playerId), out var cts))
            cts.Cancel();
    }

    /// <summary>Démarre le délai de 60 s après lequel une déconnexion devient un abandon.</summary>
    public void StartGrace(Guid gameId, Guid playerId)
    {
        var cts = new CancellationTokenSource();
        _graceTimers[(gameId, playerId)] = cts;
        _ = ExpireAsync(gameId, playerId, cts.Token);
    }

    private async Task ExpireAsync(Guid gameId, Guid playerId, CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(DisconnectGraceSeconds), ct);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        _graceTimers.TryRemove((gameId, playerId), out _);

        // PresenceService est singleton : GameService/GameNotifier sont scoped, donc résolus
        // dans une portée dédiée plutôt que capturés depuis le hub qui a déclenché ce délai.
        using var scope = _scopeFactory.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<GameService>();
        var notifier = scope.ServiceProvider.GetRequiredService<GameNotifier>();

        var result = await svc.AbandonDueToDisconnectionAsync(gameId, playerId, CancellationToken.None);
        if (result is not null)
            await notifier.NotifyGameOverAsync(gameId, result);
    }
}
