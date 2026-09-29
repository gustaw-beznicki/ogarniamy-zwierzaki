using Microsoft.AspNetCore.Http.HttpResults;

namespace ogarniamy_zwierzaki_api;

// Error responses are ProblemDetails with a machine-readable `code` extension; the UI owns the user-facing text.
public static class ApiProblem
{
    public static ProblemHttpResult Create(int statusCode, string code) =>
        TypedResults.Problem(statusCode: statusCode, extensions: new Dictionary<string, object?> { ["code"] = code });
}
