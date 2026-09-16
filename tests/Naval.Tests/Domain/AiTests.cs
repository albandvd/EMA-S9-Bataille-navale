using FluentAssertions;
using Naval.Shared.Contracts;
using Naval.Shared.Domain;
using Naval.Shared.Domain.Ai;
using Naval.Tests.Builders;

namespace Naval.Tests.Domain;

public sealed class AiTests
{
    // ── RandomAi ──

    [Fact]
    public void RandomAi_NeverChoosesAlreadyShotCell()
    {
        var (ai, opponent) = BuildPlayers();
        var strategy = new RandomAi(new Random(42));

        var chosen = new HashSet<(int, int)>();
        for (int i = 0; i < 20; i++)
        {
            var coord = strategy.ChooseTarget(ai, opponent);
            ai.OutgoingBoard.MarkShot(coord);
            chosen.Add((coord.X, coord.Y));
        }

        // Vérifie qu'aucun doublon
        chosen.Should().HaveCount(20);
    }

    [Fact]
    public void RandomAi_CoversAllCells_Eventually()
    {
        var (ai, opponent) = BuildPlayers();
        var strategy = new RandomAi(new Random(1));
        int total = 10 * 10;

        for (int i = 0; i < total; i++)
        {
            var coord = strategy.ChooseTarget(ai, opponent);
            ai.OutgoingBoard.MarkShot(coord);
        }

        int shot = 0;
        for (int x = 0; x < 10; x++)
            for (int y = 0; y < 10; y++)
                if (ai.OutgoingBoard.HasBeenShot(new Coordinate(x, y))) shot++;

        shot.Should().Be(total);
    }

    // ── HuntTargetAi ──

    [Fact]
    public void HuntTargetAi_AfterHit_TargetsAdjacent()
    {
        var (ai, opponent) = BuildPlayers();

        // Pose un hit manuel connu
        var hitCoord = new Coordinate(5, 5);
        ai.OutgoingBoard.MarkShot(hitCoord);
        // Le navire à (5,5) : on y met un Destroyer fictif
        var ship = opponent.Fleet!.FindByCell(hitCoord);
        // S'il n'y a rien, on place une flotte centrée là
        if (ship is null)
        {
            var fakeFleet = new Fleet([new Ship("ai-1", ShipType.Destroyer, 2, hitCoord, Orientation.Horizontal)]);
            opponent.Fleet = fakeFleet;
            ship = opponent.Fleet.FindByCell(hitCoord)!;
        }
        ship.TryHit(hitCoord);

        var strategy = new HuntTargetAi(new Random(0));
        var next = strategy.ChooseTarget(ai, opponent);

        Coordinate[] adjacents =
        [
            new(4, 5), new(6, 5), new(5, 4), new(5, 6),
        ];
        adjacents.Should().Contain(next);
    }

    [Fact]
    public void HuntTargetAi_AfterTwoAlignedHits_FollowsAxis()
    {
        var (ai, opponent) = BuildPlayers();

        var hit1 = new Coordinate(3, 3);
        var hit2 = new Coordinate(4, 3);

        // Cruiser taille 3 : (3,3),(4,3),(5,3) — pas encore coulé après 2 hits
        var ship = new Ship("ai-cruiser", ShipType.Cruiser, 3, hit1, Orientation.Horizontal);
        opponent.Fleet = new Fleet([ship]);
        ship.TryHit(hit1);
        ship.TryHit(hit2);

        ai.OutgoingBoard.MarkShot(hit1);
        ai.OutgoingBoard.MarkShot(hit2);

        var strategy = new HuntTargetAi(new Random(0));
        var next = strategy.ChooseTarget(ai, opponent);

        // Doit être en (2,3) ou (5,3) — continuation de l'axe horizontal
        new[] { new Coordinate(2, 3), new Coordinate(5, 3) }.Should().Contain(next);
    }

    [Fact]
    public void HuntTargetAi_WhenNoHits_UsesCheckerboard()
    {
        var (ai, opponent) = BuildPlayers();
        // Flotte vide pour garantir l'absence de hits et rester en mode chasse damier
        opponent.Fleet = new Fleet([]);

        var strategy = new HuntTargetAi(new Random(7));

        var results = new List<Coordinate>();
        for (int i = 0; i < 10; i++)
        {
            var coord = strategy.ChooseTarget(ai, opponent);
            ai.OutgoingBoard.MarkShot(coord);
            results.Add(coord);
        }

        // En mode chasse damier, (x+y) % 2 == 0
        results.Should().OnlyContain(c => (c.X + c.Y) % 2 == 0);
    }

    private static (PlayerState ai, PlayerState opponent) BuildPlayers()
    {
        var ai = new PlayerState(PlayerId.New(), "IA", PlayerSlot.Two, "ai-token", 10, 10, isAi: true);
        var human = new PlayerState(PlayerId.New(), "Humain", PlayerSlot.One, "h-token", 10, 10);
        human.Fleet = FleetBuilder.Classic().Build(FleetPresets.Classic);
        return (ai, human);
    }
}
