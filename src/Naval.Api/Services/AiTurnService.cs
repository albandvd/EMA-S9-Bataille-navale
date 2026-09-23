using Naval.Shared.Contracts;
using Naval.Shared.Domain;

namespace Naval.Api.Services;

/// <summary>Déclenche le tour de l'IA après un tir humain en mode solo.</summary>
public sealed class AiTurnService
{
    private readonly GameService _svc;

    public AiTurnService(GameService svc)
    {
        _svc = svc;
    }

    public Task<(ShotResultDto result, Game game)?> PlayAsync(Guid gameId, CancellationToken ct) =>
        _svc.PlayAiTurnAsync(gameId, ct);
}
