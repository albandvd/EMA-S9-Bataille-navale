using Naval.Shared.Contracts;

namespace Naval.Shared.Domain;

public sealed class PlayerState
{
    public PlayerId Id { get; }
    public string Name { get; set; }
    public PlayerSlot Slot { get; }
    public string Token { get; set; }
    public bool IsAi { get; }
    public bool IsConnected { get; set; }
    public bool IsReady { get; set; }
    public int Energy { get; set; }

    /// <summary>Flotte propre, positionnée sur la grille propre.</summary>
    public Fleet? Fleet { get; set; }

    /// <summary>Grille qui suit les coups ENTRANTS sur la flotte propre.</summary>
    public Board IncomingBoard { get; }

    /// <summary>Grille qui suit les coups que CE joueur a tirés sur l'adversaire.</summary>
    public Board OutgoingBoard { get; }

    // Statistiques fin de partie
    public int ShotsFired { get; set; }
    public int Hits { get; set; }
    public int PowersUsed { get; set; }
    public int EnergySpent { get; set; }

    public IReadOnlyList<PowerId> EquippedPowers { get; }
    public List<Powers.PowerSlot> PowerSlots { get; }

    public PlayerState(PlayerId id, string name, PlayerSlot slot, string token, int gridWidth, int gridHeight,
        bool isAi = false, IReadOnlyList<PowerId>? equippedPowers = null)
    {
        Id = id;
        Name = name;
        Slot = slot;
        Token = token;
        IsAi = isAi;
        IsConnected = !isAi;
        IncomingBoard = new Board(gridWidth, gridHeight);
        OutgoingBoard = new Board(gridWidth, gridHeight);

        EquippedPowers = equippedPowers ?? [];
        PowerSlots = EquippedPowers
            .Select(powerId => new Powers.PowerSlot(
                powerId,
                PowerCatalog.All.First(d => d.Id == powerId).MaxUses))
            .ToList();
    }
}
