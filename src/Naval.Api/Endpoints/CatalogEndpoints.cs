using Naval.Shared.Contracts;
using Naval.Shared.Domain;

namespace Naval.Api.Endpoints;

public static class CatalogEndpoints
{
    public static void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/catalog").WithTags("Catalog");

        group.MapGet("/fleets", GetFleetPresets)
             .AllowAnonymous();

        group.MapGet("/powers", GetPowerCatalog)
             .AllowAnonymous();

        group.MapGet("/power-presets", GetPowerPresets)
             .AllowAnonymous();
    }

    private static IResult GetFleetPresets() =>
        Results.Ok(FleetPresets.All.Select(p => new FleetPresetDto(
            p.Name,
            p.Description,
            p.Ships.Select(s => new FleetShipDto(s.Type, s.Size, s.Count)).ToList(),
            p.MinGridSize)));

    private static IResult GetPowerCatalog() =>
        Results.Ok(PowerCatalog.All);

    private static IResult GetPowerPresets() =>
        Results.Ok(PowerPresets.All);
}
