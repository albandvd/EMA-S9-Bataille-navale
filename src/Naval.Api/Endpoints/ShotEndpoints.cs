using Naval.Api.Infrastructure;
using Naval.Api.Services;
using Naval.Shared.Contracts;

namespace Naval.Api.Endpoints;

public static class ShotEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/games/{gameId:guid}").WithTags("Play");

        group.MapPost("/shots", Fire);
        group.MapPost("/emotes", SendEmote);
    }

    private static async Task<IResult> Fire(
        Guid gameId,
        FireRequest req,
        HttpContext ctx,
        GameService svc,
        AiTurnService aiSvc,
        GameNotifier notifier,
        CancellationToken ct)
    {
        var token = PlayerTokenAccessor.GetToken(ctx);
        if (token is null) return NavalProblemDetails.Unauthorized("En-tête X-Player-Token absent.");

        var (result, game) = await svc.FireAsync(gameId, token, req, ct);
        await notifier.NotifyShotResultAsync(game, result);

        // En mode solo, joue le tour IA immédiatement après
        if (game.Mode == Naval.Shared.Contracts.GameMode.SinglePlayer &&
            game.Status == Naval.Shared.Contracts.GameStatus.InProgress)
        {
            var aiOutcome = await aiSvc.PlayAsync(gameId, ct);
            if (aiOutcome is { } outcome)
                await notifier.NotifyShotResultAsync(outcome.game, outcome.result);
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> SendEmote(
        Guid gameId,
        SendEmoteRequest req,
        HttpContext ctx,
        GameService svc,
        GameNotifier notifier,
        CancellationToken ct)
    {
        var token = PlayerTokenAccessor.GetToken(ctx);
        if (token is null) return NavalProblemDetails.Unauthorized("En-tête X-Player-Token absent.");

        var (_, player) = await svc.ResolvePlayerAsync(gameId, token, ct);
        await notifier.NotifyEmoteAsync(gameId, player.Id.Value, req.Code);
        return Results.NoContent();
    }
}
