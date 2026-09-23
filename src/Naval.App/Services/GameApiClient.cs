namespace Naval.App.Services;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Naval.Shared.Contracts;

/// <summary>Implémentation HTTP réelle, branchée sur Naval.Api.</summary>
public sealed class GameApiClient(HttpClient http) : IGameApiClient
{
    private const string PlayerTokenHeader = "X-Player-Token";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public Task<CreateGameResponse> CreateGameAsync(CreateGameRequest request, CancellationToken ct) =>
        SendAsync<CreateGameResponse>(HttpMethod.Post, "api/games", request, null, ct);

    public Task<JoinGameResponse> JoinGameAsync(JoinGameRequest request, CancellationToken ct) =>
        SendAsync<JoinGameResponse>(HttpMethod.Post, "api/games/join", request, null, ct);

    public Task<IReadOnlyList<OpenGameDto>> ListOpenGamesAsync(CancellationToken ct) =>
        SendAsync<IReadOnlyList<OpenGameDto>>(HttpMethod.Get, "api/games/open", null, null, ct);

    public Task<GameStateDto> GetGameAsync(Guid gameId, string playerToken, CancellationToken ct) =>
        SendAsync<GameStateDto>(HttpMethod.Get, $"api/games/{gameId}", null, playerToken, ct);

    public Task<GameStateDto> PlaceFleetAsync(Guid gameId, string playerToken, PlaceFleetRequest request, CancellationToken ct) =>
        SendAsync<GameStateDto>(HttpMethod.Post, $"api/games/{gameId}/fleet", request, playerToken, ct);

    public Task<ShotResultDto> FireAsync(Guid gameId, string playerToken, FireRequest request, CancellationToken ct) =>
        SendAsync<ShotResultDto>(HttpMethod.Post, $"api/games/{gameId}/shots", request, playerToken, ct);

    public Task<PowerResultDto> UsePowerAsync(Guid gameId, string playerToken, UsePowerRequest request, CancellationToken ct) =>
        SendAsync<PowerResultDto>(HttpMethod.Post, $"api/games/{gameId}/powers", request, playerToken, ct);

    public Task<GameOverDto> ForfeitAsync(Guid gameId, string playerToken, CancellationToken ct) =>
        SendAsync<GameOverDto>(HttpMethod.Post, $"api/games/{gameId}/forfeit", null, playerToken, ct);

    public Task<IReadOnlyList<PowerDefinitionDto>> GetPowerCatalogAsync(CancellationToken ct) =>
        SendAsync<IReadOnlyList<PowerDefinitionDto>>(HttpMethod.Get, "api/catalog/powers", null, null, ct);

    public Task<SpectatorViewDto> GetSpectatorViewAsync(Guid gameId, CancellationToken ct) =>
        SendAsync<SpectatorViewDto>(HttpMethod.Get, $"api/games/{gameId}/spectate", null, null, ct);

    public async Task<FleetPresetDto> GetFleetPresetAsync(string presetName, CancellationToken ct)
    {
        var presets = await SendAsync<IReadOnlyList<FleetPresetDto>>(HttpMethod.Get, "api/catalog/fleets", null, null, ct);
        var preset = presets.FirstOrDefault(p => p.Name == presetName);

        if (preset is null)
        {
            throw new GameApiException(new ApiProblemDto(
                "about:blank", "Preset introuvable", 404,
                $"Aucun preset de flotte nommé « {presetName} ».",
                ErrorCodes.GameNotFound, null));
        }

        return preset;
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string url, object? body, string? playerToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);

        if (playerToken is not null)
        {
            request.Headers.Add(PlayerTokenHeader, playerToken);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        using var response = await http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiProblemDto>(JsonOptions, ct)
                ?? new ApiProblemDto("about:blank", "Erreur", (int)response.StatusCode,
                    response.ReasonPhrase ?? "Une erreur est survenue.", "INTERNAL_ERROR", null);
            throw new GameApiException(problem);
        }

        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct))!;
    }
}
