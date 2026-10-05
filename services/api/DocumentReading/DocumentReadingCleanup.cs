namespace ogarniamy_zwierzaki_api.DocumentReading;

// Deletes provider analysis results that are no longer needed: right after their output is checkpointed (called by
// the worker) and on a periodic pass that is independent of job state, so obligations left by a crash, a lost lease
// or a terminal failure are retried after a restart. A 204 or a "not found" from the authenticated provider marks the
// run file's cleanup Acknowledged, which is a deletion request, not verified erasure. Run-file records are never
// deleted. A submission whose response was lost has no known result ID; it cannot be deleted early and relies on the
// provider's bounded retention.
public sealed class DocumentReadingCleanup(
    IServiceScopeFactory scopes, IDocumentTextReader reader, ILogger<DocumentReadingCleanup> logger)
{
    public int BatchSize { get; init; } = 20;

    // How long a failed deletion waits before the next try, by the number of earlier failed tries.
    public static TimeSpan BackoffAfter(int failedAttempts) => failedAttempts switch
    {
        0 => TimeSpan.FromMinutes(1),
        1 => TimeSpan.FromMinutes(5),
        2 => TimeSpan.FromMinutes(30),
        _ => TimeSpan.FromHours(2),
    };

    // One pass over the due cleanups. Returns how many were acknowledged.
    public async Task<int> RunDueAsync(CancellationToken cancellationToken)
    {
        List<DocumentReadingCleanupWork> due;
        await using (var scope = scopes.CreateAsyncScope())
        {
            due = await scope.ServiceProvider.GetRequiredService<DocumentReadingJobs>()
                .ListDueCleanupsAsync(BatchSize, cancellationToken);
        }

        var acknowledged = 0;
        foreach (var work in due)
        {
            if (await DeleteAsync(work, cancellationToken))
            {
                acknowledged++;
            }
        }

        return acknowledged;
    }

    // Requests deletion of one result. True when acknowledged; otherwise the cleanup stays pending with a backoff.
    public async Task<bool> DeleteAsync(DocumentReadingCleanupWork work, CancellationToken cancellationToken)
    {
        try
        {
            await reader.DeleteResultAsync(work.ProviderOperationId, cancellationToken);
        }
        catch (DocumentReadFailure failure)
        {
            var delay = BackoffAfter(work.AttemptCount);
            await UseJobsAsync(jobs => jobs.DeferCleanupAsync(work.RunFileId, delay, cancellationToken));
            logger.LogWarning(
                "Provider result deletion for run file {RunFileId} of document {DocumentId} failed ({FailureCode}); " +
                "retrying in {RetryDelay}.",
                work.RunFileId, work.DocumentId, failure.Code, delay);
            return false;
        }

        await UseJobsAsync(jobs => jobs.AcknowledgeCleanupAsync(work.RunFileId, cancellationToken));
        logger.LogInformation(
            "Provider acknowledged result deletion for run file {RunFileId} of document {DocumentId}.",
            work.RunFileId, work.DocumentId);
        return true;
    }

    private async Task UseJobsAsync(Func<DocumentReadingJobs, Task<bool>> action)
    {
        await using var scope = scopes.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<DocumentReadingJobs>());
    }
}
