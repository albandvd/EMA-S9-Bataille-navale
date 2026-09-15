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

    public Task CreateGameAsync(CreateGameRequest request) => RunAsync(async () =>
    {
        var response = await api.CreateGameAsync(request, CancellationToken.None);
        _playerToken = response.PlayerToken;
        CurrentGame = await api.GetGameAsync(response.GameId, response.PlayerToken, CancellationToken.None);
    });

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
