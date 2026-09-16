using Naval.Shared.Domain;

namespace Naval.Api.Services;

public interface IGameStore
{
    Task<Game?> GetAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(Game game, CancellationToken ct = default);
    Task<IReadOnlyList<GameSummary>> ListOpenAsync(CancellationToken ct = default);
    Task<Game?> FindByJoinCodeAsync(string code, CancellationToken ct = default);
    Task RemoveAsync(Guid id, CancellationToken ct = default);
    int ActiveCount { get; }
}
