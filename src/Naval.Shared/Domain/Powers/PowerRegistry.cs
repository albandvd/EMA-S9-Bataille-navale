using Naval.Shared.Contracts;

namespace Naval.Shared.Domain.Powers;

/// <summary>Indexe les handlers par PowerId. Construite par Naval.Api au démarrage à partir
/// des handlers enregistrés en DI ; ce type lui-même ne dépend d'aucun conteneur DI.</summary>
public sealed class PowerRegistry
{
    private readonly IReadOnlyDictionary<PowerId, IPowerHandler> _handlers;

    public PowerRegistry(IEnumerable<IPowerHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.Id);
    }

    public IPowerHandler? Find(PowerId id) => _handlers.GetValueOrDefault(id);
}
