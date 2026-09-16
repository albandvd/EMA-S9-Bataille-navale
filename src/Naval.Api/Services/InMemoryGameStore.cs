using System.Collections.Concurrent;
using Naval.Shared.Contracts;
using Naval.Shared.Domain;

namespace Naval.Api.Services;

public sealed class InMemoryGameStore : IGameStore
{
    private readonly ConcurrentDictionary<Guid, Game> _store = new();

    public int ActiveCount => _store.Count;

    public Task<Game?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_store.GetValueOrDefault(id));

    public Task SaveAsync(Game game, CancellationToken ct = default)
    {
        _store[game.Id.Value] = game;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<GameSummary>> ListOpenAsync(CancellationToken ct = default)
    {
        var open = _store.Values
            .Where(g => g.Mode == GameMode.PublicOnline && g.Status == GameStatus.AwaitingOpponent)
            .OrderBy(g => g.CreatedAtUtc)
            .Select(g => new GameSummary(
                GameId: g.Id.Value,
                HostName: g.Player1.Name,
                GridWidth: g.GridWidth,
                GridHeight: g.GridHeight,
                FleetPreset: g.FleetPreset,
                PowersEnabled: g.PowersEnabled,
                TurnTimeoutSeconds: g.TurnTimeoutSeconds,
                CreatedAtUtc: g.CreatedAtUtc))
            .ToList();

        return Task.FromResult<IReadOnlyList<GameSummary>>(open);
    }

    public Task<Game?> FindByJoinCodeAsync(string code, CancellationToken ct = default)
    {
        var game = _store.Values
            .FirstOrDefault(g => g.JoinCode != null &&
                                 g.JoinCode.Equals(code, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(game);
    }

    public Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        _store.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
