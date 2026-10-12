using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ogarniamy_zwierzaki_api.Auth;

namespace ogarniamy_zwierzaki_api.DocumentReading;

// Reading status and owner Retry of a Stored document. Both routes require a session (fallback policy); Retry also
// requires an antiforgery token in the X-CSRF-TOKEN header. Another account's, missing and Uploading documents are
// indistinguishable (404). Served through OwnedDocumentReading only, never through the worker's DocumentReadingJobs.
public static class DocumentReadingEndpoints
{
    private const long EmptyRequestBytes = 1024;

    public static IEndpointRouteBuilder MapDocumentReadingEndpoints(this IEndpointRouteBuilder app)
    {
        var reading = app.MapGroup("/api/documents/{documentId:guid}/reading");
        reading.MapGet("/", GetAsync).WithName("GetDocumentReading");
        reading.MapPost("/retry", RetryAsync)
            .WithName("RetryDocumentReading")
            .AddEndpointFilter<AntiforgeryHeaderFilter>()
            .WithMetadata(new RequestSizeLimitAttribute(EmptyRequestBytes));
        return app;
    }

    private static async Task<Results<Ok<DocumentReadingResponse>, NotFound>> GetAsync(
        Guid documentId, ClaimsPrincipal principal, UserManager<AppUser> users, OwnedDocumentReading reading,
        CancellationToken cancellationToken)
    {
        var status = await reading.FindAsync(CurrentUser.Id(principal, users), documentId, cancellationToken);
        return status is null ? TypedResults.NotFound() : TypedResults.Ok(status);
    }

    // 200 with the status after an accepted or coalesced Retry; 429 retry_limited with retryAfter (and a Retry-After
    // header in seconds) while the hourly limit blocks it.
    private static async Task<Results<Ok<DocumentReadingResponse>, NotFound, ProblemHttpResult>> RetryAsync(
        Guid documentId, HttpContext context, ClaimsPrincipal principal, UserManager<AppUser> users,
        OwnedDocumentReading reading, DocumentReadingWakeUp wakeUp, TimeProvider time, CancellationToken cancellationToken)
    {
        var result = await reading.RetryAsync(CurrentUser.Id(principal, users), documentId, cancellationToken);
        if (result.Status is not { } status)
        {
            return TypedResults.NotFound();
        }

        if (result.Outcome == DocumentReadingRetryOutcome.Accepted)
        {
            wakeUp.Notify();
        }

        if (result.Outcome != DocumentReadingRetryOutcome.RateLimited)
        {
            return TypedResults.Ok(status);
        }

        if (status.RetryAfter is { } retryAfter)
        {
            var seconds = Math.Max(1, (long)Math.Ceiling((retryAfter - time.GetUtcNow()).TotalSeconds));
            context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }

        return TypedResults.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            extensions: new Dictionary<string, object?> { ["code"] = "retry_limited", ["retryAfter"] = status.RetryAfter });
    }
}
