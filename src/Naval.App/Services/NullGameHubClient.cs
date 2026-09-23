namespace Naval.App.Services;

using Naval.Shared.Contracts;

/// <summary>
/// Hub inerte : valeur par défaut de <see cref="GameStateStore"/> quand aucun
/// <see cref="IGameHubClient"/> n'est fourni (mode solo, tests). N'émet jamais d'événement.
/// </summary>
public sealed class NullGameHubClient : IGameHubClient
{
#pragma warning disable CS0067 // jamais déclenchés : implémentation inerte, par construction.
    public event Action<GameStateDto>? GameStateChanged;
    public event Action<ShotResultDto>? ShotResolved;
    public event Action<Guid, DateTimeOffset?, int>? TurnChanged;
    public event Action<string>? OpponentJoined;
    public event Action<int>? OpponentLeft;
    public event Action? OpponentReconnected;
    public event Action<Guid, EmoteCode>? EmoteReceived;
    public event Action<GameOverDto>? GameOver;
    public event Action<string, string>? ActionRejected;
#pragma warning restore CS0067

    public Task ConnectAsync(Guid gameId, string playerToken, CancellationToken ct = default) => Task.CompletedTask;
    public Task DisconnectAsync() => Task.CompletedTask;
    public Task FireAsync(int x, int y) => Task.CompletedTask;
    public Task SendEmoteAsync(EmoteCode code) => Task.CompletedTask;
    public Task ForfeitAsync() => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
