using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;

namespace ogarniamy_zwierzaki_api.Auth;

// Issues the antiforgery request token for capture mutations. The paired token lives in an HttpOnly cookie; the
// token is tied to the signed-in account, so the client fetches a new one after signing in again.
public static class AntiforgeryEndpoints
{
    public static IEndpointRouteBuilder MapAntiforgeryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/antiforgery", GetToken).WithName("GetAntiforgeryToken");
        return app;
    }

    private static Ok<AntiforgeryTokenResponse> GetToken(HttpContext context, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Headers.CacheControl = "no-cache, no-store";
        context.Response.Headers.Pragma = "no-cache";
        return TypedResults.Ok(new AntiforgeryTokenResponse(
            tokens.RequestToken ?? throw new InvalidOperationException("No antiforgery request token was generated."),
            tokens.HeaderName ?? throw new InvalidOperationException("No antiforgery header name is configured.")));
    }
}
