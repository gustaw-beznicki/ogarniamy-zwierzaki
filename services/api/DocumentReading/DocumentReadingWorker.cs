using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ogarniamy_zwierzaki_api.Documents;
using ogarniamy_zwierzaki_api.Storage;

namespace ogarniamy_zwierzaki_api.DocumentReading;

// The background reader of Stored documents. One job at a time: claim a due job with a renewable lease, read every
// original in manifest order (a PDF's own text layer locally, the pages that need it and every photo with the
// provider's OCR), checkpoint each original's output, then publish one complete text revision. No request and no
// database transaction is held during parsing or OCR. Progress is resumable: a saved provider operation is polled
// again instead of being resubmitted, and checkpointed output is never fetched twice. Losing the lease stops the
// attempt before anything else is written. Logs carry IDs, states, codes and timings only, never text, file names,
// provider payloads or tokens.
//
// Only the hosted loop checks configuration; RunOnceAsync lets tests drive single jobs with fakes.
public sealed class DocumentReadingWorker(
    IServiceScopeFactory scopes,
    DocumentReadingAvailability availability,
    IDocumentTextReader reader,
    PdfTextReader pdfReader,
    DocumentReadingCleanup cleanup,
    IOriginalStorage storage,
    TimeProvider time,
    ILogger<DocumentReadingWorker> logger) : BackgroundService
{
    // Including the first attempt; a transient failure of the last one fails the job.
    public const int MaxAttempts = 5;

    public const int MaxPages = PdfTextReaderProcess.MaxPages;

    public const long MaxCharacters = 10_000_000;

    // The wait before attempts 2..5; a longer Retry-After from the provider wins.
    public static readonly IReadOnlyList<TimeSpan> RetryDelays =
        [TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30)];

    private static readonly JsonSerializerOptions CheckpointJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    // The idle wait between claims when nothing is due.
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromMinutes(2);

    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(30);

    // One processing attempt may take this long; running out counts as a transient failure.
    public TimeSpan AttemptDeadline { get; init; } = TimeSpan.FromMinutes(30);

    // Between polls of a running analysis when the provider sends no Retry-After.
    public TimeSpan AnalysisPollInterval { get; init; } = TimeSpan.FromSeconds(1);

    // Assumes no continuous run time: App Service F1 sleeps after idle time and may stop mid-job. Every start (or wake-up)
    // first retries due provider cleanup, then claims due and lease-expired jobs, so work queued while the app slept
    // resumes on the next wake-up.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!availability.IsAvailable || !availability.Options.WorkerEnabled)
        {
            logger.LogInformation(
                "The document reading worker is not running: {Reason}",
                availability.IsAvailable ? "DocumentReading:WorkerEnabled is false." : availability.Reason);
            return;
        }

        logger.LogInformation("The document reading worker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            var claimed = false;
            try
            {
                await cleanup.RunDueAsync(stoppingToken);
                claimed = await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // The type only: database errors can quote row values.
                logger.LogError("The document reading worker loop failed ({ExceptionType}); continuing.", exception.GetType().Name);
            }

            if (!claimed)
            {
                try
                {
                    await Task.Delay(PollInterval, time, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
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
        logger.LogInformation(
            "Reading document {DocumentId}: run {RunGeneration}, attempt {Attempt}.",
            claim.DocumentId, claim.RunGeneration, claim.AttemptCount);

        using var leaseLost = new CancellationTokenSource();
        using var deadline = new CancellationTokenSource(AttemptDeadline, time);
        using var processing = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, leaseLost.Token, deadline.Token);
        using var stopHeartbeat = new CancellationTokenSource();
        var heartbeat = HeartbeatAsync(claim, leaseLost, stopHeartbeat.Token);
        try
        {
            await ProcessAsync(claim, leaseLost, processing.Token);
            logger.LogInformation(
                "Document {DocumentId} read in {ElapsedMs} ms.", claim.DocumentId, ElapsedMs(started));
        }
        catch (OperationCanceledException) when (leaseLost.IsCancellationRequested)
        {
            logger.LogWarning(
                "Lost the lease on document {DocumentId} after {ElapsedMs} ms; stopped without publishing.",
                claim.DocumentId, ElapsedMs(started));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down: hand the job back at once instead of waiting for the lease to expire.
            await TryRequeueOnShutdownAsync(claim);
            throw;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            await HandleFailureAsync(claim, DocumentReadFailure.Transient(DocumentReadFailure.AttemptDeadlineExceeded), started);
        }
        catch (DocumentReadFailure failure)
        {
            await HandleFailureAsync(claim, failure, started);
        }
        catch (Exception exception) when (TransientFailures.Is(exception, cancellationToken))
        {
            // Blob Storage or the database is unavailable.
            await HandleFailureAsync(claim, DocumentReadFailure.Transient(DocumentReadFailure.StorageUnavailable), started);
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Reading document {DocumentId} failed unexpectedly ({ExceptionType}).",
                claim.DocumentId, exception.GetType().Name);
            await HandleFailureAsync(claim, DocumentReadFailure.Transient(DocumentReadFailure.UnexpectedError), started);
        }
        finally
        {
            await stopHeartbeat.CancelAsync();
            await heartbeat;
        }

        // Provider results of this job (checkpointed, abandoned or of a failed run) are deleted promptly.
        try
        {
            await cleanup.RunDueAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning("Provider cleanup after document {DocumentId} failed ({ExceptionType}); it is retried later.",
                claim.DocumentId, exception.GetType().Name);
        }

        return true;
    }

    // Renews the lease until stopped. A refused renewal means the lease is gone: processing is cancelled at once.
    private async Task HeartbeatAsync(DocumentReadingClaim claim, CancellationTokenSource leaseLost, CancellationToken stop)
    {
        while (true)
        {
            try
            {
                await Task.Delay(HeartbeatInterval, time, stop);
                var renewed = await UseJobsAsync(jobs => jobs.RenewLeaseAsync(claim, LeaseDuration, stop));
                if (renewed is null)
                {
                    await leaseLost.CancelAsync();
                    return;
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // A failed renewal is tried again at the next beat; if the lease expires meanwhile, every write is refused.
                logger.LogWarning("Renewing the lease on document {DocumentId} failed ({ExceptionType}).",
                    claim.DocumentId, exception.GetType().Name);
            }
        }
    }

    private async Task ProcessAsync(
        DocumentReadingClaim claim, CancellationTokenSource leaseLost, CancellationToken cancellationToken)
    {
        var sources = await UseJobsAsync(jobs => jobs.ListSourcesAsync(claim, cancellationToken));
        if (sources.Count == 0)
        {
            await LeaseLostAsync(leaseLost);
        }

        var runFiles = await UseJobsAsync(jobs => jobs.ListRunFilesAsync(claim, cancellationToken));
        var context = new DocumentReadingJobContext(claim, leaseLost);
        var pages = new List<DocumentTextPage>();
        foreach (var source in sources)
        {
            var filePages = await ReadSourceAsync(
                context, source, runFiles.Where(r => r.DocumentFileId == source.DocumentFileId).ToList(), cancellationToken);
            pages.AddRange(filePages.Select(page => new DocumentTextPage
            {
                DocumentFileId = source.DocumentFileId,
                SourcePageNumber = page.SourcePageNumber,
                ExtractionMethod = page.Method,
                Text = page.Text,
            }));
        }

        var publication = await UseJobsAsync(jobs => jobs.PublishAsync(claim, pages, cancellationToken));
        switch (publication)
        {
            case DocumentReadingPublication.Published:
                return;
            case DocumentReadingPublication.EmptyDocument:
                throw DocumentReadFailure.Terminal(DocumentReadFailure.EmptyDocument);
            default:
                await LeaseLostAsync(leaseLost);
                return;
        }
    }

    // One original: its checkpoint when there is one, else the saved operation, else a fresh read.
    private async Task<IReadOnlyList<ReadPageResult>> ReadSourceAsync(
        DocumentReadingJobContext context, DocumentReadingSource source, List<DocumentReadingRunFile> runFiles,
        CancellationToken cancellationToken)
    {
        var active = runFiles.Where(r => r.Stage != DocumentReadingRunFileStage.Failed).ToList();
        foreach (var stale in active.Take(active.Count - 1))
        {
            await AbandonAsync(context, stale, cancellationToken);
        }

        var latest = active.LastOrDefault();
        switch (latest?.Stage)
        {
            case DocumentReadingRunFileStage.Retrieved:
                var checkpoint = JsonSerializer.Deserialize<List<ReadPageResult>>(latest.FetchedText!, CheckpointJson)!;
                context.Charge(checkpoint);
                return checkpoint;
            case DocumentReadingRunFileStage.Submitted:
                return await ResumeAsync(context, source, latest, cancellationToken);
            case DocumentReadingRunFileStage.Pending:
                await AbandonAsync(context, latest, cancellationToken);
                break;
        }

        return await ReadFreshAsync(context, source, cancellationToken);
    }

    private async Task<IReadOnlyList<ReadPageResult>> ReadFreshAsync(
        DocumentReadingJobContext context, DocumentReadingSource source, CancellationToken cancellationToken)
    {
        if (source.ContentType != DocumentFile.PdfContentType)
        {
            return await OcrAsync(context, source, null, null, cancellationToken);
        }

        IReadOnlyList<PdfLocalPage>? local = null;
        try
        {
            local = await ReadLocalAsync(source, cancellationToken);
        }
        catch (DocumentReadFailure failure) when (failure.CanFallBackToProvider)
        {
            logger.LogInformation(
                "The local parser could not read file {DocumentFileId} of document {DocumentId}; reading it with OCR.",
                source.DocumentFileId, context.Claim.DocumentId);
        }

        if (local is null)
        {
            return await OcrAsync(context, source, null, null, cancellationToken);
        }

        var ocrPages = local.Where(page => !PdfTextReader.IsUsable(page)).Select(page => page.PageNumber).ToList();
        if (ocrPages.Count == 0)
        {
            // Local only: the provider is never called.
            var pages = local
                .Select(page => new ReadPageResult(page.PageNumber, DocumentTextExtractionMethod.TextLayer, page.Text))
                .ToList();
            var runFileId = await StartRunFileAsync(context, source, null, cancellationToken);
            await CheckpointAsync(context, runFileId, null, pages, cancellationToken);
            return pages;
        }

        var ranges = PageRanges(ocrPages);
        return ranges.Length <= DocumentReadingRunFile.SelectedPageRangesMaxLength
            ? await OcrAsync(context, source, ranges, local, cancellationToken)
            // Too fragmented to name: read the whole file with OCR.
            : await OcrAsync(context, source, null, null, cancellationToken);
    }

    private async Task<IReadOnlyList<ReadPageResult>> OcrAsync(
        DocumentReadingJobContext context, DocumentReadingSource source, string? ranges, IReadOnlyList<PdfLocalPage>? local,
        CancellationToken cancellationToken)
    {
        var runFileId = await StartRunFileAsync(context, source, ranges, cancellationToken);
        string operationId;
        await using (var content = await OpenOriginalAsync(source, cancellationToken))
        {
            operationId = await reader.SubmitAsync(content, source.ContentType, ranges, cancellationToken);
        }

        // Saved even if the attempt is being cancelled, so the operation can be resumed or at least deleted.
        if (!await UseJobsAsync(jobs => jobs.RecordOperationAsync(context.Claim, runFileId, operationId, CancellationToken.None)))
        {
            await LeaseLostAsync(context.LeaseLost);
        }

        logger.LogInformation(
            "Submitted file {DocumentFileId} of document {DocumentId} for OCR as run file {RunFileId}.",
            source.DocumentFileId, context.Claim.DocumentId, runFileId);
        return await CompleteOcrAsync(context, source, runFileId, operationId, ranges, local, cancellationToken);
    }

    // A saved operation after a restart or requeue: poll it instead of submitting again.
    private async Task<IReadOnlyList<ReadPageResult>> ResumeAsync(
        DocumentReadingJobContext context, DocumentReadingSource source, DocumentReadingRunFile runFile, CancellationToken cancellationToken)
    {
        IReadOnlyList<PdfLocalPage>? local = null;
        if (runFile.SelectedPageRanges is not null)
        {
            try
            {
                local = await ReadLocalAsync(source, cancellationToken);
            }
            catch (DocumentReadFailure failure) when (failure.CanFallBackToProvider)
            {
                await AbandonAsync(context, runFile, cancellationToken);
                return await ReadFreshAsync(context, source, cancellationToken);
            }
        }

        logger.LogInformation(
            "Resuming saved OCR operation of run file {RunFileId} of document {DocumentId}.", runFile.Id, context.Claim.DocumentId);
        return await CompleteOcrAsync(
            context, source, runFile.Id, runFile.ProviderOperationId!, runFile.SelectedPageRanges, local, cancellationToken);
    }

    private async Task<IReadOnlyList<ReadPageResult>> CompleteOcrAsync(
        DocumentReadingJobContext context, DocumentReadingSource source, Guid runFileId, string operationId, string? ranges,
        IReadOnlyList<PdfLocalPage>? local, CancellationToken cancellationToken)
    {
        List<ReadPageResult> pages;
        try
        {
            var analysis = await PollAsync(operationId, cancellationToken);
            pages = Merge(source, ranges, local, analysis.Pages);
        }
        catch (DocumentReadFailure failure) when (failure.AbandonsOperation)
        {
            await UseJobsAsync(jobs => jobs.AbandonRunFileAsync(context.Claim, runFileId, CancellationToken.None));
            throw;
        }

        await CheckpointAsync(context, runFileId, operationId, pages, cancellationToken);
        return pages;
    }

    private async Task<OcrAnalysis> PollAsync(string operationId, CancellationToken cancellationToken)
    {
        while (true)
        {
            var analysis = await reader.GetAnalysisAsync(operationId, cancellationToken);
            if (analysis.IsComplete)
            {
                return analysis;
            }

            var delay = analysis.RetryAfter is { } retryAfter && retryAfter > TimeSpan.Zero
                ? TimeSpan.FromTicks(Math.Min(retryAfter.Ticks, TimeSpan.FromMinutes(1).Ticks))
                : AnalysisPollInterval;
            await Task.Delay(delay, time, cancellationToken);
        }
    }

    // The pages of one original in page order. A PDF read partly locally takes the OCR text for exactly the pages it
    // sent (by original page number) and keeps the text layer of the others.
    private static List<ReadPageResult> Merge(
        DocumentReadingSource source, string? ranges, IReadOnlyList<PdfLocalPage>? local, IReadOnlyList<ReadPageResult> ocr)
    {
        if (source.ContentType != DocumentFile.PdfContentType)
        {
            // A photo is one page, whatever the provider's page split.
            var text = string.Join('\n', ocr.OrderBy(p => p.SourcePageNumber).Select(p => p.Text).Where(t => t.Length > 0));
            return [new ReadPageResult(1, DocumentTextExtractionMethod.Ocr, text)];
        }

        if (ranges is null || local is null)
        {
            var numbers = ocr.Select(p => p.SourcePageNumber).ToList();
            if (numbers.Count == 0 || !numbers.Order().SequenceEqual(Enumerable.Range(1, numbers.Count)))
            {
                throw DocumentReadFailure.Terminal(DocumentReadFailure.ProviderProtocol, abandonsOperation: true);
            }

            if (numbers.Count > MaxPages)
            {
                throw DocumentReadFailure.Terminal(DocumentReadFailure.PageLimitExceeded);
            }

            return ocr.OrderBy(p => p.SourcePageNumber).ToList();
        }

        var requested = local.Where(page => !PdfTextReader.IsUsable(page)).Select(page => page.PageNumber).ToList();
        if (PageRanges(requested) != ranges)
        {
            // The PDF parses differently than when the operation was submitted; its output cannot be placed safely.
            throw DocumentReadFailure.Transient(DocumentReadFailure.PdfUnreadable, abandonsOperation: true);
        }

        var returned = ocr.Select(p => p.SourcePageNumber).Order().ToList();
        Dictionary<int, string> ocrText;
        if (returned.SequenceEqual(requested))
        {
            ocrText = ocr.ToDictionary(p => p.SourcePageNumber, p => p.Text);
        }
        else if (returned.SequenceEqual(Enumerable.Range(1, requested.Count)))
        {
            // Numbered by position within the selection rather than by original page.
            ocrText = ocr.OrderBy(p => p.SourcePageNumber).Select((p, i) => (requested[i], p.Text))
                .ToDictionary(p => p.Item1, p => p.Text);
        }
        else
        {
            throw DocumentReadFailure.Terminal(DocumentReadFailure.ProviderProtocol, abandonsOperation: true);
        }

        return local
            .Select(page => ocrText.TryGetValue(page.PageNumber, out var text)
                ? new ReadPageResult(page.PageNumber, DocumentTextExtractionMethod.Ocr, text)
                : new ReadPageResult(page.PageNumber, DocumentTextExtractionMethod.TextLayer, page.Text))
            .ToList();
    }

    // "1-3,5,7-8" for the provider's pages parameter and the run file's record.
    public static string PageRanges(IEnumerable<int> pageNumbers)
    {
        var ranges = new StringBuilder();
        var numbers = pageNumbers.Distinct().Order().ToList();
        for (var i = 0; i < numbers.Count;)
        {
            var end = i;
            while (end + 1 < numbers.Count && numbers[end + 1] == numbers[end] + 1)
            {
                end++;
            }

            ranges.Append(ranges.Length > 0 ? "," : string.Empty)
                .Append(numbers[i])
                .Append(end > i ? $"-{numbers[end]}" : string.Empty);
            i = end + 1;
        }

        return ranges.ToString();
    }

    private async Task<IReadOnlyList<PdfLocalPage>> ReadLocalAsync(DocumentReadingSource source, CancellationToken cancellationToken)
    {
        await using var content = await OpenOriginalAsync(source, cancellationToken);
        return await pdfReader.ReadAsync(content, cancellationToken);
    }

    // The stored original, verified against its receipt; streamed, never cached.
    private async Task<Stream> OpenOriginalAsync(DocumentReadingSource source, CancellationToken cancellationToken) =>
        await storage.OpenReadAsync(source.BlobKey, source.ByteLength, source.Sha256, cancellationToken)
        ?? throw DocumentReadFailure.Terminal(DocumentReadFailure.OriginalUnavailable);

    private async Task<Guid> StartRunFileAsync(
        DocumentReadingJobContext context, DocumentReadingSource source, string? ranges, CancellationToken cancellationToken)
    {
        var runFileId = await UseJobsAsync(jobs => jobs.StartRunFileAsync(
            context.Claim, source.DocumentFileId, ranges, cancellationToken));
        if (runFileId is null)
        {
            await LeaseLostAsync(context.LeaseLost);
        }

        return runFileId!.Value;
    }

    // Saves an original's output (within the character limit) before its provider result is deleted.
    private async Task CheckpointAsync(
        DocumentReadingJobContext context, Guid runFileId, string? operationId, List<ReadPageResult> pages, CancellationToken cancellationToken)
    {
        context.Charge(pages);
        var json = JsonSerializer.Serialize(pages, CheckpointJson);
        if (!await UseJobsAsync(jobs => jobs.RecordFetchedTextAsync(context.Claim, runFileId, json, cancellationToken)))
        {
            await LeaseLostAsync(context.LeaseLost);
        }

        if (operationId is not null)
        {
            await cleanup.DeleteAsync(
                new DocumentReadingCleanupWork(runFileId, context.Claim.DocumentId, operationId, 0), cancellationToken);
        }
    }

    private async Task AbandonAsync(DocumentReadingJobContext context, DocumentReadingRunFile runFile, CancellationToken cancellationToken)
    {
        if (runFile.Stage == DocumentReadingRunFileStage.Pending)
        {
            // The submission may have reached the provider with its response lost: no result ID is known, so it
            // cannot be deleted early and is left to the provider's bounded retention.
            logger.LogWarning(
                "Run file {RunFileId} of document {DocumentId} has an unknown submission outcome; abandoning it.",
                runFile.Id, context.Claim.DocumentId);
        }

        if (!await UseJobsAsync(jobs => jobs.AbandonRunFileAsync(context.Claim, runFile.Id, cancellationToken)))
        {
            await LeaseLostAsync(context.LeaseLost);
        }
    }

    private async Task HandleFailureAsync(DocumentReadingClaim claim, DocumentReadFailure failure, long started)
    {
        if (failure.IsTransient && claim.AttemptCount < MaxAttempts)
        {
            var delay = RetryDelays[Math.Clamp(claim.AttemptCount - 1, 0, RetryDelays.Count - 1)];
            if (failure.RetryAfter is { } retryAfter && retryAfter > delay)
            {
                delay = retryAfter;
            }

            if (await UseJobsAsync(jobs => jobs.RequeueAsync(claim, delay, CancellationToken.None)))
            {
                logger.LogInformation(
                    "Reading document {DocumentId} failed transiently ({FailureCode}) after {ElapsedMs} ms on attempt " +
                    "{Attempt}; retrying in {RetryDelay}.",
                    claim.DocumentId, failure.Code, ElapsedMs(started), claim.AttemptCount, delay);
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
                "Reading document {DocumentId} failed: the provider rejected the API's configuration or identity " +
                "({FailureCode}). Check DocumentReading:Endpoint, ModelId and the Cognitive Services User role.",
                claim.DocumentId, failure.Code);
        }
        else
        {
            logger.LogInformation(
                "Reading document {DocumentId} failed ({FailureCode}) after {ElapsedMs} ms on attempt {Attempt}.",
                claim.DocumentId, failure.Code, ElapsedMs(started), claim.AttemptCount);
        }
    }

    private async Task TryRequeueOnShutdownAsync(DocumentReadingClaim claim)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await UseJobsAsync(jobs => jobs.RequeueAsync(claim, TimeSpan.Zero, timeout.Token));
        }
        catch (Exception exception)
        {
            logger.LogInformation("Document {DocumentId} is left to lease expiry at shutdown ({ExceptionType}).",
                claim.DocumentId, exception.GetType().Name);
        }
    }

    private static async Task LeaseLostAsync(CancellationTokenSource leaseLost)
    {
        await leaseLost.CancelAsync();
        throw new OperationCanceledException(leaseLost.Token);
    }

    private static long ElapsedMs(long started) => (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

    private async Task<T> UseJobsAsync<T>(Func<DocumentReadingJobs, Task<T>> action)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<DocumentReadingJobs>());
    }
}
