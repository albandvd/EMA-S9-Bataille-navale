using Microsoft.AspNetCore.Mvc;

namespace Naval.Api.Infrastructure;

public static class NavalProblemDetails
{
    public static IResult Problem(int status, string title, string detail, string code,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Title = title,
            Status = status,
            Detail = detail,
        };
        problem.Extensions["code"] = code;
        if (errors is not null) problem.Extensions["errors"] = errors;
        return Results.Problem(problem);
    }

    public static IResult NotFound(string detail, string code) =>
        Problem(404, "Non trouvé", detail, code);

    public static IResult Unauthorized(string detail) =>
        Problem(401, "Non autorisé", detail, "MISSING_TOKEN");

    public static IResult Forbidden(string detail) =>
        Problem(403, "Interdit", detail, "NOT_A_PLAYER");

    public static IResult Conflict(string detail, string code) =>
        Problem(409, "Conflit", detail, code);

    public static IResult BadRequest(string detail, string code,
        IReadOnlyDictionary<string, string[]>? errors = null) =>
        Problem(400, "Requête invalide", detail, code, errors);
}
