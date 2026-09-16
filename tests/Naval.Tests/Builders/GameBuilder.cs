using Naval.Shared.Contracts;
using Naval.Shared.Domain;

namespace Naval.Tests.Builders;

public sealed class GameBuilder
{
    private int _gridWidth = 10;
    private int _gridHeight = 10;
    private string _fleetPreset = "Classic";
    private Fleet? _fleet1;
    private Fleet? _fleet2;
    private bool _inProgress;

    public static GameBuilder New() => new();

    public GameBuilder WithGrid(int width, int height)
    {
        _gridWidth = width;
        _gridHeight = height;
        return this;
    }

    public GameBuilder WithFleetPreset(string preset)
    {
        _fleetPreset = preset;
        return this;
    }

    public GameBuilder WithFleet(PlayerSlot slot, Fleet fleet)
    {
        if (slot == PlayerSlot.One) _fleet1 = fleet;
        else _fleet2 = fleet;
        return this;
    }

    public GameBuilder ReadyToFight()
    {
        _inProgress = true;

        var preset = FleetPresets.Find(_fleetPreset) ?? FleetPresets.Classic;
        var rng1 = new Random(1);
        var rng2 = new Random(2);

        _fleet1 ??= FleetBuilder.Classic().Build(preset, _gridWidth, _gridHeight);
        _fleet2 ??= new FleetBuilder().WithPrefix("ai").Build(preset, _gridWidth, _gridHeight);

        return this;
    }

    public Game Build()
    {
        var gameId = GameId.New();
        var p1Id = PlayerId.New();
        var p2Id = PlayerId.New();

        var p1 = new PlayerState(p1Id, "Joueur 1", PlayerSlot.One, "token-p1", _gridWidth, _gridHeight);
        var p2 = new PlayerState(p2Id, "IA", PlayerSlot.Two, "token-p2", _gridWidth, _gridHeight, isAi: true);

        var game = new Game(gameId, GameMode.SinglePlayer, null, _fleetPreset,
            _gridWidth, _gridHeight, false, 0, p1, p2);

        if (_fleet1 is not null)
        {
            p1.Fleet = _fleet1;
            p1.IsReady = true;
        }

        if (_fleet2 is not null)
        {
            p2.Fleet = _fleet2;
            p2.IsReady = true;
        }

        if (_inProgress)
        {
            game.Status = GameStatus.InProgress;
            game.TurnNumber = 1;
            game.CurrentPlayerId = p1Id;
        }

        return game;
    }
}
