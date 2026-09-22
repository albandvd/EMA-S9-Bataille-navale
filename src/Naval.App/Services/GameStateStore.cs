namespace Naval.App.Services;

using System.Text.Json;
using Naval.Shared.Contracts;

/// <summary>
/// Point de contact unique entre l'UI et l'API (REST + hub). Les pages et composants ne parlent
/// jamais directement à <see cref="IGameApiClient"/> ni à <see cref="IGameHubClient"/>.
/// </summary>
public sealed class GameStateStore
{
    private const string SessionKey = "naval.session";

    private readonly IGameApiClient _api;
    private readonly IGameHubClient _hub;
    private readonly LocalStorageService? _storage;

    private string _playerToken = string.Empty;

    public GameStateStore(IGameApiClient api, IGameHubClient? hub = null, LocalStorageService? storage = null)
    {
        _api = api;
        _hub = hub ?? new NullGameHubClient();
        _storage = storage;

        _hub.GameStateChanged += OnHubGameStateChanged;
        _hub.ShotResolved += OnHubShotResolved;
        _hub.OpponentJoined += _ => StateChanged?.Invoke();
        _hub.OpponentLeft += OnHubOpponentLeft;
        _hub.OpponentReconnected += OnHubOpponentReconnected;
        _hub.EmoteReceived += (_, code) => { LastReceivedEmote = code; StateChanged?.Invoke(); };
        _hub.GameOver += OnHubGameOver;
        _hub.ActionRejected += OnHubActionRejected;
    }

    public event Action? StateChanged;

    /// <summary>Un tir vient d'être résolu, qu'il vienne de ce joueur ou de l'adversaire — pour
    /// que l'écran de bataille puisse jouer le bon son même sur un tir reçu en temps réel.</summary>
    public event Action<ShotResultDto>? ShotResolved;

    public GameStateDto? CurrentGame { get; private set; }
    public ApiProblemDto? LastError { get; private set; }

    /// <summary>E-05 : renseigné pendant la grâce de 60 s après une déconnexion adverse.</summary>
    public int? OpponentGraceSecondsRemaining { get; private set; }

    /// <summary>E-08 : dernière emote reçue de l'adversaire, affichée puis effacée par la page.</summary>
    public EmoteCode? LastReceivedEmote { get; private set; }

    public IReadOnlyList<OpenGameDto> OpenGames { get; private set; } = [];
    public IReadOnlyList<PowerDefinitionDto> PowerCatalog { get; private set; } = [];
    public FleetPresetDto? FleetPreset { get; private set; }
    public SpectatorViewDto? Spectator { get; private set; }

    public Task CreateGameAsync(CreateGameRequest request) => RunAsync(async () =>
    {
        var response = await _api.CreateGameAsync(request, CancellationToken.None);
        _playerToken = response.PlayerToken;
        CurrentGame = await _api.GetGameAsync(response.GameId, response.PlayerToken, CancellationToken.None);
        await AfterJoinedOrCreatedAsync();
    });

    public Task JoinGameAsync(JoinGameRequest request) => RunAsync(async () =>
    {
        var response = await _api.JoinGameAsync(request, CancellationToken.None);
        _playerToken = response.PlayerToken;
        CurrentGame = await _api.GetGameAsync(response.GameId, response.PlayerToken, CancellationToken.None);
        await AfterJoinedOrCreatedAsync();
    });

    /// <summary>E-06 : reprend une partie depuis le <c>playerToken</c> persisté. Ne peuple pas
    /// <see cref="LastError"/> quand il n'y a simplement rien à reprendre.</summary>
    public async Task<bool> TryResumeSessionAsync()
    {
        if (_storage is null) return false;

        var raw = await _storage.GetItemAsync(SessionKey);
        if (raw is null) return false;

        SavedSession? saved;
        try
        {
            saved = JsonSerializer.Deserialize<SavedSession>(raw);
        }
        catch (JsonException)
        {
            saved = null;
        }

        if (saved is null)
        {
            await ClearSessionAsync();
            return false;
        }

        var resumed = false;
        await RunAsync(async () =>
        {
            CurrentGame = await _api.GetGameAsync(saved.GameId, saved.PlayerToken, CancellationToken.None);
            _playerToken = saved.PlayerToken;
            resumed = true;

            if (CurrentGame.Mode != GameMode.SinglePlayer &&
                CurrentGame.Status is GameStatus.AwaitingDeployment or GameStatus.InProgress)
            {
                await _hub.ConnectAsync(CurrentGame.GameId, _playerToken, CancellationToken.None);
            }
        });

        if (!resumed)
        {
            await ClearSessionAsync();
        }

        return resumed;
    }

