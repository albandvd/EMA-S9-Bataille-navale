using System.Text.Json.Serialization;
using Naval.Api.Endpoints;
using Naval.Api.Hubs;
using Naval.Api.Infrastructure;
using Naval.Api.Services;
using Naval.Shared.Contracts;
using Naval.Shared.Domain.Powers;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──
builder.Services.AddSingleton<IGameStore, InMemoryGameStore>();
builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<AiTurnService>();
builder.Services.AddSingleton<IPowerHandler, SonarHandler>();
builder.Services.AddSingleton(sp => new PowerRegistry(sp.GetServices<IPowerHandler>()));
builder.Services.AddScoped<GameNotifier>();
builder.Services.AddSingleton<PresenceService>();
builder.Services.AddHostedService<TurnTimeoutService>();

// ── SignalR (E-04) ──
builder.Services.AddSignalR();

// ── JSON ──
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// ── ProblemDetails ──
builder.Services.AddProblemDetails();

// ── OpenAPI ──
builder.Services.AddOpenApi();

// ── CORS ──
builder.Services.AddCors(o => o.AddPolicy("app", p => p
    .WithOrigins(builder.Configuration["Cors:AppOrigin"] ?? "http://localhost:5018")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

// ── Middleware d'erreur global — toutes les exceptions → ProblemDetails ──
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    var ex = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    if (ex is GameException gex)
    {
        ctx.Response.ContentType = "application/problem+json";
        ctx.Response.StatusCode = gex.IsNotFound ? 404
            : gex.IsForbidden ? 403
            : gex.IsConflict ? 409
            : 400;
        var result = NavalProblemDetails.Problem(
            ctx.Response.StatusCode,
            ctx.Response.StatusCode switch
            {
                404 => "Non trouvé",
                403 => "Interdit",
                409 => "Conflit",
                _ => "Requête invalide"
            },
            gex.Message,
            gex.Code);
        await result.ExecuteAsync(ctx);
    }
    else
    {
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/problem+json";
        var result = NavalProblemDetails.Problem(500, "Erreur interne",
            "Une erreur inattendue s'est produite.", "INTERNAL_ERROR");
        await result.ExecuteAsync(ctx);
    }
}));

app.UseCors("app");

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// ── Santé ──
app.MapGet("/health", (IGameStore store) =>
    Results.Ok(new { status = "Healthy", activeGames = store.ActiveCount }))
    .AllowAnonymous();

// ── Endpoints ──
GameEndpoints.Map(app);
FleetEndpoints.Map(app);
ShotEndpoints.Map(app);
CatalogEndpoints.Map(app);
PowerEndpoints.Map(app);

// ── Hub temps réel (E-04) ──
app.MapHub<GameHub>(GameHubMethods.Path).RequireCors("app");

app.Run();

// Nécessaire pour WebApplicationFactory dans les tests
public partial class Program { }
