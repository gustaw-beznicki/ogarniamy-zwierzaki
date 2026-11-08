using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ogarniamy_zwierzaki_api.Search;

// Per-account limit on POST /api/search. Every search costs one paid embedding call and one vector scan, and searches
// share the deployment's tokens-per-minute limit with the background worker, so one session or script must not be able
// to use it up. A fixed one-minute window per signed-in user (the route requires a session, so anonymous requests are
// refused before the limiter); a rejected request is 429 search_rate_limited with Retry-After, and the query is never
// logged.
public static class SearchRateLimit
{
    public const string PolicyName = "search";

    public static IServiceCollection AddSearchRateLimit(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(PolicyName, context =>
            {
                // Invalid settings make search itself unavailable; the limiter then falls back to the default.
                var permits = SearchOptions.TryRead(
                    context.RequestServices.GetRequiredService<IOptions<SearchOptions>>(), out _)?.MaxRequestsPerMinute
                    ?? new SearchOptions().MaxRequestsPerMinute;
                var userIdClaim = context.RequestServices.GetRequiredService<IOptions<IdentityOptions>>().Value
                    .ClaimsIdentity.UserIdClaimType;
                var user = context.User.FindFirstValue(userIdClaim) ?? string.Empty;
                return RateLimitPartition.GetFixedWindowLimiter(user, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permits,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
            });
            options.OnRejected = async (rejected, cancellationToken) =>
            {
                var response = rejected.HttpContext.Response;
                if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(
                        System.Globalization.CultureInfo.InvariantCulture);
                }

                await ApiProblem.Create(StatusCodes.Status429TooManyRequests, "search_rate_limited")
                    .ExecuteAsync(rejected.HttpContext);
            };
        });
}
