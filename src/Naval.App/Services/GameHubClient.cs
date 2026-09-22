namespace Naval.App.Services;

using Microsoft.AspNetCore.SignalR.Client;
using Naval.Shared.Contracts;

/// <summary>Implémentation réelle, branchée sur le hub <c>/hub/game</c> de Naval.Api.</summary>
public sealed class GameHubClient(HttpClient http) : IGameHubClient
{
    private HubConnection? _connection;

    public event Action<GameStateDto>? GameStateChanged;
    public event Action<ShotResultDto>? ShotResolved;
    public event Action<Guid, DateTimeOffset?, int>? TurnChanged;
    public event Action<string>? OpponentJoined;
    public event Action<int>? OpponentLeft;
    public event Action? OpponentReconnected;
    public event Action<Guid, EmoteCode>? EmoteReceived;
    public event Action<GameOverDto>? GameOver;
    public event Action<string, string>? ActionRejected;

    public async Task ConnectAsync(Guid gameId, string playerToken, CancellationToken ct = default)
    {
        await DisconnectAsync();

        var hubUrl = new Uri(http.BaseAddress!, GameHubMethods.Path);
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        connection.On<GameStateDto>(nameof(IGameClient.GameStateChanged), s => GameStateChanged?.Invoke(s));
        connection.On<ShotResultDto>(nameof(IGameClient.ShotResolved), r => ShotResolved?.Invoke(r));
        connection.On<Guid, DateTimeOffset?, int>(nameof(IGameClient.TurnChanged),
            (playerId, deadline, turn) => TurnChanged?.Invoke(playerId, deadline, turn));
        connection.On<string>(nameof(IGameClient.OpponentJoined), name => OpponentJoined?.Invoke(name));
        connection.On<int>(nameof(IGameClient.OpponentLeft), grace => OpponentLeft?.Invoke(grace));
        connection.On(nameof(IGameClient.OpponentReconnected), () => OpponentReconnected?.Invoke());
        connection.On<Guid, EmoteCode>(nameof(IGameClient.EmoteReceived),
            (playerId, code) => EmoteReceived?.Invoke(playerId, code));
        connection.On<GameOverDto>(nameof(IGameClient.GameOver), r => GameOver?.Invoke(r));
        connection.On<string, string>(nameof(IGameClient.ActionRejected),
            (code, message) => ActionRejected?.Invoke(code, message));

        connection.Reconnected += _ => connection.InvokeAsync(GameHubMethods.JoinGame, gameId, playerToken);

        await connection.StartAsync(ct);
        await connection.InvokeAsync(GameHubMethods.JoinGame, gameId, playerToken, ct);

        _connection = connection;
    }

    public async Task DisconnectAsync()
    {
        if (_connection is null) return;

        var connection = _connection;
        _connection = null;
        await connection.DisposeAsync();
    }

    public Task FireAsync(int x, int y) =>
        _connection?.InvokeAsync(GameHubMethods.Fire, x, y) ?? Task.CompletedTask;

    public Task SendEmoteAsync(EmoteCode code) =>
        _connection?.InvokeAsync(GameHubMethods.SendEmote, code) ?? Task.CompletedTask;

    public Task ForfeitAsync() =>
        _connection?.InvokeAsync(GameHubMethods.Forfeit) ?? Task.CompletedTask;

    public ValueTask DisposeAsync() =>
        _connection?.DisposeAsync() ?? ValueTask.CompletedTask;
}
