namespace Naval.App.Services;

using Naval.Shared.Contracts;

/// <summary>
/// Abstraction du contrat REST. GameStateStore est le seul consommateur de cette interface.
/// </summary>
public interface IGameApiClient
{
    Task<CreateGameResponse> CreateGameAsync(CreateGameRequest request, CancellationToken ct);
    Task<JoinGameResponse> JoinGameAsync(JoinGameRequest request, CancellationToken ct);
    Task<IReadOnlyList<OpenGameDto>> ListOpenGamesAsync(CancellationToken ct);
    Task<GameStateDto> GetGameAsync(Guid gameId, string playerToken, CancellationToken ct);
    Task<GameStateDto> PlaceFleetAsync(Guid gameId, string playerToken, PlaceFleetRequest request, CancellationToken ct);
    Task<ShotResultDto> FireAsync(Guid gameId, string playerToken, FireRequest request, CancellationToken ct);
    Task<PowerResultDto> UsePowerAsync(Guid gameId, string playerToken, UsePowerRequest request, CancellationToken ct);
    Task<GameOverDto> ForfeitAsync(Guid gameId, string playerToken, CancellationToken ct);
    Task<IReadOnlyList<PowerDefinitionDto>> GetPowerCatalogAsync(CancellationToken ct);
    Task<FleetPresetDto> GetFleetPresetAsync(string presetName, CancellationToken ct);

    /// <summary>E-09. Lecture seule, pas de <c>playerToken</c> requis.</summary>
    Task<SpectatorViewDto> GetSpectatorViewAsync(Guid gameId, CancellationToken ct);
}
