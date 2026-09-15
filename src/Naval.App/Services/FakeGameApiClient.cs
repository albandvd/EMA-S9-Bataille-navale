namespace Naval.App.Services;

using Naval.Shared.Contracts;

/// <summary>
/// Implémentation en mémoire, déterministe, de <see cref="IGameApiClient"/>. Utilisée tant que
/// Naval.Api n'existe pas. Seuls CreateGame et GetGame ont une logique réelle ; les autres
/// méthodes seront complétées à la Task 10.
/// </summary>
public sealed class FakeGameApiClient : IGameApiClient
{
    private const string PlayerToken = "fake-player-token";
    private GameStateDto? _game;

    public Task<CreateGameResponse> CreateGameAsync(CreateGameRequest request, CancellationToken ct)
    {
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var opponentId = Guid.NewGuid();

        var emptyRows = Enumerable.Repeat(new string('.', request.GridWidth), request.GridHeight).ToList();
        var selfBoard = new BoardViewDto(request.GridWidth, request.GridHeight, emptyRows);
        var opponentBoard = new BoardViewDto(request.GridWidth, request.GridHeight, emptyRows);

        var powers = request.Powers
            .Select(id => new PowerSlotDto(id, PowerSlotStatus.Ready, 0, 0, -1, true))
            .ToList();

        var self = new SelfViewDto(playerId, request.PlayerName, PlayerSlot.One, 0, selfBoard, [], powers);
        var opponent = new OpponentViewDto(opponentId, "IA", true, true, 0, 0, 0, opponentBoard,
            [], new OpponentChargeDto(false, null, null, null), []);

        _game = new GameStateDto(gameId, request.Mode, GameStatus.AwaitingDeployment, null, 0,
            null, null, null, self, opponent, [], 0);

        return Task.FromResult(new CreateGameResponse(gameId, PlayerToken, null, GameStatus.AwaitingDeployment));
    }

    public Task<GameStateDto> GetGameAsync(Guid gameId, string playerToken, CancellationToken ct)
    {
        EnsureGame(gameId);
        return Task.FromResult(_game!);
    }

    private void EnsureGame(Guid gameId)
    {
        if (_game is null || _game.GameId != gameId)
        {
            throw new GameApiException(new ApiProblemDto(
                "about:blank", "Partie introuvable", 404,
                "Aucune partie active ne correspond à cet identifiant.",
                ErrorCodes.GameNotFound, null));
        }
    }

    public Task<JoinGameResponse> JoinGameAsync(JoinGameRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<OpenGameDto>> ListOpenGamesAsync(CancellationToken ct) => throw new NotImplementedException();
    public Task<GameStateDto> PlaceFleetAsync(Guid gameId, string playerToken, PlaceFleetRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<ShotResultDto> FireAsync(Guid gameId, string playerToken, FireRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<PowerResultDto> UsePowerAsync(Guid gameId, string playerToken, UsePowerRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<GameStateDto> ForfeitAsync(Guid gameId, string playerToken, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<PowerDefinitionDto>> GetPowerCatalogAsync(CancellationToken ct) => throw new NotImplementedException();
}
