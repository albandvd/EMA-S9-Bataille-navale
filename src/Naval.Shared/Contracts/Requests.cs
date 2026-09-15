namespace Naval.Shared.Contracts;

/// <summary>Coordonnée 0-based. X = colonne (0 → A), Y = ligne (0 → 1).</summary>
public readonly record struct CoordinateDto(int X, int Y);

// ─────────────────────────────── Cycle de vie ───────────────────────────────

/// <summary>Crée une partie. L'appelant devient le joueur 1.</summary>
/// <param name="PlayerName">Pseudo affiché, 2 à 16 caractères.</param>
/// <param name="Mode">Solo, privée (code) ou publique (salon).</param>
/// <param name="GridWidth">8 à 16.</param>
/// <param name="GridHeight">8 à 16.</param>
/// <param name="FleetPreset">"Classic" (5 navires) ou "Skirmish" (3 navires).</param>
/// <param name="AiLevel">Requis si Mode == SinglePlayer, ignoré sinon.</param>
/// <param name="Powers">Exactement 3 pouvoirs, ou aucun pour désactiver la mécanique.</param>
/// <param name="TurnTimeoutSeconds">0 = pas de limite. 10 à 120 sinon.</param>
/// <param name="Seed">Graine de génération, pour rejouer une partie à l'identique. Optionnel.</param>
public sealed record CreateGameRequest(
    string            PlayerName,
    GameMode          Mode,
    int               GridWidth,
    int               GridHeight,
    string            FleetPreset,
    AiLevel?          AiLevel,
    IReadOnlyList<PowerId> Powers,
    int               TurnTimeoutSeconds,
    int?              Seed);

/// <summary>Rejoint une partie privée par son code, ou une partie publique par son identifiant.</summary>
public sealed record JoinGameRequest(
    string  PlayerName,
    string? JoinCode,
    Guid?   GameId,
    IReadOnlyList<PowerId> Powers);

// ─────────────────────────────── Déploiement ───────────────────────────────

/// <param name="Type">Type de navire ; détermine sa taille via le preset de flotte.</param>
/// <param name="Origin">Case la plus en haut à gauche occupée par le navire.</param>
public sealed record ShipPlacementDto(
    ShipType      Type,
    CoordinateDto Origin,
    Orientation   Orientation);

/// <summary>Soumet la flotte complète. Rejeté si un seul navire est invalide : pas de placement partiel.</summary>
public sealed record PlaceFleetRequest(IReadOnlyList<ShipPlacementDto> Ships);

/// <summary>Demande une proposition de flotte valide. Ne l'applique pas.</summary>
public sealed record RandomFleetRequest(int? Seed);

// ─────────────────────────────── Actions ───────────────────────────────

/// <summary>Tir normal sur la grille adverse.</summary>
public sealed record FireRequest(CoordinateDto Target);

/// <summary>
/// Cible d'un pouvoir. Les champs utilisés dépendent du <see cref="TargetKind"/> de la
/// définition du pouvoir ; les autres doivent être nuls.
/// </summary>
/// <param name="Cell">Pour Cell et Area : la case d'origine.</param>
/// <param name="ShipId">Pour OwnShip : identifiant du navire de sa propre flotte.</param>
/// <param name="LineIndex">Pour Line : index de la ligne ou de la colonne.</param>
/// <param name="LineIsRow">Pour Line : true = ligne, false = colonne.</param>
/// <param name="CarrierShipId">Navire porteur, requis si ChargeTurns ≥ 3.</param>
public sealed record PowerTargetDto(
    CoordinateDto? Cell,
    string?        ShipId,
    int?           LineIndex,
    bool?          LineIsRow,
    string?        CarrierShipId);

/// <summary>Active un pouvoir équipé.</summary>
public sealed record UsePowerRequest(PowerId PowerId, PowerTargetDto Target);

/// <summary>Annule une charge en cours. L'énergie dépensée n'est pas remboursée.</summary>
public sealed record CancelChargeRequest(PowerId PowerId);

/// <summary>Envoie une emote à l'adversaire. Pas de texte libre : rien à modérer.</summary>
public sealed record SendEmoteRequest(EmoteCode Code);
