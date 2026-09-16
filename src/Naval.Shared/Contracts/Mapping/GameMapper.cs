using Naval.Shared.Domain;
using Naval.Shared.Domain.Events;

namespace Naval.Shared.Contracts.Mapping;

public static class GameMapper
{
    public static GameStateDto ToGameStateDto(Game game, PlayerState viewer)
    {
        var opponent = game.GetOpponent(viewer.Id);
        var recentEvents = game.Events.TakeLast(20).Select(ToEventDto).ToList();

        return new GameStateDto(
            GameId: game.Id.Value,
            Mode: game.Mode,
            Status: game.Status,
            JoinCode: game.JoinCode,
            TurnNumber: game.TurnNumber,
            CurrentPlayerId: game.CurrentPlayerId?.Value,
            TurnDeadlineUtc: game.TurnDeadlineUtc,
            WinnerId: game.WinnerId,
            Self: ToSelfViewDto(viewer),
            Opponent: ToOpponentViewDto(opponent, viewer),
            RecentEvents: recentEvents,
            EventCursor: game.Events.Count > 0 ? game.Events[^1].Sequence : 0);
    }

    public static SelfViewDto ToSelfViewDto(PlayerState player)
    {
        var board = player.Fleet is not null
            ? player.IncomingBoard.BuildOwnView(player.Fleet)
            : BuildEmptyView(player.IncomingBoard.Width, player.IncomingBoard.Height);

        return new SelfViewDto(
            PlayerId: player.Id.Value,
            Name: player.Name,
            Slot: player.Slot,
            Energy: player.Energy,
            Board: ToBoardViewDto(board, player.IncomingBoard.Width, player.IncomingBoard.Height),
            Fleet: player.Fleet?.Ships.Select(ToShipStateDtoOwn).ToList() ?? [],
            Powers: []);
    }

    public static OpponentViewDto ToOpponentViewDto(PlayerState opponent, PlayerState viewer)
    {
        var targetBoard = opponent.Fleet is not null
            ? viewer.OutgoingBoard.BuildTargetView(opponent.Fleet)
            : BuildEmptyView(viewer.OutgoingBoard.Width, viewer.OutgoingBoard.Height);

        var sunkShips = opponent.Fleet?.Ships
            .Where(s => s.IsSunk)
            .Select(ToShipStateDtoSunk)
            .ToList() ?? [];

        return new OpponentViewDto(
            PlayerId: opponent.Id.Value,
            Name: opponent.Name,
            IsConnected: opponent.IsConnected,
            IsAi: opponent.IsAi,
            Energy: opponent.Energy,
            ShipsRemaining: opponent.Fleet?.Ships.Count(s => !s.IsSunk) ?? 0,
            ShipsTotal: opponent.Fleet?.Ships.Count ?? 0,
            TargetBoard: ToBoardViewDto(targetBoard, viewer.OutgoingBoard.Width, viewer.OutgoingBoard.Height),
            SunkShips: sunkShips,
            Charge: new OpponentChargeDto(false, null, null, null),
            EquippedPowers: []);
    }

    public static ShotResultDto ToShotResultDto(
        Game game, PlayerState shooter, Coordinate target, Domain.ShotResult result, Guid? nextPlayerId)
    {
        return new ShotResultDto(
            GameId: game.Id.Value,
            TurnNumber: game.TurnNumber,
            ShooterId: shooter.Id.Value,
            Target: new CoordinateDto(target.X, target.Y),
            Outcome: result.Outcome,
            SunkShip: result.SunkShip is not null ? ToShipStateDtoSunk(result.SunkShip) : null,
            EnergyGained: result.EnergyGained,
            NextPlayerId: nextPlayerId,
            GameOver: game.Status == GameStatus.Finished || game.Status == GameStatus.Abandoned,
            WinnerId: game.WinnerId);
    }

    public static GameOverDto ToGameOverDto(Game game, string reason)
    {
        return new GameOverDto(
            GameId: game.Id.Value,
            WinnerId: game.WinnerId ?? Guid.Empty,
            WinnerName: game.WinnerId.HasValue
                ? (game.GetPlayerById(new PlayerId(game.WinnerId.Value))?.Name ?? "")
                : "",
            Reason: reason,
            TotalTurns: game.TurnNumber,
            Stats:
            [
                ToPlayerStatsDto(game.Player1),
                ToPlayerStatsDto(game.Player2),
            ]);
    }

    public static GameEventDto ToEventDto(GameEvent evt)
    {
        var type = evt switch
        {
            ShotFiredEvent   => "ShotFired",
            TurnChangedEvent => "TurnChanged",
            GameOverEvent    => "GameOver",
            PlayerReadyEvent => "PlayerJoined",
            _ => "Unknown"
        };

        return new GameEventDto(
            Sequence: evt.Sequence,
            AtUtc: evt.AtUtc,
            Type: type,
            ActorId: evt.ActorId?.Value,
            Message: evt.Message,
            Data: null);
    }

    public static OpenGameDto ToOpenGameDto(GameSummary s) => new(
        GameId: s.GameId,
        HostName: s.HostName,
        GridWidth: s.GridWidth,
        GridHeight: s.GridHeight,
        FleetPreset: s.FleetPreset,
        PowersEnabled: s.PowersEnabled,
        TurnTimeoutSeconds: s.TurnTimeoutSeconds,
        CreatedAtUtc: s.CreatedAtUtc);

    // ─── Helpers privés ───

    private static BoardViewDto ToBoardViewDto(char[,] grid, int width, int height)
    {
        var rows = new List<string>(height);
        for (int y = 0; y < height; y++)
        {
            var sb = new System.Text.StringBuilder(width);
            for (int x = 0; x < width; x++)
                sb.Append(grid[x, y]);
            rows.Add(sb.ToString());
        }
        return new BoardViewDto(width, height, rows);
    }

    private static char[,] BuildEmptyView(int width, int height)
    {
        var view = new char[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                view[x, y] = '.';
        return view;
    }

    private static ShipStateDto ToShipStateDtoOwn(Ship ship) => new(
        Id: ship.Id,
        Type: ship.Type,
        Size: ship.Size,
        Hits: ship.HitCount,
        IsSunk: ship.IsSunk,
        Cells: ship.Cells.Select(c => new CoordinateDto(c.X, c.Y)).ToList());

    private static ShipStateDto ToShipStateDtoSunk(Ship ship) => new(
        Id: ship.Id,
        Type: ship.Type,
        Size: ship.Size,
        Hits: ship.HitCount,
        IsSunk: ship.IsSunk,
        Cells: ship.Cells.Select(c => new CoordinateDto(c.X, c.Y)).ToList());

    private static PlayerStatsDto ToPlayerStatsDto(PlayerState p) => new(
        PlayerId: p.Id.Value,
        Name: p.Name,
        ShotsFired: p.ShotsFired,
        Hits: p.Hits,
        Accuracy: p.ShotsFired > 0 ? Math.Round((double)p.Hits / p.ShotsFired, 2) : 0,
        PowersUsed: p.PowersUsed,
        EnergySpent: p.EnergySpent);
}
