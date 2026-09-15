namespace Naval.App.Services;

using Naval.Shared.Contracts;

/// <summary>
/// Point de contact unique entre l'UI et l'API. Les pages et composants ne parlent jamais
/// directement à <see cref="IGameApiClient"/>.
/// </summary>
public sealed class GameStateStore(IGameApiClient api)
{
    public event Action? StateChanged;

    public GameStateDto? CurrentGame { get; private set; }
    public ApiProblemDto? LastError { get; private set; }

    private string _playerToken = string.Empty;

    public IReadOnlyList<OpenGameDto> OpenGames { get; private set; } = [];
    public IReadOnlyList<PowerDefinitionDto> PowerCatalog { get; private set; } = [];
    public FleetPresetDto? FleetPreset { get; private set; }

    public Task CreateGameAsync(CreateGameRequest request) => RunAsync(async () =>
    {
        var response = await api.CreateGameAsync(request, CancellationToken.None);
        _playerToken = response.PlayerToken;
        CurrentGame = await api.GetGameAsync(response.GameId, response.PlayerToken, CancellationToken.None);
    });

    public Task JoinGameAsync(JoinGameRequest request) => RunAsync(async () =>
    {
        var response = await api.JoinGameAsync(request, CancellationToken.None);
        _playerToken = response.PlayerToken;
        CurrentGame = await api.GetGameAsync(response.GameId, response.PlayerToken, CancellationToken.None);
    });

    public Task LoadOpenGamesAsync() => RunAsync(async () =>
    {
        OpenGames = await api.ListOpenGamesAsync(CancellationToken.None);
    });

    public Task LoadPowerCatalogAsync() => RunAsync(async () =>
    {
        PowerCatalog = await api.GetPowerCatalogAsync(CancellationToken.None);
    });

    public Task LoadFleetPresetAsync(string presetName) => RunAsync(async () =>
    {
        FleetPreset = await api.GetFleetPresetAsync(presetName, CancellationToken.None);
    });

    public Task PlaceFleetAsync(PlaceFleetRequest request) => RunAsync(async () =>
    {
        EnsureGameLoaded();
        CurrentGame = await api.PlaceFleetAsync(CurrentGame!.GameId, _playerToken, request, CancellationToken.None);
    });

    public Task FireAsync(CoordinateDto target) => RunAsync(async () =>
    {
        EnsureGameLoaded();
        await api.FireAsync(CurrentGame!.GameId, _playerToken, new FireRequest(target), CancellationToken.None);
        CurrentGame = await api.GetGameAsync(CurrentGame.GameId, _playerToken, CancellationToken.None);
    });

    public Task UsePowerAsync(PowerId id, PowerTargetDto target) => RunAsync(async () =>
    {
        EnsureGameLoaded();
        await api.UsePowerAsync(CurrentGame!.GameId, _playerToken, new UsePowerRequest(id, target), CancellationToken.None);
        CurrentGame = await api.GetGameAsync(CurrentGame.GameId, _playerToken, CancellationToken.None);
    });

    public Task ForfeitAsync() => RunAsync(async () =>
    {
        EnsureGameLoaded();
        CurrentGame = await api.ForfeitAsync(CurrentGame!.GameId, _playerToken, CancellationToken.None);
    });

    private void EnsureGameLoaded()
    {
        if (CurrentGame is null)
        {
            throw new InvalidOperationException("Aucune partie chargée.");
        }
    }

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
            LastError = null;
        }
        catch (GameApiException ex)
        {
            LastError = ex.Problem;
        }
        finally
        {
            StateChanged?.Invoke();
        }
    }
}
