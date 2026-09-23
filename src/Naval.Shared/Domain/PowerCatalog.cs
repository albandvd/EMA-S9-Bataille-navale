using Naval.Shared.Contracts;

namespace Naval.Shared.Domain;

/// <summary>Catalogue statique des pouvoirs. Seule source de vérité pour les définitions.</summary>
public static class PowerCatalog
{
    public static readonly IReadOnlyList<PowerDefinitionDto> All =
    [
        // ── Reconnaissance ──
        new(PowerId.Sonar, "Sonar", PowerCategory.Recon,
            "Révèle le nombre de cases occupées dans un rayon de 4 cases autour d'une cible.",
            EnergyCost: 3, ChargeTurns: 0, Cooldown: 3, MaxUses: -1,
            TargetKind: TargetKind.Area, Radius: 4, RequiresCarrier: false, IconName: "sonar"),

        new(PowerId.TacticalRadar, "Radar tactique", PowerCategory.Recon,
            "Révèle le contenu de toutes les cases d'une ligne ou d'une colonne.",
            EnergyCost: 4, ChargeTurns: 0, Cooldown: 4, MaxUses: 3,
            TargetKind: TargetKind.Line, Radius: null, RequiresCarrier: false, IconName: "radar"),

        new(PowerId.LineDrone, "Drone de ligne", PowerCategory.Recon,
            "Révèle exactement les cases occupées ou vides d'une ligne entière.",
            EnergyCost: 3, ChargeTurns: 0, Cooldown: 3, MaxUses: -1,
            TargetKind: TargetKind.Line, Radius: null, RequiresCarrier: false, IconName: "drone"),

        new(PowerId.RadioIntercept, "Interception radio", PowerCategory.Recon,
            "Révèle si l'adversaire a un navire dans la moitié nord ou sud de la grille.",
            EnergyCost: 2, ChargeTurns: 0, Cooldown: 2, MaxUses: -1,
            TargetKind: TargetKind.None, Radius: null, RequiresCarrier: false, IconName: "radio"),

        new(PowerId.Satellite, "Satellite", PowerCategory.Recon,
            "Révèle les cases d'un carré 3×3 centré sur la cible.",
            EnergyCost: 5, ChargeTurns: 0, Cooldown: 5, MaxUses: 2,
            TargetKind: TargetKind.Area, Radius: 1, RequiresCarrier: false, IconName: "satellite"),

        new(PowerId.WakeAnalysis, "Analyse de sillage", PowerCategory.Recon,
            "Indique combien de navires adverses sont encore intacts.",
            EnergyCost: 1, ChargeTurns: 0, Cooldown: 2, MaxUses: -1,
            TargetKind: TargetKind.None, Radius: null, RequiresCarrier: false, IconName: "wake"),

        // ── Offensif ──
        new(PowerId.TripleSalvo, "Triple salve", PowerCategory.Offense,
            "Tire sur 3 cases adjacentes horizontalement.",
            EnergyCost: 5, ChargeTurns: 0, Cooldown: 4, MaxUses: -1,
            TargetKind: TargetKind.Cell, Radius: null, RequiresCarrier: false, IconName: "triple"),

        new(PowerId.CrossStrike, "Croix de feu", PowerCategory.Offense,
            "Tire sur la cible et les 4 cases cardinales adjacentes.",
            EnergyCost: 6, ChargeTurns: 0, Cooldown: 5, MaxUses: -1,
            TargetKind: TargetKind.Cell, Radius: null, RequiresCarrier: false, IconName: "cross"),

        new(PowerId.CarpetBombing, "Bombardement de zone", PowerCategory.Offense,
            "Tire sur toutes les cases d'une ligne entière.",
            EnergyCost: 8, ChargeTurns: 1, Cooldown: 6, MaxUses: 2,
            TargetKind: TargetKind.Line, Radius: null, RequiresCarrier: false, IconName: "carpet"),

        new(PowerId.Torpedo, "Torpille", PowerCategory.Offense,
            "Parcourt une colonne entière et touche le premier navire rencontré.",
            EnergyCost: 5, ChargeTurns: 0, Cooldown: 4, MaxUses: -1,
            TargetKind: TargetKind.Line, Radius: null, RequiresCarrier: false, IconName: "torpedo"),

        new(PowerId.OrbitalStrike, "Frappe orbitale", PowerCategory.Offense,
            "Après 10 tours de charge, détruit complètement un navire adverse aléatoire.",
            EnergyCost: 8, ChargeTurns: 10, Cooldown: 0, MaxUses: 1,
            TargetKind: TargetKind.None, Radius: null, RequiresCarrier: true, IconName: "orbital"),

        new(PowerId.PiercingMissile, "Missile perforant", PowerCategory.Offense,
            "Ignore le bouclier adverse et touche la case ciblée.",
            EnergyCost: 6, ChargeTurns: 0, Cooldown: 4, MaxUses: -1,
            TargetKind: TargetKind.Cell, Radius: null, RequiresCarrier: false, IconName: "piercing"),

        new(PowerId.HeavyBomb, "Bombe lourde", PowerCategory.Offense,
            "Pilonne un carré 3×3 centré sur la cible. Remplace le tir du tour ; ses touches ne rapportent pas d'énergie.",
            EnergyCost: 10, ChargeTurns: 0, Cooldown: 11, MaxUses: -1,
            TargetKind: TargetKind.Area, Radius: 1, RequiresCarrier: false, IconName: "bomb"),

        new(PowerId.NavalMine, "Mine navale", PowerCategory.Offense,
            "Pose une mine sur votre grille ; explose si un navire adverse passe dessus (mode sans IA uniquement).",
            EnergyCost: 3, ChargeTurns: 0, Cooldown: 3, MaxUses: 3,
            TargetKind: TargetKind.Cell, Radius: null, RequiresCarrier: false, IconName: "mine"),

        // ── Défensif ──
        new(PowerId.Decoy, "Leurre", PowerCategory.Defense,
            "Place un leurre sur votre grille qui encaisse un tir à la place d'un navire.",
            EnergyCost: 3, ChargeTurns: 0, Cooldown: 4, MaxUses: 2,
            TargetKind: TargetKind.Cell, Radius: null, RequiresCarrier: false, IconName: "decoy"),

        new(PowerId.Shield, "Bouclier", PowerCategory.Defense,
            "Protège un navire de votre flotte pour 1 tir.",
            EnergyCost: 4, ChargeTurns: 0, Cooldown: 5, MaxUses: -1,
            TargetKind: TargetKind.OwnShip, Radius: null, RequiresCarrier: false, IconName: "shield"),

        new(PowerId.EmergencyRepair, "Réparation d'urgence", PowerCategory.Defense,
            "Répare 1 case touchée sur un navire de votre flotte.",
            EnergyCost: 5, ChargeTurns: 0, Cooldown: 6, MaxUses: 2,
            TargetKind: TargetKind.OwnShip, Radius: null, RequiresCarrier: false, IconName: "repair"),

        new(PowerId.EvasiveManeuver, "Manœuvre évasive", PowerCategory.Defense,
            "Déplace un de vos navires d'une case.",
            EnergyCost: 6, ChargeTurns: 0, Cooldown: 7, MaxUses: 1,
            TargetKind: TargetKind.OwnShip, Radius: null, RequiresCarrier: false, IconName: "evasive"),

        new(PowerId.Jamming, "Brouillage", PowerCategory.Defense,
            "Empêche l'adversaire d'utiliser des pouvoirs pendant 2 tours.",
            EnergyCost: 5, ChargeTurns: 0, Cooldown: 8, MaxUses: 2,
            TargetKind: TargetKind.None, Radius: null, RequiresCarrier: false, IconName: "jamming"),

        new(PowerId.SmokeScreen, "Écran de fumée", PowerCategory.Defense,
            "Masque une zone 3×3 de votre grille pendant 2 tours.",
            EnergyCost: 3, ChargeTurns: 0, Cooldown: 5, MaxUses: -1,
            TargetKind: TargetKind.Area, Radius: 1, RequiresCarrier: false, IconName: "smoke"),

        // ── Utilitaire ──
        new(PowerId.DoubleTurn, "Double tour", PowerCategory.Utility,
            "Vous permet de tirer deux fois ce tour.",
            EnergyCost: 6, ChargeTurns: 0, Cooldown: 6, MaxUses: 2,
            TargetKind: TargetKind.None, Radius: null, RequiresCarrier: false, IconName: "double"),

        new(PowerId.Sabotage, "Sabotage", PowerCategory.Utility,
            "Annule la charge en cours de l'adversaire.",
            EnergyCost: 5, ChargeTurns: 0, Cooldown: 7, MaxUses: 2,
            TargetKind: TargetKind.None, Radius: null, RequiresCarrier: false, IconName: "sabotage"),

        new(PowerId.Espionage, "Espionnage", PowerCategory.Utility,
            "Révèle quel pouvoir est en charge chez l'adversaire.",
            EnergyCost: 3, ChargeTurns: 0, Cooldown: 4, MaxUses: -1,
            TargetKind: TargetKind.None, Radius: null, RequiresCarrier: false, IconName: "espionage"),

        new(PowerId.ReactorOverload, "Surcharge du réacteur", PowerCategory.Utility,
            "Gagne 5 points d'énergie immédiatement, mais le prochain tour est sauté.",
            EnergyCost: 0, ChargeTurns: 0, Cooldown: 8, MaxUses: 2,
            TargetKind: TargetKind.None, Radius: null, RequiresCarrier: false, IconName: "reactor"),
    ];

    public static PowerDefinitionDto? Find(PowerId id) => All.FirstOrDefault(p => p.Id == id);
}

public static class PowerPresets
{
    public static readonly IReadOnlyList<PowerPresetDto> All =
    [
        new("Reconnaissance", "Trois pouvoirs axés sur la détection de l'adversaire.",
            [PowerId.Sonar, PowerId.TacticalRadar, PowerId.Satellite]),

        new("Assaut", "Trois pouvoirs offensifs pour détruire rapidement.",
            [PowerId.TripleSalvo, PowerId.CrossStrike, PowerId.Torpedo]),

        new("Forteresse", "Protégez votre flotte et réparez les dégâts.",
            [PowerId.Shield, PowerId.EmergencyRepair, PowerId.SmokeScreen]),

        new("Équilibré", "Un pouvoir de chaque catégorie.",
            [PowerId.Sonar, PowerId.TripleSalvo, PowerId.Shield]),
    ];
}
