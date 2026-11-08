using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ogarniamy_zwierzaki_api.Auth;

namespace ogarniamy_zwierzaki_api.Search;

// Semantic search over the signed-in user's documents. The route requires a session (fallback policy) and an
// antiforgery token in the X-CSRF-TOKEN header (it is a POST, so the query never lands in URLs or access logs). Results
// are only the caller's own documents; there is no way to name another account's data.
public static class SearchEndpoints
{
    // 500 characters of JSON-escaped text stay far below this.
    private const long SearchRequestBytes = 16 * 1024;

    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/search", SearchAsync)
            .WithName("SearchDocuments")
            .AddEndpointFilter<AntiforgeryHeaderFilter>()
            .WithMetadata(new RequestSizeLimitAttribute(SearchRequestBytes));
        return app;
    }

    // 400 invalid_query for an empty or too long query; 503 search_unavailable when embedding is unavailable or the
    // provider failed (never partial or substitute results); otherwise 200, with no items when nothing matches.
    private static async Task<Results<Ok<SearchResponse>, ProblemHttpResult>> SearchAsync(
        SearchRequest? request, ClaimsPrincipal principal, UserManager<AppUser> users, OwnedSearch search,
        CancellationToken cancellationToken)
    {
        var query = OwnedSearch.TryNormalizeQuery(request?.Query);
        if (query is null)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "invalid_query");
        }

        var response = await search.SearchAsync(CurrentUser.Id(principal, users), query, cancellationToken);
        return response is null
            ? ApiProblem.Create(StatusCodes.Status503ServiceUnavailable, "search_unavailable")
            : TypedResults.Ok(response);
    }
}
