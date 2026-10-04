using System.Data.Common;
using Azure;

namespace ogarniamy_zwierzaki_api.Documents;

// Recognises failures of Blob Storage or PostgreSQL that a client can retry (answered with 503 storage_unavailable).
// A request cancelled by the client is never one of them.
public static class TransientFailures
{
    public static bool Is(Exception exception, CancellationToken requestAborted) =>
        !requestAborted.IsCancellationRequested && IsTransient(exception);

    private static bool IsTransient(Exception exception) => exception switch
    {
        // The Azure SDK reports exhausted retries as an AggregateException of the individual failures.
        AggregateException aggregate => aggregate.InnerExceptions.Any(IsTransient),
        // Blob Storage could not be reached, timed out, throttled or failed on its side; expected answers are handled
        // by the adapter. Other 4xx answers (e.g. 403 when the API's Blob role is missing) are configuration faults:
        // they surface as unhandled 500s logged at Error instead of a retryable storage_unavailable.
        RequestFailedException { Status: 0 or 408 or 429 or >= 500 } => true,
        DbException { IsTransient: true } => true,
        // A timeout: the request itself was not cancelled.
        TimeoutException or OperationCanceledException => true,
        _ => exception.InnerException is { } inner && IsTransient(inner),
    };
}
