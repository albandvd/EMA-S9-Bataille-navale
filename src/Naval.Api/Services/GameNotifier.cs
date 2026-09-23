using Microsoft.AspNetCore.SignalR;
using Naval.Api.Hubs;
using Naval.Shared.Contracts;
using Naval.Shared.Contracts.Mapping;
using Naval.Shared.Domain;

namespace Naval.Api.Services;

/// <summary>
/// Traduit les résultats de <see cref="GameService"/> en notifications SignalR. Aucune règle de
/// jeu ici, uniquement du mapping DTO et de la diffusion vers les bons groupes — appelée aussi
/// bien depuis <see cref="Hubs.GameHub"/> que depuis les endpoints REST, pour que le tir soumis
/// par l'un ou l'autre chemin pousse la même notification temps réel à l'adversaire.
/// </summary>
public sealed class GameNotifier
{
    private readonly IHubContext<GameHub, IGameClient> _hub;
    private readonly IGameStore _store;

    public GameNotifier(IHubContext<GameHub, IGameClient> hub, IGameStore store)
    {
        _hub = hub;
        _store = store;
    }

    public static string GameGroup(Guid gameId) => $"game:{gameId}";
    public static string PlayerGroup(Guid playerId) => $"player:{playerId}";

    /// <summary>Pousse à chacun des deux joueurs sa propre vue filtrée.</summary>
    public async Task PushGameStateAsync(Guid gameId, CancellationToken ct = default)
    {
        var game = await _store.GetAsync(gameId, ct);
        if (game is null) return;

        await _hub.Clients.Group(PlayerGroup(game.Player1.Id.Value))
            .GameStateChanged(GameMapper.ToGameStateDto(game, game.Player1));
        await _hub.Clients.Group(PlayerGroup(game.Player2.Id.Value))
            .GameStateChanged(GameMapper.ToGameStateDto(game, game.Player2));
    }

    public async Task NotifyOpponentJoinedAsync(Guid gameId, Guid hostPlayerId, string joinerName)
    {
        await _hub.Clients.Group(PlayerGroup(hostPlayerId)).OpponentJoined(joinerName);
        await PushGameStateAsync(gameId);
    }

    /// <summary>Tir résolu, quel qu'en soit le déclencheur (REST, hub, ou timer de tour).</summary>
    public async Task NotifyShotResultAsync(Game game, ShotResultDto result)
    {
        await _hub.Clients.Group(GameGroup(game.Id.Value)).ShotResolved(result);
        await PushGameStateAsync(game.Id.Value);

        if (result.GameOver)
        {
            var dto = GameMapper.ToGameOverDto(game, "FleetDestroyed");
            await _hub.Clients.Group(GameGroup(game.Id.Value)).GameOver(dto);
        }
        else
        {
            await NotifyTurnChangedAsync(game);
        }
    }

    public Task NotifyTurnChangedAsync(Game game) =>
        game.CurrentPlayerId is { } currentId
            ? _hub.Clients.Group(GameGroup(game.Id.Value))
                .TurnChanged(currentId.Value, game.TurnDeadlineUtc, game.TurnNumber)
            : Task.CompletedTask;

    /// <summary>
    /// Pousse aussi l'état complet : un abandon ou une déconnexion n'ont pas déjà transité par
    /// <see cref="NotifyShotResultAsync"/>, donc rien d'autre ne mettrait à jour le
    /// <c>Status</c>/<c>WinnerId</c> que les clients lisent pour naviguer vers l'écran de fin.
    /// </summary>
    public async Task NotifyGameOverAsync(Guid gameId, GameOverDto result)
    {
        await _hub.Clients.Group(GameGroup(gameId)).GameOver(result);
        await PushGameStateAsync(gameId);
    }

    public Task NotifyEmoteAsync(Guid gameId, Guid playerId, EmoteCode code) =>
        _hub.Clients.Group(GameGroup(gameId)).EmoteReceived(playerId, code);
}
