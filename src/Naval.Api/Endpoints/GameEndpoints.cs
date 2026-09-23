using Naval.Api.Infrastructure;
using Naval.Api.Services;
using Naval.Shared.Contracts;

namespace Naval.Api.Endpoints;

public static class GameEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/games").WithTags("Games");

        group.MapPost("/", CreateGame).AllowAnonymous();
        group.MapPost("/join", JoinGame).AllowAnonymous();
        group.MapGet("/open", ListOpenGames).AllowAnonymous();
        group.MapGet("/{gameId:guid}", GetGame);
        group.MapPost("/{gameId:guid}/forfeit", Forfeit);
        group.MapGet("/{gameId:guid}/events", GetEvents);
        group.MapGet("/{gameId:guid}/spectate", Spectate).AllowAnonymous();
    }

    private static async Task<IResult> CreateGame(
        CreateGameRequest req,
        GameService svc,
        CancellationToken ct)
    {
        var (game, token) = await svc.CreateGameAsync(req, ct);
        var response = new CreateGameResponse(game.Id.Value, token, game.JoinCode, game.Status);
        return Results.Created($"/api/games/{game.Id.Value}", response);
    }

    private static async Task<IResult> JoinGame(
        JoinGameRequest req,
        GameService svc,
        GameNotifier notifier,
        CancellationToken ct)
    {
        var (game, token) = await svc.JoinGameAsync(req, ct);
        await notifier.NotifyOpponentJoinedAsync(game.Id.Value, game.Player1.Id.Value, game.Player2.Name);
        return Results.Ok(new JoinGameResponse(
            game.Id.Value, token, game.Status, game.Player1.Name));
    }

    private static async Task<IResult> ListOpenGames(
        GameService svc,
        int limit = 20,
        CancellationToken ct = default)
    {
        var games = await svc.ListOpenGamesAsync(Math.Clamp(limit, 1, 50), ct);
        return Results.Ok(games);
    }

    private static async Task<IResult> GetGame(
        Guid gameId,
        HttpContext ctx,
        GameService svc,
        CancellationToken ct)
    {
        var token = PlayerTokenAccessor.GetToken(ctx);
        if (token is null) return NavalProblemDetails.Unauthorized("En-tête X-Player-Token absent.");

        var state = await svc.GetGameStateAsync(gameId, token, ct);
        return Results.Ok(state);
    }

    private static async Task<IResult> Forfeit(
        Guid gameId,
        HttpContext ctx,
        GameService svc,
        GameNotifier notifier,
        CancellationToken ct)
    {
        var token = PlayerTokenAccessor.GetToken(ctx);
        if (token is null) return NavalProblemDetails.Unauthorized("En-tête X-Player-Token absent.");

        var result = await svc.ForfeitAsync(gameId, token, ct);
        await notifier.NotifyGameOverAsync(gameId, result);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetEvents(
        Guid gameId,
        HttpContext ctx,
        GameService svc,
        int since = 0,
        CancellationToken ct = default)
    {
        var token = PlayerTokenAccessor.GetToken(ctx);
        if (token is null) return NavalProblemDetails.Unauthorized("En-tête X-Player-Token absent.");

        var events = await svc.GetEventsAsync(gameId, token, since, ct);
        return Results.Ok(events);
    }

    private static async Task<IResult> Spectate(
        Guid gameId,
        GameService svc,
        CancellationToken ct)
    {
        var view = await svc.GetSpectatorViewAsync(gameId, ct);
        return Results.Ok(view);
    }
}
