namespace Naval.Shared.Contracts;

// Toutes ces énumérations sont sérialisées en chaînes (JsonStringEnumConverter configuré
// dans Program.cs et dans le client). Sérialisées en nombres, elles rendent les réponses
// illisibles dans api.http et fragiles au réordonnancement.

public enum GameMode
{
    /// Un humain contre l'IA du serveur.
    SinglePlayer,
    /// Deux humains, partie privée rejointe par code.
    PrivateOnline,
    /// Deux humains, partie publique listée dans le salon.
    PublicOnline
}

public enum GameStatus
{
    /// Partie créée, en attente du second joueur (modes en ligne uniquement).
    AwaitingOpponent,
    /// Les deux joueurs placent leur flotte.
    AwaitingDeployment,
    /// Bataille en cours.
    InProgress,
    /// Terminée par la destruction d'une flotte.
    Finished,
    /// Terminée par abandon ou déconnexion prolongée.
    Abandoned
}

public enum AiLevel
{
    /// Tir uniforme sur une case non jouée.
    Random,
    /// Chasse en damier, puis traque des cases adjacentes après une touche.
    HuntTarget,
    /// Carte de densité des placements encore possibles.
    Probability
}

public enum Orientation { Horizontal, Vertical }

public enum ShipType
{
    Carrier,     // 5
    Battleship,  // 4
    Cruiser,     // 3
    Submarine,   // 3
    Destroyer    // 2
}

public enum ShotOutcome
{
    Miss,
    Hit,
    /// Touche qui achève le navire.
    Sunk,
    /// Le coup n'a pas été appliqué ; voir le code d'erreur.
    Rejected
}

public enum PlayerSlot { One, Two }

public enum PowerCategory { Recon, Offense, Defense, Utility }

public enum PowerId
{
    // Reconnaissance
    Sonar,
    TacticalRadar,
    LineDrone,
    RadioIntercept,
    Satellite,
    WakeAnalysis,
    // Offensif
    TripleSalvo,
    CrossStrike,
    CarpetBombing,
    Torpedo,
    OrbitalStrike,
    PiercingMissile,
    NavalMine,
    // Défensif
    Decoy,
    Shield,
    EmergencyRepair,
    EvasiveManeuver,
    Jamming,
    SmokeScreen,
    // Utilitaire
    DoubleTurn,
    Sabotage,
    Espionage,
    ReactorOverload
}

public enum PowerSlotStatus
{
    /// Utilisable si l'énergie et le cooldown le permettent.
    Ready,
    /// En cours de chargement : le joueur ne tire pas.
    Charging,
    /// Chargé, se déclenche au prochain tour.
    Armed,
    /// Utilisé récemment.
    OnCooldown,
    /// Charges épuisées pour cette partie.
    Exhausted
}

public enum TargetKind
{
    /// Aucune cible (effet global ou sur soi).
    None,
    /// Une case.
    Cell,
    /// Un navire de sa propre flotte.
    OwnShip,
    /// Une ligne ou une colonne entière.
    Line,
    /// Une case d'origine, l'effet s'étend selon le rayon du pouvoir.
    Area
}

/// Raison d'annulation d'une charge en cours.
public enum PowerAbortReason
{
    /// Le navire porteur a été coulé.
    CarrierSunk,
    /// Un Sabotage adverse a interrompu la charge.
    Sabotaged,
    /// Le joueur a annulé lui-même (l'énergie n'est pas remboursée).
    Cancelled
}

public enum EmoteCode
{
    GoodLuck, WellPlayed, Oops, Thinking, Taunt, Sorry, Gg, Hurry
}
