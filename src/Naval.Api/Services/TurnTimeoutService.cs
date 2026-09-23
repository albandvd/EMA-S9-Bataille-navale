namespace Naval.Api.Services;

/// <summary>
/// E-07. Scanne les parties en bataille une fois par seconde et joue un tir aléatoire pour tout
/// joueur dont la date limite de tour est dépassée. Portée dédiée à chaque passage : ce service
/// est un singleton hébergé, <see cref="GameService"/> et <see cref="GameNotifier"/> sont scoped.
/// </summary>
public sealed class TurnTimeoutService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly IServiceScopeFactory _scopeFactory;

    public TurnTimeoutService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IGameStore>();
            var svc = scope.ServiceProvider.GetRequiredService<GameService>();
            var notifier = scope.ServiceProvider.GetRequiredService<GameNotifier>();

            var games = await store.ListInProgressAsync(stoppingToken);
            var due = games.Where(g => g.TurnDeadlineUtc is { } deadline && deadline <= DateTimeOffset.UtcNow);

            foreach (var game in due)
            {
                var result = await svc.PlayTimeoutShotAsync(game.Id.Value, stoppingToken);
                if (result is null) continue;

                var refreshed = await store.GetAsync(game.Id.Value, stoppingToken);
                if (refreshed is not null)
                    await notifier.NotifyShotResultAsync(refreshed, result);
            }
        }
    }
}
