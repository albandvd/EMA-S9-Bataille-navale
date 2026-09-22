namespace Naval.Shared.Contracts;

// ═══════════════════════════════════════════════════════════════════════════
//  RÈGLE ABSOLUE
//  Ces types sont construits par le serveur POUR UN JOUEUR DONNÉ.
//  Aucun d'eux ne doit jamais contenir la position d'un navire adverse que ce
//  joueur n'a pas découverte. Avant d'ajouter un champ ici, demande-toi ce que
//  l'adversaire apprendrait en lisant la réponse dans les DevTools.
// ═══════════════════════════════════════════════════════════════════════════

// ─────────────────────────────── Création ───────────────────────────────

/// <param name="PlayerToken">
/// Identifie la session de ce joueur. À renvoyer dans l'en-tête X-Player-Token et dans la
/// query string SignalR. Ce n'est pas un secret cryptographique : c'est un identifiant de
/// session, à ne pas partager.
/// </param>
/// <param name="JoinCode">Code à 6 caractères pour une partie privée, null sinon.</param>
public sealed record CreateGameResponse(
    Guid GameId,
    string PlayerToken,
    string? JoinCode,
    GameStatus Status);

public sealed record JoinGameResponse(
    Guid GameId,
    string PlayerToken,
    GameStatus Status,
    string OpponentName);

/// <summary>Une partie en attente, telle qu'affichée dans le salon public.</summary>
public sealed record OpenGameDto(
    Guid GameId,
    string HostName,
    int GridWidth,
    int GridHeight,
    string FleetPreset,
    bool PowersEnabled,
    int TurnTimeoutSeconds,
    DateTimeOffset CreatedAtUtc);

// ─────────────────────────────── Grilles ───────────────────────────────

/// <summary>
/// Grille encodée en lignes de caractères, une chaîne par ligne, un caractère par case.
/// Choix assumé : 100 objets JSON pour une grille 10×10 pèsent dix fois plus qu'une
/// dizaine de chaînes, et l'encodage se lit à l'œil nu dans api.http — ce qui accélère
/// le débogage. La légende est ci-dessous et ne doit pas être dupliquée côté front.
/// </summary>
/// <remarks>
/// Légende commune :
///   '.'  inconnu
///   'o'  tir manqué
///   'x'  touché
///   '#'  coulé
///   '~'  révélé vide (reconnaissance)
///   '?'  révélé occupé, sans identification du navire
///   'f'  masqué par un écran de fumée
/// Grille propre uniquement :
///   'S'  segment de navire intact
///   'X'  segment de navire touché
///   'D'  leurre
///   'M'  mine posée
///   'B'  segment protégé par un bouclier actif
/// </remarks>
public sealed record BoardViewDto(
    int Width,
    int Height,
    IReadOnlyList<string> Rows);

/// <summary>
/// État d'un navire. <paramref name="Cells"/> n'est renseigné que pour SA PROPRE flotte,
/// ou pour un navire adverse déjà coulé. Il vaut null partout ailleurs.
/// </summary>
public sealed record ShipStateDto(
    string Id,
    ShipType Type,
    int Size,
    int Hits,
    bool IsSunk,
    IReadOnlyList<CoordinateDto>? Cells);

// ─────────────────────────────── Pouvoirs ───────────────────────────────

/// <summary>Définition statique d'un pouvoir. Servie par GET /api/catalog/powers.</summary>
/// <param name="MaxUses">-1 = illimité.</param>
/// <param name="RequiresCarrier">Vrai si ChargeTurns ≥ 3 : un navire porteur doit être désigné.</param>
public sealed record PowerDefinitionDto(
    PowerId Id,
    string Name,
    PowerCategory Category,
    string Description,
    int EnergyCost,
    int ChargeTurns,
    int Cooldown,
    int MaxUses,
    TargetKind TargetKind,
    int? Radius,
    bool RequiresCarrier,
    string IconName);

/// <summary>État d'un pouvoir équipé, pour un joueur donné.</summary>
public sealed record PowerSlotDto(
    PowerId PowerId,
    PowerSlotStatus Status,
    int ChargeRemaining,
    int CooldownRemaining,
    int UsesLeft,
    bool CanAffordNow);

/// <summary>
/// Ce qu'un joueur sait de la charge adverse. Le pouvoir chargé n'est identifié que si
/// l'adversaire a utilisé Espionnage (P-22) ou si la charge est publique (Frappe orbitale
/// au-delà du 5ᵉ tour).
/// </summary>
public sealed record OpponentChargeDto(
    bool IsCharging,
    PowerId? RevealedPowerId,
    int? TurnsRemaining,
    int? RevealedColumn);

// ─────────────────────────────── Joueurs ───────────────────────────────

public sealed record SelfViewDto(
    Guid PlayerId,
    string Name,
    PlayerSlot Slot,
    int Energy,
    BoardViewDto Board,
    IReadOnlyList<ShipStateDto> Fleet,
    IReadOnlyList<PowerSlotDto> Powers);

