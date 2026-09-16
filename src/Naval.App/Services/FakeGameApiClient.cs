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
        _shotCounter = 0;
        _hitCounter = 0;

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
    private int _hitCounter;

    /// <summary>
    /// Nombre de touches (déterministes, 1 tir sur 3) déclenchant une victoire simulée. Simple
    /// seuil arbitraire pour permettre d'atteindre l'écran /result en développement ; ne reflète
    /// aucune règle de jeu réelle (pas de coulage de navires, pas de flotte adverse).
    /// </summary>
    private const int HitsToWin = 3;

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

        var self = _game!.Self with { Fleet = fleet, Board = PaintFleet(_game.Self.Board, fleet) };
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

        if (_game!.Status != GameStatus.InProgress)
        {
            throw new GameApiException(new ApiProblemDto(
                "about:blank", "Partie terminée", 409,
                "Cette partie est déjà terminée, aucun tir ne peut plus être joué.",
                ErrorCodes.GameNotInProgress, null));
        }

        _shotCounter++;
        var outcome = _shotCounter % 3 == 0 ? ShotOutcome.Hit : ShotOutcome.Miss;
        var symbol = outcome == ShotOutcome.Hit ? 'x' : 'o';

        var rows = _game!.Opponent.TargetBoard.Rows.ToList();
        var chars = rows[request.Target.Y].ToCharArray();
        chars[request.Target.X] = symbol;
        rows[request.Target.Y] = new string(chars);

        var opponent = _game.Opponent with { TargetBoard = _game.Opponent.TargetBoard with { Rows = rows } };

        if (outcome == ShotOutcome.Hit)
        {
            _hitCounter++;
        }

        var gameOver = _hitCounter >= HitsToWin;
        var status = gameOver ? GameStatus.Finished : _game.Status;
        var winnerId = gameOver ? _game.Self.PlayerId : _game.WinnerId;

        _game = _game with
        {
            Opponent = opponent,
            TurnNumber = _game.TurnNumber + 1,
            Status = status,
            WinnerId = winnerId
        };

        return Task.FromResult(new ShotResultDto(
            gameId, _game.TurnNumber, _game.Self.PlayerId, request.Target, outcome,
            null, outcome == ShotOutcome.Hit ? 2 : 0, gameOver ? null : _game.Self.PlayerId,
            gameOver, winnerId));
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

    private static readonly IReadOnlyDictionary<string, FleetPresetDto> FleetPresets =
        new Dictionary<string, FleetPresetDto>
        {
            ["Classic"] = new FleetPresetDto("Classic", "Flotte complète à cinq navires.",
                [
                    new FleetShipDto(ShipType.Carrier, 5, 1),
                    new FleetShipDto(ShipType.Battleship, 4, 1),
                    new FleetShipDto(ShipType.Cruiser, 3, 1),
                    new FleetShipDto(ShipType.Submarine, 3, 1),
                    new FleetShipDto(ShipType.Destroyer, 2, 1)
                ], 8),
            ["Skirmish"] = new FleetPresetDto("Skirmish", "Flotte réduite à trois navires, pour une partie rapide.",
                [
                    new FleetShipDto(ShipType.Cruiser, 3, 1),
                    new FleetShipDto(ShipType.Submarine, 3, 1),
                    new FleetShipDto(ShipType.Destroyer, 2, 1)
                ], 8)
        };

    public Task<FleetPresetDto> GetFleetPresetAsync(string presetName, CancellationToken ct)
    {
        if (!FleetPresets.TryGetValue(presetName, out var preset))
        {
            throw new GameApiException(new ApiProblemDto(
                "about:blank", "Preset introuvable", 404,
                $"Aucun preset de flotte nommé « {presetName} ».",
                ErrorCodes.GameNotFound, null));
        }

        return Task.FromResult(preset);
    }

    private static int ShipSize(ShipType type) => type switch
    {
        ShipType.Carrier => 5,
        ShipType.Battleship => 4,
        ShipType.Cruiser => 3,
        ShipType.Submarine => 3,
        ShipType.Destroyer => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    /// <summary>
    /// Peint les cases occupées par la flotte du joueur sur son propre plateau. Sans risque de
    /// fuite : il s'agit toujours de sa propre flotte, jamais de celle de l'adversaire.
    /// </summary>
    private static BoardViewDto PaintFleet(BoardViewDto board, IReadOnlyList<ShipStateDto> fleet)
    {
        var rows = board.Rows.Select(row => row.ToCharArray()).ToArray();

        foreach (var cell in fleet.SelectMany(ship => ship.Cells ?? []))
        {
            rows[cell.Y][cell.X] = 'S';
        }

        return board with { Rows = rows.Select(row => new string(row)).ToList() };
    }

    private static List<CoordinateDto> Cells(CoordinateDto origin, Orientation orientation, int size) =>
        Enumerable.Range(0, size)
            .Select(i => orientation == Orientation.Horizontal
                ? origin with { X = origin.X + i }
                : origin with { Y = origin.Y + i })
            .ToList();
}
