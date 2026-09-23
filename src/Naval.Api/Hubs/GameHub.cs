using Microsoft.AspNetCore.SignalR;
using Naval.Api.Services;
using Naval.Shared.Contracts;
using Naval.Shared.Domain;

namespace Naval.Api.Hubs;

/// <summary>
/// E-04. Chaque méthode appelle <see cref="GameService"/> — le même service que les endpoints
/// REST — puis diffuse via <see cref="GameNotifier"/>. Aucune règle de jeu ici.
/// </summary>
public sealed class GameHub : Hub<IGameClient>
{
    private readonly GameService _svc;
    private readonly PresenceService _presence;
    private readonly GameNotifier _notifier;

    public GameHub(GameService svc, PresenceService presence, GameNotifier notifier)
    {
        _svc = svc;
        _presence = presence;
        _notifier = notifier;
    }

    /// <summary>
    /// À appeler juste après la connexion. Rattache la connexion à la partie, et ne signale une
    /// reconnexion (E-06) que si le joueur était marqué déconnecté — une adhésion initiale ne
    /// déclenche pas OpponentReconnected, seul <c>POST /api/games/join</c> émet OpponentJoined.
    /// </summary>
    public Task JoinGame(Guid gameId, string token) => GuardAsync(async () =>
    {
        var (_, player) = await _svc.ResolvePlayerAsync(gameId, token, Context.ConnectionAborted);
        var wasDisconnected = !player.IsConnected;

        await Groups.AddToGroupAsync(Context.ConnectionId, GameNotifier.GameGroup(gameId));
        await Groups.AddToGroupAsync(Context.ConnectionId, GameNotifier.PlayerGroup(player.Id.Value));
        _presence.Register(Context.ConnectionId, gameId, player.Id.Value, token);
        _presence.CancelGrace(gameId, player.Id.Value);

        if (wasDisconnected)
        {
            await _svc.SetPresenceAsync(gameId, player.Id.Value, connected: true, Context.ConnectionAborted);
            await Clients.OthersInGroup(GameNotifier.GameGroup(gameId)).OpponentReconnected();
        }

        await _notifier.PushGameStateAsync(gameId);
    });

    public Task LeaveGame() => GuardAsync(async () =>
    {
        var info = _presence.Remove(Context.ConnectionId);
        if (info is null) return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GameNotifier.GameGroup(info.GameId));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GameNotifier.PlayerGroup(info.PlayerId));
    });

    /// <summary>
    /// Le ready est implicite au placement de la flotte (comme côté REST, cf. FleetEndpoints) ;
    /// cette méthode ne fait que resynchroniser l'appelant, pour rester alignée sur le contrat
    /// du hub documenté dans docs/03-architecture.md.
    /// </summary>
    public Task SetReady() => GuardAsync(async () =>
    {
        var info = RequireConnection();
        await _notifier.PushGameStateAsync(info.GameId);
    });

    public Task Fire(int x, int y) => GuardAsync(async () =>
    {
        var info = RequireConnection();
        var (result, game) = await _svc.FireAsync(
            info.GameId, info.Token, new FireRequest(new CoordinateDto(x, y)), Context.ConnectionAborted);
        await _notifier.NotifyShotResultAsync(game, result);
    });

    /// <summary>Même chemin que POST /api/games/{id}/powers : GameService puis GameNotifier.</summary>
    public Task UsePower(PowerId powerId, PowerTargetDto target) => GuardAsync(async () =>
    {
        var info = RequireConnection();
        var (result, game) = await _svc.UsePowerAsync(
            info.GameId, info.Token, new UsePowerRequest(powerId, target), Context.ConnectionAborted);
        await _notifier.NotifyPowerResultAsync(game, result);
    });

    public Task CancelCharge(PowerId powerId) => GuardAsync(() =>
        throw new GameException(ErrorCodes.PowerNotEquipped, "Les pouvoirs ne sont pas encore disponibles."));

    public Task SendEmote(EmoteCode code) => GuardAsync(async () =>
    {
        var info = RequireConnection();
        await _notifier.NotifyEmoteAsync(info.GameId, info.PlayerId, code);
    });

    public Task Forfeit() => GuardAsync(async () =>
    {
        var info = RequireConnection();
        var result = await _svc.ForfeitAsync(info.GameId, info.Token, Context.ConnectionAborted);
        await _notifier.NotifyGameOverAsync(info.GameId, result);
    });

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var info = _presence.Remove(Context.ConnectionId);
        if (info is not null)
        {
            await _svc.SetPresenceAsync(info.GameId, info.PlayerId, connected: false, CancellationToken.None);
            await Clients.Group(GameNotifier.GameGroup(info.GameId)).OpponentLeft(PresenceService.DisconnectGraceSeconds);
            await _notifier.PushGameStateAsync(info.GameId);
            _presence.StartGrace(info.GameId, info.PlayerId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private PresenceService.ConnectionInfo RequireConnection() =>
        _presence.Get(Context.ConnectionId)
            ?? throw new GameException(ErrorCodes.NotAPlayer, "Rejoignez la partie (JoinGame) avant d'agir.");

    /// <summary>
    /// Traduit un refus métier en <see cref="IGameClient.ActionRejected"/> plutôt que de laisser
    /// SignalR renvoyer une exception brute à l'appelant.
    /// </summary>
    private async Task GuardAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (GameException ex)
        {
            await Clients.Caller.ActionRejected(ex.Code, ex.Message);
        }
    }
}
