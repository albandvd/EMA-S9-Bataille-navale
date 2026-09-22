using Naval.Shared.Contracts;

namespace Naval.Shared.Domain;

/// <summary>Résumé léger pour le salon public (GET /api/games/open).</summary>
public sealed record GameSummary(
    Guid GameId,
    string HostName,
    int GridWidth,
    int GridHeight,
    string FleetPreset,
    bool PowersEnabled,
    int TurnTimeoutSeconds,
    DateTimeOffset CreatedAtUtc);
