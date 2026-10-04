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
        // Blob Storage answered with an error or could not be reached; expected answers are handled by the adapter.
        RequestFailedException => true,
        DbException { IsTransient: true } => true,
        // A timeout: the request itself was not cancelled.
        TimeoutException or OperationCanceledException => true,
        _ => exception.InnerException is { } inner && IsTransient(inner),
    };
}
