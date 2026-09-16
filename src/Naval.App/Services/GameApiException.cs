namespace Naval.App.Services;

using Naval.Shared.Contracts;

/// <summary>Levée par une implémentation de IGameApiClient quand le serveur refuse une action.</summary>
public sealed class GameApiException(ApiProblemDto problem) : Exception(problem.Detail)
{
    public ApiProblemDto Problem { get; } = problem;
}
