using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;

namespace ogarniamy_zwierzaki_api.Auth;

// Requires a valid antiforgery request token in the configured header (X-CSRF-TOKEN) before the endpoint runs, for
// JSON and multipart mutations alike. A missing header is refused before the token store is consulted, so the body
// (for example a multipart form) is never read to look for a token elsewhere.
public sealed class AntiforgeryHeaderFilter(IAntiforgery antiforgery, IOptions<AntiforgeryOptions> options) : IEndpointFilter
{
    public const string HeaderName = "X-CSRF-TOKEN";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var headerName = options.Value.HeaderName ?? HeaderName;
        if (!httpContext.Request.Headers.ContainsKey(headerName) || !await antiforgery.IsRequestValidAsync(httpContext))
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "invalid_antiforgery_token");
        }

        return await next(context);
    }
}
