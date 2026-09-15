namespace Naval.App.Services;

using Naval.Shared.Contracts;

/// <summary>
/// Implémentation HTTP réelle. Pas encore branchée dans Program.cs : Naval.Api n'existe pas
/// encore côté binôme moteur. À compléter dans un cycle ultérieur.
/// </summary>
public sealed class GameApiClient : IGameApiClient
{
    public Task<CreateGameResponse> CreateGameAsync(CreateGameRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<JoinGameResponse> JoinGameAsync(JoinGameRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<OpenGameDto>> ListOpenGamesAsync(CancellationToken ct) => throw new NotImplementedException();
    public Task<GameStateDto> GetGameAsync(Guid gameId, string playerToken, CancellationToken ct) => throw new NotImplementedException();
    public Task<GameStateDto> PlaceFleetAsync(Guid gameId, string playerToken, PlaceFleetRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<ShotResultDto> FireAsync(Guid gameId, string playerToken, FireRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<PowerResultDto> UsePowerAsync(Guid gameId, string playerToken, UsePowerRequest request, CancellationToken ct) => throw new NotImplementedException();
    public Task<GameStateDto> ForfeitAsync(Guid gameId, string playerToken, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<PowerDefinitionDto>> GetPowerCatalogAsync(CancellationToken ct) => throw new NotImplementedException();
}
