using Naval.Shared.Contracts;

namespace Naval.Shared.Domain;

/// <summary>
/// Moteur de jeu pur. Aucune dépendance externe, testable seul.
/// </summary>
public static class GameEngine
{
    public static (Fleet? fleet, IReadOnlyList<PlacementError> errors) ValidateAndBuildFleet(
        IReadOnlyList<ShipPlacementDto> placements,
        FleetPreset preset,
        int gridWidth,
        int gridHeight,
        string ownerPrefix)
    {
        var errors = new List<PlacementError>();

        // Vérifie que tous les types requis sont présents
        foreach (var entry in preset.Ships)
        {
            int count = placements.Count(p => p.Type == entry.Type);
            if (count != entry.Count)
            {
                errors.Add(new PlacementError(
                    PlacementErrorKind.FleetIncomplete,
                    $"Le type {entry.Type} attend {entry.Count} navire(s), {count} fourni(s)."));
            }
        }

        // Vérifie les types non attendus
        var expectedTypes = preset.Ships.Select(e => e.Type).ToHashSet();
        foreach (var p in placements.Where(p => !expectedTypes.Contains(p.Type)))
            errors.Add(new PlacementError(PlacementErrorKind.FleetIncomplete,
                $"Le type {p.Type} n'est pas dans le preset {preset.Name}."));

        if (errors.Count > 0) return (null, errors);

        var ships = new List<Ship>();
        var occupied = new HashSet<(int, int)>();
        var counters = new Dictionary<ShipType, int>();

        foreach (var p in placements)
        {
            var entry = preset.Ships.First(e => e.Type == p.Type);
            counters.TryGetValue(p.Type, out int idx);
            var shipId = $"{ownerPrefix}-{p.Type.ToString().ToLower()}-{idx + 1}";
            counters[p.Type] = idx + 1;

            var ship = new Ship(shipId, p.Type, entry.Size, new Coordinate(p.Origin.X, p.Origin.Y), p.Orientation);

            foreach (var cell in ship.Cells)
            {
                if (!cell.IsWithinBounds(gridWidth, gridHeight))
                {
                    errors.Add(new PlacementError(PlacementErrorKind.OutOfBounds,
                        $"Le navire {p.Type} dépasse la grille en ({cell.X},{cell.Y})."));
                    goto nextShip;
                }

                if (!occupied.Add((cell.X, cell.Y)))
                {
                    errors.Add(new PlacementError(PlacementErrorKind.OverlappingShips,
                        $"Chevauchement en ({cell.X},{cell.Y})."));
                    goto nextShip;
                }
            }

            ships.Add(ship);
            nextShip:;
        }

        if (errors.Count > 0) return (null, errors);
        return (new Fleet(ships), errors);
    }

    public static IReadOnlyList<ShipPlacementDto> GenerateRandomPlacement(
        FleetPreset preset, int gridWidth, int gridHeight, Random rng)
    {
        var placements = new List<ShipPlacementDto>();
        var occupied = new HashSet<(int, int)>();

        foreach (var entry in preset.Ships.OrderByDescending(e => e.Size))
        {
            for (int shipIdx = 0; shipIdx < entry.Count; shipIdx++)
            {
                ShipPlacementDto? placed = null;
                int attempts = 0;

                while (placed is null && attempts < 1000)
                {
                    attempts++;
                    var orientation = rng.Next(2) == 0 ? Orientation.Horizontal : Orientation.Vertical;
                    int maxX = orientation == Orientation.Horizontal ? gridWidth - entry.Size : gridWidth - 1;
                    int maxY = orientation == Orientation.Vertical ? gridHeight - entry.Size : gridHeight - 1;

                    if (maxX < 0 || maxY < 0) continue;

                    int x = rng.Next(maxX + 1);
                    int y = rng.Next(maxY + 1);

                    var cells = new List<(int, int)>();
                    for (int i = 0; i < entry.Size; i++)
                        cells.Add(orientation == Orientation.Horizontal ? (x + i, y) : (x, y + i));

                    if (cells.Any(c => occupied.Contains(c))) continue;

                    foreach (var c in cells) occupied.Add(c);
                    placed = new ShipPlacementDto(entry.Type, new CoordinateDto(x, y), orientation);
                }

                if (placed is not null) placements.Add(placed);
            }
        }

        return placements;
    }

    public static (ShotResult result, string? errorCode) ExecuteShot(
        PlayerState shooter, PlayerState target, Coordinate coord)
    {
        if (!coord.IsWithinBounds(shooter.OutgoingBoard.Width, shooter.OutgoingBoard.Height))
            return (new ShotResult(ShotOutcome.Rejected, null, 0), ErrorCodes.OutOfBounds);

        if (shooter.OutgoingBoard.HasBeenShot(coord))
            return (new ShotResult(ShotOutcome.Rejected, null, 0), ErrorCodes.CellAlreadyTargeted);

        shooter.OutgoingBoard.MarkShot(coord);
        target.IncomingBoard.MarkShot(coord);

        var ship = target.Fleet!.FindByCell(coord);
        if (ship is null)
        {
            shooter.ShotsFired++;
            return (new ShotResult(ShotOutcome.Miss, null, 0), null);
        }

        ship.TryHit(coord);
        shooter.ShotsFired++;
        shooter.Hits++;

        if (ship.IsSunk)
        {
            shooter.Energy += 3;
            return (new ShotResult(ShotOutcome.Sunk, ship, 3), null);
        }

        shooter.Energy += 2;
        return (new ShotResult(ShotOutcome.Hit, null, 2), null);
    }

    public static bool IsGameOver(Game game) =>
        game.Player1.Fleet?.AllSunk == true || game.Player2.Fleet?.AllSunk == true;

    public static PlayerState? FindWinner(Game game)
    {
        if (game.Player1.Fleet?.AllSunk == true) return game.Player2;
        if (game.Player2.Fleet?.AllSunk == true) return game.Player1;
        return null;
    }
}
