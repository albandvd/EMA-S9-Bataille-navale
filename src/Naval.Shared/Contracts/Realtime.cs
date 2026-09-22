namespace Naval.Shared.Contracts;

/// <summary>
/// Méthodes que le serveur peut appeler sur un client. Le hub implémente
/// Hub&lt;IGameClient&gt; : le compilateur attrape alors les fautes de frappe sur les noms de
/// méthodes, ce qu'une chaîne "ShotResolved" ne permet pas.
/// </summary>
public interface IGameClient
{
    /// <summary>Vue complète, filtrée pour ce destinataire. Envoyée après reconnexion ou changement majeur.</summary>
    Task GameStateChanged(GameStateDto state);

    /// <summary>Un tir vient d'être résolu. Chaque joueur reçoit sa propre projection.</summary>
    Task ShotResolved(ShotResultDto result);

    /// <summary>Un pouvoir a été activé ou s'est déclenché.</summary>
    Task PowerResolved(PowerResultDto result);

    /// <summary>Le tour change. La date limite vaut null si le timer est désactivé.</summary>
    Task TurnChanged(Guid currentPlayerId, DateTimeOffset? deadlineUtc, int turnNumber);

    Task OpponentJoined(string opponentName);

    /// <summary>L'adversaire a perdu la connexion. Il lui reste graceSeconds pour revenir.</summary>
    Task OpponentLeft(int graceSeconds);

    Task OpponentReconnected();

    Task EmoteReceived(Guid playerId, EmoteCode code);

    Task GameOver(GameOverDto result);

    /// <summary>Action refusée. Le front affiche le message et réactive les entrées.</summary>
    Task ActionRejected(string code, string message);
}

/// <summary>
/// Noms des méthodes exposées par le hub, côté serveur. Regroupés ici pour que le client
/// Blazor et le hub ne divergent pas.
/// </summary>
public static class GameHubMethods
{
    public const string Path = "/hub/game";

    public const string JoinGame = nameof(JoinGame);
    public const string LeaveGame = nameof(LeaveGame);
    public const string SetReady = nameof(SetReady);
    public const string Fire = nameof(Fire);
    public const string UsePower = nameof(UsePower);
    public const string CancelCharge = nameof(CancelCharge);
    public const string SendEmote = nameof(SendEmote);
    public const string Forfeit = nameof(Forfeit);
}

/// <summary>Codes d'erreur métier. Partagés pour que le front n'en invente aucun.</summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string MissingToken = "MISSING_TOKEN";
    public const string NotAPlayer = "NOT_A_PLAYER";
    public const string GameNotFound = "GAME_NOT_FOUND";
    public const string GameFull = "GAME_FULL";
    public const string InvalidJoinCode = "INVALID_JOIN_CODE";
    public const string NotYourTurn = "NOT_YOUR_TURN";
    public const string GameNotInProgress = "GAME_NOT_IN_PROGRESS";
    public const string CellAlreadyTargeted = "CELL_ALREADY_TARGETED";
    public const string OutOfBounds = "OUT_OF_BOUNDS";
    public const string OverlappingShips = "OVERLAPPING_SHIPS";
    public const string FleetIncomplete = "FLEET_INCOMPLETE";
    public const string FleetAlreadyPlaced = "FLEET_ALREADY_PLACED";
    public const string InsufficientEnergy = "INSUFFICIENT_ENERGY";
    public const string PowerOnCooldown = "POWER_ON_COOLDOWN";
    public const string PowerNotEquipped = "POWER_NOT_EQUIPPED";
    public const string PowerExhausted = "POWER_EXHAUSTED";
    public const string PowerAlreadyCharging = "POWER_ALREADY_CHARGING";
    public const string InvalidTarget = "INVALID_TARGET";
    public const string CarrierRequired = "CARRIER_REQUIRED";
    public const string ShipAlreadyDamaged = "SHIP_ALREADY_DAMAGED";
}
