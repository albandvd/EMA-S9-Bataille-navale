namespace Naval.App.Services;

using Naval.Shared.Contracts;

/// <summary>
/// Abstraction du hub temps réel (E-04). <see cref="GameStateStore"/> est le seul consommateur,
/// au même titre que pour <see cref="IGameApiClient"/> : les composants ne s'y abonnent jamais
/// directement.
/// </summary>
public interface IGameHubClient : IAsyncDisposable
{
    event Action<GameStateDto>? GameStateChanged;
    event Action<ShotResultDto>? ShotResolved;
    event Action<Guid, DateTimeOffset?, int>? TurnChanged;
    event Action<string>? OpponentJoined;
    event Action<int>? OpponentLeft;
    event Action? OpponentReconnected;
    event Action<Guid, EmoteCode>? EmoteReceived;
    event Action<GameOverDto>? GameOver;
    event Action<string, string>? ActionRejected;

    /// <summary>Ouvre la connexion (si nécessaire) et rejoint le groupe de la partie.</summary>
    Task ConnectAsync(Guid gameId, string playerToken, CancellationToken ct = default);

    Task DisconnectAsync();

    Task FireAsync(int x, int y);

    Task SendEmoteAsync(EmoteCode code);

    Task ForfeitAsync();
}
