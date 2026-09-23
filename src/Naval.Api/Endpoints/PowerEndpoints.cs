using Naval.Api.Infrastructure;
using Naval.Api.Services;
using Naval.Shared.Contracts;

namespace Naval.Api.Endpoints;

public static class PowerEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/games/{gameId:guid}").WithTags("Powers");
        group.MapPost("/powers", UsePower);
    }

    private static async Task<IResult> UsePower(
        Guid gameId,
        UsePowerRequest req,
        HttpContext ctx,
        GameService svc,
        AiTurnService aiSvc,
        CancellationToken ct)
    {
        var token = PlayerTokenAccessor.GetToken(ctx);
        if (token is null) return NavalProblemDetails.Unauthorized("En-tête X-Player-Token absent.");

        var (result, game) = await svc.UsePowerAsync(gameId, token, req, ct);

        // Un pouvoir qui consomme le tour (Bombe lourde) passe la main : en solo, l'IA joue.
        if (game.Mode == GameMode.SinglePlayer && game.Status == GameStatus.InProgress)
            await aiSvc.PlayAsync(gameId, ct);

        return Results.Ok(result);
    }
}
