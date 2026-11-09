using System.Diagnostics;
using ogarniamy_zwierzaki_api.Documents;

namespace ogarniamy_zwierzaki_api.DocumentEmbedding;

// The background embedder of read documents. One job at a time: claim a due job with a lease, read the document's
// published pages and its animal's current name in one snapshot, cut the pages into chunks, embed
// "{animal name}\n\n{chunk text}" for each, then replace the document's chunks and mark the job embedded in one
// transaction, only if the reading revision and the animal name are still the ones embedded (otherwise the result is
// discarded and the job re-queued). No database transaction is held during the provider call. A transient failure
// requeues the job with a bounded backoff; a terminal one fails it with a sanitized code. Logs carry IDs, counts,
// states, codes and timings only, never document text, the animal's name or provider payloads.
//
// Only the hosted loop checks configuration; RunOnceAsync lets tests drive single jobs with a fake client.
public sealed class DocumentEmbeddingWorker(
    IServiceScopeFactory scopes,
    EmbeddingAvailability availability,
    IEmbeddingClient client,
    DocumentEmbeddingWakeUp wakeUp,
    TimeProvider time,
    ILogger<DocumentEmbeddingWorker> logger) : BackgroundService
{
    // Including the first attempt; a transient failure of the last one fails the job.
    public const int MaxAttempts = 5;

    // The wait before attempts 2..5; a longer Retry-After from the provider wins.
    public static readonly IReadOnlyList<TimeSpan> RetryDelays =
        [TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30)];

    // The idle wait between claims when nothing is due. It doubles on every idle pass up to MaxPollInterval and drops
    // back after a claim or a wake-up, so an idle app polls about once a minute while fresh work starts within seconds.
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan MaxPollInterval { get; init; } = TimeSpan.FromSeconds(60);

    // Longer than AttemptDeadline, so an attempt is cancelled before its lease can expire. A paced attempt extends both by
    // its expected pacing wait.
    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromMinutes(10);

    // One processing attempt may take this long, plus the pacing wait its document needs; running out counts as a
    // transient failure.
    public TimeSpan AttemptDeadline { get; init; } = TimeSpan.FromMinutes(5);

    // The hosted loop waits this long after the app starts before its first claim, so a deploy or restart on a small
    // CPU share is not starved by background work (see DocumentReadingWorker).
    public TimeSpan StartupDelay { get; init; } = TimeSpan.FromSeconds(60);

    // Created by the hosted loop from Embedding:WorkerTokensPerMinute; RunOnceAsync paces only when a test sets one.
    private EmbeddingTokenPacer? pacer;

    public EmbeddingTokenPacer? Pacer
    {
        init => pacer = value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!availability.IsAvailable || !availability.Options.WorkerEnabled)
        {
            logger.LogInformation(
                "The embedding worker is not running: {Reason}",
                availability.IsAvailable ? "Embedding:WorkerEnabled is false." : availability.Reason);
            return;
        }

        pacer ??= new EmbeddingTokenPacer(availability.Options.WorkerTokensPerMinute, time);
        logger.LogInformation("The embedding worker started; first claim after {StartupDelay}.", StartupDelay);
        try
        {
            await Task.Delay(StartupDelay, time, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var idleWait = PollInterval;
        while (!stoppingToken.IsCancellationRequested)
        {
            var claimed = false;
            try
            {
                claimed = await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // The type only: database errors can quote row values.
                logger.LogError("The embedding worker loop failed ({ExceptionType}); continuing.", exception.GetType().Name);
            }

            if (claimed)
            {
                idleWait = PollInterval;
                continue;
            }

            try
            {
                var woken = await wakeUp.WaitAsync(idleWait, time, stoppingToken);
                idleWait = woken
                    ? PollInterval
                    : TimeSpan.FromTicks(Math.Min(idleWait.Ticks * 2, Math.Max(MaxPollInterval.Ticks, PollInterval.Ticks)));
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    // Claims and processes one due job. False when nothing was due.
    public async Task<bool> RunOnceAsync(CancellationToken cancellationToken)
    {
        var claim = await UseJobsAsync(jobs => jobs.ClaimNextAsync(LeaseDuration, cancellationToken));
        if (claim is null)
        {
            return false;
        }

        var started = Stopwatch.GetTimestamp();
        if (claim.AttemptCount > MaxAttempts)
        {
            // Only a reclaim after the lease expired gets here: earlier attempts ended without handing the job back,
            // typically because the process crashed. Stop before doing any work.
            if (await UseJobsAsync(jobs => jobs.FailAsync(claim, EmbeddingFailure.AttemptsExhausted, CancellationToken.None)))
            {
                logger.LogWarning(
                    "Embedding of document {DocumentId} was claimed for attempt {Attempt} after earlier attempts ended " +
                    "without a result; it failed ({FailureCode}).",
                    claim.DocumentId, claim.AttemptCount, EmbeddingFailure.AttemptsExhausted);
            }

            return true;
        }

        logger.LogInformation(
            "Embedding document {DocumentId}: attempt {Attempt}.", claim.DocumentId, claim.AttemptCount);

        using var deadline = new CancellationTokenSource(AttemptDeadline, time);
        using var processing = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            await ProcessAsync(claim, started, deadline, processing.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down: hand the job back at once instead of waiting for the lease to expire, without counting the
            // attempt (the platform stopped it; nothing failed).
            await TryRequeueOnShutdownAsync(claim);
            throw;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            await HandleFailureAsync(claim, EmbeddingFailure.Transient(EmbeddingFailure.AttemptDeadlineExceeded), started);
        }
        catch (EmbeddingFailure failure)
        {
            await HandleFailureAsync(claim, failure, started);
        }
        catch (Exception exception) when (TransientFailures.Is(exception, cancellationToken))
        {
            // The database is unavailable.
            await HandleFailureAsync(claim, EmbeddingFailure.Transient(EmbeddingFailure.ProviderUnavailable), started);
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Embedding document {DocumentId} failed unexpectedly ({ExceptionType}).",
                claim.DocumentId, exception.GetType().Name);
            await HandleFailureAsync(claim, EmbeddingFailure.Transient(EmbeddingFailure.UnexpectedError), started);
        }

        return true;
    }

    private async Task ProcessAsync(
        DocumentEmbeddingClaim claim, long started, CancellationTokenSource deadline, CancellationToken cancellationToken)
    {
        var source = await UseJobsAsync(jobs => jobs.LoadSourceAsync(claim.DocumentId, cancellationToken))
            ?? throw EmbeddingFailure.Terminal(EmbeddingFailure.TextUnavailable);

        var chunks = PageChunker.Chunk(source.Pages);
        var inputs = chunks.Select(chunk => EmbeddingInput.For(source.AnimalName, chunk.Text)).ToList();
        var batches = AzureOpenAiEmbeddingClient.Batches(inputs, availability.Options.MaxEstimatedTokensPerRequest).ToList();

        // A document whose tokens do not fit the pacer's allowance waits between batches. Give this attempt (and its
        // lease) that much more time, so a long document finishes instead of timing out on every attempt.
        var pacing = pacer?.EstimateWait(batches.Sum(b => b.Sum(AzureOpenAiEmbeddingClient.EstimateTokens))) ?? TimeSpan.Zero;
        if (pacing > TimeSpan.Zero)
        {
            if (!await UseJobsAsync(jobs => jobs.ExtendLeaseAsync(claim, LeaseDuration + pacing, cancellationToken)))
            {
                logger.LogWarning(
                    "Lost the lease on document {DocumentId} before embedding; stopped without calling the provider.",
                    claim.DocumentId);
                return;
            }

            deadline.CancelAfter(AttemptDeadline + pacing);
            logger.LogInformation(
                "Document {DocumentId} needs about {PacingWait} of pacing; the attempt deadline is extended to match.",
                claim.DocumentId, pacing);
        }

        var vectors = new List<float[]>(inputs.Count);
        foreach (var batch in batches)
        {
            var wait = pacer?.Reserve(batch.Sum(AzureOpenAiEmbeddingClient.EstimateTokens)) ?? TimeSpan.Zero;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, time, cancellationToken);
            }

            vectors.AddRange(await client.EmbedAsync(batch, cancellationToken));
        }
        if (vectors.Count != chunks.Count || vectors.Any(v => v.Length != EmbeddingOptions.StoredDimensions))
        {
            throw EmbeddingFailure.Terminal(EmbeddingFailure.ProviderProtocol);
        }

        var version = EmbeddingInput.Version(availability.Options.Deployment, availability.Options.Dimensions);
        var publication = await UseJobsAsync(
            jobs => jobs.PublishAsync(claim, source, version, chunks, vectors, cancellationToken));
        switch (publication)
        {
            case DocumentEmbeddingPublication.Embedded:
                logger.LogInformation(
                    "Document {DocumentId} embedded: {ChunkCount} chunks from {PageCount} pages in {ElapsedMs} ms.",
                    claim.DocumentId, chunks.Count, source.Pages.Count, ElapsedMs(started));
                break;
            case DocumentEmbeddingPublication.Stale:
                logger.LogInformation(
                    "Document {DocumentId} changed while it was embedded (run {RunGeneration}); the result was discarded " +
                    "and the job re-queued.",
                    claim.DocumentId, source.RunGeneration);
                break;
            default:
                logger.LogWarning(
                    "Lost the lease on document {DocumentId} after {ElapsedMs} ms; stopped without publishing.",
                    claim.DocumentId, ElapsedMs(started));
                break;
        }
    }

    private async Task HandleFailureAsync(DocumentEmbeddingClaim claim, EmbeddingFailure failure, long started)
    {
        // A rejected identity or endpoint is retried like a transient failure: a role grant can take a while to take
        // effect, and the job should not stay failed forever because the worker started a little too early.
        if ((failure.IsTransient || failure.IsConfiguration) && claim.AttemptCount < MaxAttempts)
        {
            var delay = RetryDelays[Math.Clamp(claim.AttemptCount - 1, 0, RetryDelays.Count - 1)];
            if (failure.RetryAfter is { } retryAfter && retryAfter > delay)
            {
                delay = retryAfter;
            }

            if (await UseJobsAsync(jobs => jobs.RequeueAsync(claim, delay, CancellationToken.None)))
            {
                if (failure.IsConfiguration)
                {
                    logger.LogWarning(
                        "Embedding document {DocumentId} was rejected by the provider ({FailureCode}) on attempt " +
                        "{Attempt}; retrying in {RetryDelay}. Check Embedding:Endpoint, Embedding:Deployment and the " +
                        "Cognitive Services OpenAI User role.",
                        claim.DocumentId, failure.Code, claim.AttemptCount, delay);
                }
                else
                {
                    logger.LogInformation(
                        "Embedding document {DocumentId} failed transiently ({FailureCode}) after {ElapsedMs} ms on attempt " +
                        "{Attempt}; retrying in {RetryDelay}.",
                        claim.DocumentId, failure.Code, ElapsedMs(started), claim.AttemptCount, delay);
                }
            }

            return;
        }

        if (!await UseJobsAsync(jobs => jobs.FailAsync(claim, failure.Code, CancellationToken.None)))
        {
            return;
        }

        if (failure.IsConfiguration)
        {
            logger.LogWarning(
                "Embedding document {DocumentId} failed: the provider rejected the API's configuration or identity " +
                "({FailureCode}). Check Embedding:Endpoint, Embedding:Deployment and the Cognitive Services OpenAI User role.",
                claim.DocumentId, failure.Code);
        }
        else
        {
            logger.LogInformation(
                "Embedding document {DocumentId} failed ({FailureCode}) after {ElapsedMs} ms on attempt {Attempt}.",
                claim.DocumentId, failure.Code, ElapsedMs(started), claim.AttemptCount);
        }
    }

    private async Task TryRequeueOnShutdownAsync(DocumentEmbeddingClaim claim)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await UseJobsAsync(jobs => jobs.RequeueAsync(claim, TimeSpan.Zero, timeout.Token, countAttempt: false));
        }
        catch (Exception exception)
        {
            logger.LogInformation("Document {DocumentId} is left to lease expiry at shutdown ({ExceptionType}).",
                claim.DocumentId, exception.GetType().Name);
        }
    }

    private static long ElapsedMs(long started) => (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

    private async Task<T> UseJobsAsync<T>(Func<DocumentEmbeddingJobs, Task<T>> action)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<DocumentEmbeddingJobs>());
    }
}
