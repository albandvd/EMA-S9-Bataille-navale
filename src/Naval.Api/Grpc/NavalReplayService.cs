using Grpc.Core;
using Naval.Api.Services;
using Naval.Shared.Contracts;
using Naval.Shared.Domain.Events;

namespace Naval.Api.Grpc;

/// <summary>
/// Relecture en streaming d'une partie terminée (E-29). Lit le même <see cref="IGameStore"/>
/// que le reste de l'API ; aucune règle de jeu n'est dupliquée ici, ce service ne fait que
/// rejouer le journal d'événements déjà produit par <see cref="GameService"/>.
/// </summary>
public sealed class NavalReplayService(IGameStore store) : NavalReplay.NavalReplayBase
{
    public override async Task StreamReplay(
        ReplayRequest request,
        IServerStreamWriter<ReplayEvent> responseStream,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.GameId, out var gameId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Identifiant de partie invalide."),
                BuildTrailers(ErrorCodes.ValidationFailed));

        var game = await store.GetAsync(gameId, context.CancellationToken);
        if (game is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Partie introuvable."),
                BuildTrailers(ErrorCodes.GameNotFound));

        if (game.Status != GameStatus.Finished)
            throw new RpcException(new Status(StatusCode.FailedPrecondition,
                    "La relecture n'est disponible qu'une fois la partie terminée."),
                BuildTrailers(ErrorCodes.ReplayNotAvailable));

        foreach (var evt in game.Events.OrderBy(e => e.Sequence))
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            await responseStream.WriteAsync(new ReplayEvent
            {
                Sequence = evt.Sequence,
                AtUtc = evt.AtUtc.ToString("O"),
                ActorId = evt.ActorId?.Value.ToString() ?? string.Empty,
                Type = DescribeType(evt),
                Message = evt.Message,
            });
        }
    }

    private static string DescribeType(GameEvent evt) => evt switch
    {
        ShotFiredEvent => "ShotFired",
        TurnChangedEvent => "TurnChanged",
        GameOverEvent => "GameOver",
        PlayerReadyEvent => "PlayerReady",
        PowerActivatedEvent => "PowerActivated",
        PowerResolvedEvent => "PowerResolved",
        EnergyChangedEvent => "EnergyChanged",
        _ => evt.GetType().Name,
    };

    /// <summary>
    /// Porte le code métier stable dans les trailers gRPC, sur le même principe que
    /// l'extension <c>code</c> des <c>ProblemDetails</c> côté REST (voir <c>NavalProblemDetails</c>).
    /// </summary>
    private static Metadata BuildTrailers(string code) => new() { { "code", code } };
}
