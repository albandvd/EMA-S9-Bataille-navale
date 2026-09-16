namespace Naval.Api.Infrastructure;

public static class PlayerTokenAccessor
{
    public const string HeaderName = "X-Player-Token";

    public static string? GetToken(HttpContext ctx) =>
        ctx.Request.Headers.TryGetValue(HeaderName, out var v) ? v.ToString() : null;
}
