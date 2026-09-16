using Naval.Api.Infrastructure;
using Naval.Api.Services;
using Naval.Shared.Contracts;

namespace Naval.Api.Endpoints;

public static class FleetEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/games/{gameId:guid}").WithTags("Fleet");

        group.MapPost("/fleet", PlaceFleet);
        group.MapPost("/fleet/random", SuggestRandom);
        group.MapPost("/ready", SetReady);
    }

    private static async Task<IResult> PlaceFleet(
        Guid gameId,
        PlaceFleetRequest req,
        HttpContext ctx,
        GameService svc,
        CancellationToken ct)
    {
        var token = PlayerTokenAccessor.GetToken(ctx);
        if (token is null) return NavalProblemDetails.Unauthorized("En-tête X-Player-Token absent.");

        var game = await svc.PlaceFleetAsync(gameId, token, req, ct);
        var viewer = game.GetPlayerByToken(token)!;
        return Results.Ok(Naval.Shared.Contracts.Mapping.GameMapper.ToGameStateDto(game, viewer));
    }

    private static async Task<IResult> SuggestRandom(
        Guid gameId,
        RandomFleetRequest? req,
        HttpContext ctx,
        GameService svc,
        CancellationToken ct)
    {
        var token = PlayerTokenAccessor.GetToken(ctx);
        if (token is null) return NavalProblemDetails.Unauthorized("En-tête X-Player-Token absent.");

        var ships = await svc.SuggestRandomFleetAsync(gameId, token, req?.Seed, ct);
        return Results.Ok(new { ships });
    }

    private static async Task<IResult> SetReady(
        Guid gameId,
        HttpContext ctx,
        GameService svc,
        CancellationToken ct)
    {
        var token = PlayerTokenAccessor.GetToken(ctx);
        if (token is null) return NavalProblemDetails.Unauthorized("En-tête X-Player-Token absent.");

        // SetReady = PlaceFleet déjà placée + marquer IsReady.
        // Pour le socle, le ready est implicite au placement de la flotte.
        // Cet endpoint est un alias pour cohérence avec le contrat OpenAPI.
        var game = await svc.GetGameStateAsync(gameId, token, ct);
        return Results.Ok(game);
    }
}
