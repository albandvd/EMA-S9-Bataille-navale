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

    private static readonly IReadOnlyList<PowerDefinitionDto> Catalog =
    [
        new(PowerId.Sonar, "Sonar", PowerCategory.Recon,
            "Renvoie le nombre de cases occupées dans un disque de rayon 4.",
            3, 2, 3, -1, TargetKind.Cell, 4, false, "sonar"),
        new(PowerId.TripleSalvo, "Salve triple", PowerCategory.Offense,
            "3 tirs consécutifs alignés.",
            4, 0, 3, -1, TargetKind.Line, null, false, "triple-salvo")
    ];

    private int _shotCounter;

    public Task<JoinGameResponse> JoinGameAsync(JoinGameRequest request, CancellationToken ct) =>
        throw new GameApiException(new ApiProblemDto(
            "about:blank", "Non disponible", 501,
            "Le multijoueur n'est pas encore simulé par le client de test.",
            ErrorCodes.GameNotFound, null));

    public Task<IReadOnlyList<OpenGameDto>> ListOpenGamesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OpenGameDto>>([]);

    public Task<GameStateDto> PlaceFleetAsync(Guid gameId, string playerToken, PlaceFleetRequest request, CancellationToken ct)
    {
        EnsureGame(gameId);

        var fleet = request.Ships
            .Select((ship, i) => new ShipStateDto(
                $"ship-{i}", ship.Type, ShipSize(ship.Type), 0, false,
                Cells(ship.Origin, ship.Orientation, ShipSize(ship.Type))))
            .ToList();

        var self = _game!.Self with { Fleet = fleet };
        _game = _game with
        {
            Status = GameStatus.InProgress,
            CurrentPlayerId = self.PlayerId,
            TurnNumber = 1,
            Self = self
        };

        return Task.FromResult(_game);
    }

    public Task<ShotResultDto> FireAsync(Guid gameId, string playerToken, FireRequest request, CancellationToken ct)
    {
        EnsureGame(gameId);

        _shotCounter++;
        var outcome = _shotCounter % 3 == 0 ? ShotOutcome.Hit : ShotOutcome.Miss;
        var symbol = outcome == ShotOutcome.Hit ? 'x' : 'o';

        var rows = _game!.Opponent.TargetBoard.Rows.ToList();
        var chars = rows[request.Target.Y].ToCharArray();
        chars[request.Target.X] = symbol;
        rows[request.Target.Y] = new string(chars);

        var opponent = _game.Opponent with { TargetBoard = _game.Opponent.TargetBoard with { Rows = rows } };
        _game = _game with { Opponent = opponent, TurnNumber = _game.TurnNumber + 1 };

        return Task.FromResult(new ShotResultDto(
            gameId, _game.TurnNumber, _game.Self.PlayerId, request.Target, outcome,
            null, outcome == ShotOutcome.Hit ? 2 : 0, _game.Self.PlayerId, false, null));
    }

    public Task<PowerResultDto> UsePowerAsync(Guid gameId, string playerToken, UsePowerRequest request, CancellationToken ct)
    {
        EnsureGame(gameId);

        return Task.FromResult(new PowerResultDto(
            gameId, _game!.TurnNumber, request.PowerId, false, 0, 3, _game.Self.Energy,
            [], 2, [], "Sonar : 2 cases occupées détectées.", _game.Self.PlayerId, false, null));
    }

    public Task<GameStateDto> ForfeitAsync(Guid gameId, string playerToken, CancellationToken ct)
    {
        EnsureGame(gameId);
        _game = _game! with { Status = GameStatus.Abandoned, WinnerId = _game.Opponent.PlayerId };
        return Task.FromResult(_game);
    }

    public Task<IReadOnlyList<PowerDefinitionDto>> GetPowerCatalogAsync(CancellationToken ct) =>
        Task.FromResult(Catalog);

    private static int ShipSize(ShipType type) => type switch
    {
        ShipType.Carrier => 5,
        ShipType.Battleship => 4,
        ShipType.Cruiser => 3,
        ShipType.Submarine => 3,
        ShipType.Destroyer => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static List<CoordinateDto> Cells(CoordinateDto origin, Orientation orientation, int size) =>
        Enumerable.Range(0, size)
            .Select(i => orientation == Orientation.Horizontal
                ? origin with { X = origin.X + i }
                : origin with { Y = origin.Y + i })
            .ToList();
}