    public Task LoadOpenGamesAsync() => RunAsync(async () =>
    {
        OpenGames = await _api.ListOpenGamesAsync(CancellationToken.None);
    });

    public Task LoadPowerCatalogAsync() => RunAsync(async () =>
    {
        PowerCatalog = await _api.GetPowerCatalogAsync(CancellationToken.None);
    });

    public Task LoadFleetPresetAsync(string presetName) => RunAsync(async () =>
    {
        FleetPreset = await _api.GetFleetPresetAsync(presetName, CancellationToken.None);
    });

    /// <summary>E-09 : ne touche jamais <see cref="CurrentGame"/> — un spectateur n'est pas joueur.</summary>
    public Task LoadSpectatorViewAsync(Guid gameId) => RunAsync(async () =>
    {
        Spectator = await _api.GetSpectatorViewAsync(gameId, CancellationToken.None);
    });

    public Task PlaceFleetAsync(PlaceFleetRequest request) => RunAsync(async () =>
    {
        EnsureGameLoaded();
        CurrentGame = await _api.PlaceFleetAsync(CurrentGame!.GameId, _playerToken, request, CancellationToken.None);
    });

    public Task FireAsync(CoordinateDto target) => RunAsync(async () =>
    {
        EnsureGameLoaded();
        var result = await _api.FireAsync(CurrentGame!.GameId, _playerToken, new FireRequest(target), CancellationToken.None);
        CurrentGame = await _api.GetGameAsync(CurrentGame.GameId, _playerToken, CancellationToken.None);
        ShotResolved?.Invoke(result);
    });

    public Task UsePowerAsync(PowerId id, PowerTargetDto target) => RunAsync(async () =>
    {
        EnsureGameLoaded();
        await _api.UsePowerAsync(CurrentGame!.GameId, _playerToken, new UsePowerRequest(id, target), CancellationToken.None);
        CurrentGame = await _api.GetGameAsync(CurrentGame.GameId, _playerToken, CancellationToken.None);
    });

    public Task ForfeitAsync() => RunAsync(async () =>
    {
        EnsureGameLoaded();
        await _api.ForfeitAsync(CurrentGame!.GameId, _playerToken, CancellationToken.None);
        CurrentGame = await _api.GetGameAsync(CurrentGame.GameId, _playerToken, CancellationToken.None);
    });

    /// <summary>E-08. Ne passe que par le hub : une emote sans adversaire connecté n'a pas de sens.</summary>
    public Task SendEmoteAsync(EmoteCode code) => _hub.SendEmoteAsync(code);

    public void ClearLastReceivedEmote()
    {
        LastReceivedEmote = null;
        StateChanged?.Invoke();
    }

    private async Task AfterJoinedOrCreatedAsync()
    {
        if (CurrentGame is null) return;

        await SaveSessionAsync(CurrentGame.GameId, _playerToken);

        if (CurrentGame.Mode != GameMode.SinglePlayer)
        {
            await _hub.ConnectAsync(CurrentGame.GameId, _playerToken, CancellationToken.None);
        }
    }

    private void OnHubGameStateChanged(GameStateDto state)
    {
        CurrentGame = state;
        if (state.Opponent.IsConnected)
        {
            OpponentGraceSecondsRemaining = null;
        }
        StateChanged?.Invoke();
    }

    private void OnHubShotResolved(ShotResultDto result)
    {
        ShotResolved?.Invoke(result);
        StateChanged?.Invoke();
    }

    private void OnHubOpponentLeft(int graceSeconds)
    {
        OpponentGraceSecondsRemaining = graceSeconds;
        StateChanged?.Invoke();
    }

    private void OnHubOpponentReconnected()
    {
        OpponentGraceSecondsRemaining = null;
        StateChanged?.Invoke();
    }

    private void OnHubGameOver(GameOverDto result)
    {
        _ = ClearSessionAsync();
        StateChanged?.Invoke();
    }

    private void OnHubActionRejected(string code, string message)
    {
        LastError = new ApiProblemDto("about:blank", "Refusé", 409, message, code, null);
        StateChanged?.Invoke();
    }

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
            if (CurrentGame is { Status: GameStatus.Finished or GameStatus.Abandoned })
            {
                await ClearSessionAsync();
            }

            StateChanged?.Invoke();
        }
    }

    private Task SaveSessionAsync(Guid gameId, string playerToken)
    {
        if (_storage is null) return Task.CompletedTask;
        var json = JsonSerializer.Serialize(new SavedSession(gameId, playerToken));
        return _storage.SetItemAsync(SessionKey, json);
    }

    private Task ClearSessionAsync() =>
        _storage?.RemoveItemAsync(SessionKey) ?? Task.CompletedTask;

    private sealed record SavedSession(Guid GameId, string PlayerToken);
}