/// <summary>
/// Vue de l'adversaire. Ne contient ni sa grille propre, ni son énergie exacte au-delà de ce
/// qui est public, ni la position de ses navires non coulés.
/// </summary>
public sealed record OpponentViewDto(
    Guid PlayerId,
    string Name,
    bool IsConnected,
    bool IsAi,
    int Energy,
    int ShipsRemaining,
    int ShipsTotal,
    /// Grille adverse telle que CE joueur l'a découverte.
    BoardViewDto TargetBoard,
    /// Navires adverses déjà coulés, avec leurs cases.
    IReadOnlyList<ShipStateDto> SunkShips,
    OpponentChargeDto Charge,
    IReadOnlyList<PowerId> EquippedPowers);

// ─────────────────────────────── État de partie ───────────────────────────────

public sealed record GameStateDto(
    Guid GameId,
    GameMode Mode,
    GameStatus Status,
    string? JoinCode,
    int TurnNumber,
    Guid? CurrentPlayerId,
    DateTimeOffset? TurnDeadlineUtc,
    Guid? WinnerId,
    SelfViewDto Self,
    OpponentViewDto Opponent,
    IReadOnlyList<GameEventDto> RecentEvents,
    /// Numéro du dernier événement inclus. Sert à reprendre le flux après reconnexion.
    int EventCursor);

// ─────────────────────────────── Résultats d'action ───────────────────────────────

public sealed record ShotResultDto(
    Guid GameId,
    int TurnNumber,
    Guid ShooterId,
    CoordinateDto Target,
    ShotOutcome Outcome,
    /// Renseigné uniquement si Outcome == Sunk.
    ShipStateDto? SunkShip,
    int EnergyGained,
    Guid? NextPlayerId,
    bool GameOver,
    Guid? WinnerId);

/// <summary>
/// Résultat d'une activation de pouvoir.
/// Un pouvoir à charge renvoie Charging = true et une charge utile vide : l'effet arrivera
/// plus tard, par un événement PowerResolved.
/// </summary>
/// <param name="RevealedCells">
/// Cases dont le contenu est révélé. Vide pour un pouvoir qui ne révèle qu'un décompte :
/// un sonar qui remplit ce champ est un bug de conception, pas une optimisation.
/// </param>
/// <param name="RevealedCount">Décompte flou, pour les pouvoirs de type sonar ou drone.</param>
public sealed record PowerResultDto(
    Guid GameId,
    int TurnNumber,
    PowerId PowerId,
    bool Charging,
    int ChargeRemaining,
    int EnergySpent,
    int EnergyRemaining,
    IReadOnlyList<RevealedCellDto> RevealedCells,
    int? RevealedCount,
    IReadOnlyList<ShotResultDto> Shots,
    string Message,
    Guid? NextPlayerId,
    bool GameOver,
    Guid? WinnerId);

public sealed record RevealedCellDto(CoordinateDto Cell, bool Occupied);

public sealed record GameOverDto(
    Guid GameId,
    Guid WinnerId,
    string WinnerName,
    string Reason,          // "FleetDestroyed", "Forfeit", "Disconnected", "Timeout"
    int TotalTurns,
    IReadOnlyList<PlayerStatsDto> Stats);

public sealed record PlayerStatsDto(
    Guid PlayerId,
    string Name,
    int ShotsFired,
    int Hits,
    double Accuracy,
    int PowersUsed,
    int EnergySpent);

// ─────────────────────────────── Événements ───────────────────────────────

/// <summary>
/// Un événement de partie, dans une forme directement affichable. Le flux complet permet de
/// rejouer une partie (E-26) sans conserver d'états intermédiaires.
/// </summary>
/// <param name="Type">
/// ShotFired · ShipSunk · PowerActivated · PowerCharging · PowerAborted · PowerResolved ·
/// EnergyChanged · TurnChanged · PlayerJoined · PlayerLeft · EmoteSent · GameOver
/// </param>
/// <param name="Data">
/// Charge utile spécifique au type, déjà filtrée pour le destinataire.
/// </param>
public sealed record GameEventDto(
    int Sequence,
    DateTimeOffset AtUtc,
    string Type,
    Guid? ActorId,
    string Message,
    IReadOnlyDictionary<string, string>? Data);

// ─────────────────────────────── Catalogue ───────────────────────────────

public sealed record FleetPresetDto(
    string Name,
    string Description,
    IReadOnlyList<FleetShipDto> Ships,
    int MinGridSize);

public sealed record FleetShipDto(ShipType Type, int Size, int Count);

public sealed record PowerPresetDto(
    string Name,
    string Description,
    IReadOnlyList<PowerId> Powers);

// ─────────────────────────────── Erreurs ───────────────────────────────

/// <summary>
/// ProblemDetails (RFC 9457) enrichi d'un code métier stable. Le front réagit au code,
/// jamais au texte : le texte est pour l'humain, le code est pour la machine.
/// </summary>
public sealed record ApiProblemDto(
    string Type,
    string Title,
    int Status,
    string Detail,
    string Code,
    IReadOnlyDictionary<string, string[]>? Errors);
