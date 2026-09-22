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
        CancellationToken ct)
    {
        var token = PlayerTokenAccessor.GetToken(ctx);
        if (token is null) return NavalProblemDetails.Unauthorized("En-tête X-Player-Token absent.");

        var (result, _) = await svc.UsePowerAsync(gameId, token, req, ct);
        return Results.Ok(result);
    }
}
