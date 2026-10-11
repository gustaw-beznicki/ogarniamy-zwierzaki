using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ogarniamy_zwierzaki_api.Data;
using ogarniamy_zwierzaki_api.DocumentReading;
using ogarniamy_zwierzaki_api.Storage;

namespace ogarniamy_zwierzaki_api.Tests;

// The reading worker driven one job at a time (RunOnceAsync) against the real database and Azurite, with a fake OCR
// provider, a fake token and a fixed clock. The autonomous loop never runs in tests (ApiFactory pins it off).
// Originals are captured through the HTTP API, so the bytes read are the stored ones.
public sealed class DocumentReadingWorkerTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly CapturedLogs _logs = new();

    [Fact]
    public async Task A_pdf_with_a_usable_text_layer_is_read_locally_without_calling_the_provider()
    {
        await ParkExistingJobsAsync();
        var provider = new FakeOcrProvider();
        var pdf = ReadingFixtures.Pdf(
            PdfPageSpec.Text("Rabies vaccination 2026", "Batch 17"), PdfPageSpec.Text("Deworming", "Next visit"));
        var (document, _) = await CaptureAsync(ReadingFixtures.PdfOriginal(pdf));

        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));

        Assert.Empty(provider.Requests);
        var job = await JobAsync(document);
        Assert.Equal(DocumentReadingState.Read, job.State);
        var pages = await PagesAsync(document);
        Assert.Equal([1, 2], pages.Select(p => p.SourcePageNumber));
        Assert.All(pages, p => Assert.Equal(DocumentTextExtractionMethod.TextLayer, p.ExtractionMethod));
        Assert.Contains("Rabies vaccination 2026", pages[0].Text);
        Assert.True(pages[0].Text.IndexOf("Rabies", StringComparison.Ordinal) < pages[0].Text.IndexOf("Batch", StringComparison.Ordinal));
        Assert.Contains("Deworming", pages[1].Text);
        var runFile = Assert.Single(await RunFilesAsync(document));
        Assert.Equal(DocumentReadingCleanupState.NotNeeded, runFile.CleanupState);
        Assert.Null(runFile.ProviderOperationId);
        AssertLogsCarryNoContent("Rabies", "Deworming");
    }

    [Fact]
    public async Task A_mixed_pdf_sends_only_the_pages_needing_ocr_and_merges_them_by_original_page_number()
    {
        await ParkExistingJobsAsync();
        var provider = new FakeOcrProvider
        {
            RunningPolls = 1,
            Analyze = (_, _) => FakeOcrProvider.Succeeded((2, "Scanned invoice line"), (4, "")),
        };
        var pdf = ReadingFixtures.Pdf(
            PdfPageSpec.Text("Typed page one"), PdfPageSpec.Scan(), PdfPageSpec.Text("Typed page three"), PdfPageSpec.Blank());
        var original = ReadingFixtures.PdfOriginal(pdf);
        var (document, client) = await CaptureAsync(original);

        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));

        var post = Assert.Single(provider.Posts);
        Assert.Equal("2,4", FakeOcrProvider.PagesOf(post));
        Assert.Equal("application/pdf", post.ContentType);
        Assert.Equal(original.Bytes, post.Body);
        Assert.Equal("Bearer test-token", post.Authorization);
        Assert.Equal(2, provider.Polls.Count());
        Assert.Equal([provider.IdOf(1)], provider.DeletedIds);

        var pages = await PagesAsync(document);
        Assert.Equal(
            [
                (1, DocumentTextExtractionMethod.TextLayer),
                (2, DocumentTextExtractionMethod.Ocr),
                (3, DocumentTextExtractionMethod.TextLayer),
                (4, DocumentTextExtractionMethod.Ocr),
            ],
            pages.Select(p => (p.SourcePageNumber, p.ExtractionMethod)));
        Assert.Equal("Scanned invoice line", pages[1].Text);
        Assert.Equal("", pages[3].Text);
        Assert.Contains("Typed page three", pages[2].Text);
        var runFile = Assert.Single(await RunFilesAsync(document));
        Assert.Equal(("2,4", provider.IdOf(1), DocumentReadingCleanupState.Acknowledged),
            (runFile.SelectedPageRanges, runFile.ProviderOperationId, runFile.CleanupState));
        Assert.Equal(original.Bytes, await client.GetOriginalBytesAsync(Assert.Single(document.OriginalUrls())));
        AssertLogsCarryNoContent("Scanned invoice", "Typed page");
    }

    [Fact]
    public async Task Photos_are_read_in_manifest_order_and_published_with_their_positions()
    {
        await ParkExistingJobsAsync();
        var photos = new[] { SampleOriginals.Jpeg("c.jpg"), SampleOriginals.Png("a.png"), TestOriginal.Jpeg(2_000, "b.jpg") };
        var provider = new FakeOcrProvider
        {
            Analyze = (request, _) =>
                FakeOcrProvider.Succeeded((1, $"photo {Array.FindIndex(photos, p => p.Bytes.SequenceEqual(request.Body!))}")),
        };
        var (document, _) = await CaptureAsync(photos);

        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));

        Assert.Equal(photos.Select(p => p.Bytes), provider.Posts.Select(p => p.Body));
        Assert.Equal(photos.Select(p => p.ContentType), provider.Posts.Select(p => p.ContentType));
        Assert.All(provider.Posts, p => Assert.Null(FakeOcrProvider.PagesOf(p)));
        var pages = await PagesAsync(document);
        Assert.Equal([(0, 1, "photo 0"), (1, 1, "photo 1"), (2, 1, "photo 2")],
            pages.Select(p => (p.FilePosition, p.SourcePageNumber, p.Text)));
        Assert.All(pages, p => Assert.Equal(DocumentTextExtractionMethod.Ocr, p.ExtractionMethod));
        Assert.Equal([provider.IdOf(1), provider.IdOf(2), provider.IdOf(3)], provider.DeletedIds.Order());
    }

    [Fact]
    public async Task An_unparsable_pdf_falls_back_to_whole_file_ocr_and_an_unreadable_result_fails_while_the_original_stays_intact()
    {
        await ParkExistingJobsAsync();
        var provider = new FakeOcrProvider { Analyze = (_, _) => FakeOcrProvider.Failed("InvalidRequest", "InvalidContent") };
        var original = TestOriginal.Pdf(5_000, "damaged.pdf");
        var (document, client) = await CaptureAsync(original);
        var url = Assert.Single(document.OriginalUrls());

        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));

        var post = Assert.Single(provider.Posts);
        Assert.Null(FakeOcrProvider.PagesOf(post));
        Assert.Equal(original.Bytes, post.Body);
        var job = await JobAsync(document);
        Assert.Equal((DocumentReadingState.Failed, "unreadable_content"), (job.State, job.FailureCode));
        Assert.Empty(await PagesAsync(document));
        // The failed analysis still had a result ID: its deletion was requested and acknowledged.
        Assert.Equal([provider.IdOf(1)], provider.DeletedIds);
        Assert.Equal(DocumentReadingCleanupState.Acknowledged, Assert.Single(await RunFilesAsync(document)).CleanupState);
        Assert.Equal(original.Bytes, await client.GetOriginalBytesAsync(url));
        AssertLogsCarryNoContent("synthetic provider message", "synthetic inner message", "damaged.pdf");
    }

    [Fact]
    public async Task A_password_protected_pdf_fails_without_reaching_the_provider()
    {
        await ParkExistingJobsAsync();
        var provider = new FakeOcrProvider();
        var original = ReadingFixtures.PdfOriginal(ReadingFixtures.PasswordProtectedPdf(), "locked.pdf");
        var (document, client) = await CaptureAsync(original);

        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));

        Assert.Empty(provider.Requests);
        var job = await JobAsync(document);
        Assert.Equal((DocumentReadingState.Failed, "encrypted_pdf"), (job.State, job.FailureCode));
        Assert.Equal(original.Bytes, await client.GetOriginalBytesAsync(Assert.Single(document.OriginalUrls())));
    }

    [Fact]
    public async Task A_document_without_any_text_fails_as_empty_and_publishes_nothing()
    {
        await ParkExistingJobsAsync();
        var provider = new FakeOcrProvider { Analyze = (_, _) => FakeOcrProvider.Succeeded((1, "")) };
        var (document, _) = await CaptureAsync(SampleOriginals.Png(), SampleOriginals.Jpeg());

        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));

        var job = await JobAsync(document);
        Assert.Equal((DocumentReadingState.Failed, "empty_document"), (job.State, job.FailureCode));
        Assert.Null(job.PublishedRunGeneration);
        Assert.Empty(await PagesAsync(document));
        Assert.Equal([provider.IdOf(1), provider.IdOf(2)], provider.DeletedIds.Order());
    }

    [Fact]
    public async Task Exceeding_the_page_or_character_limit_fails_instead_of_publishing_truncated_text()
    {
        await ParkExistingJobsAsync();
        var tooManyPages = ReadingFixtures.Pdf(
            Enumerable.Range(0, DocumentReadingWorker.MaxPages + 1).Select(_ => PdfPageSpec.Text("p")).ToArray());
        var (pagesDocument, _) = await CaptureAsync(ReadingFixtures.PdfOriginal(tooManyPages));
        var provider = new FakeOcrProvider();

        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));

        Assert.Empty(provider.Requests);
        Assert.Equal("page_limit_exceeded", (await JobAsync(pagesDocument)).FailureCode);
        Assert.Empty(await PagesAsync(pagesDocument));

        var half = new string('x', (int)(DocumentReadingWorker.MaxCharacters / 2));
        var wordy = new FakeOcrProvider { Analyze = (_, index) => FakeOcrProvider.Succeeded((1, index == 1 ? half : half + "y")) };
        var (textDocument, _) = await CaptureAsync(SampleOriginals.Png(), SampleOriginals.Jpeg());

        Assert.True(await Worker(wordy).RunOnceAsync(CancellationToken.None));

        var job = await JobAsync(textDocument);
        Assert.Equal((DocumentReadingState.Failed, "text_limit_exceeded"), (job.State, job.FailureCode));
        Assert.Empty(await PagesAsync(textDocument));
        // Both provider results are cleaned up, including the one whose text was never checkpointed.
        Assert.Equal([wordy.IdOf(1), wordy.IdOf(2)], wordy.DeletedIds.Order());
    }

    [Fact]
    public async Task A_failure_of_one_original_publishes_nothing_even_though_another_was_read()
    {
        await ParkExistingJobsAsync();
        var provider = new FakeOcrProvider
        {
            BeforeSubmit = (_, index) => index == 2
                ? FakeOcrProvider.Error(HttpStatusCode.BadRequest, "InvalidRequest", "InvalidContentDimensions")
                : null,
        };
        var (document, _) = await CaptureAsync(SampleOriginals.Png(), SampleOriginals.Jpeg());

        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));

        var job = await JobAsync(document);
        Assert.Equal((DocumentReadingState.Failed, "unsupported_image_dimensions"), (job.State, job.FailureCode));
        Assert.Empty(await PagesAsync(document));
        Assert.Equal([provider.IdOf(1)], provider.DeletedIds);
        var runFiles = await RunFilesAsync(document);
        Assert.Equal(
            [DocumentReadingRunFileStage.Retrieved, DocumentReadingRunFileStage.Failed], runFiles.Select(r => r.Stage));
    }

    [Fact]
    public async Task Transient_failures_are_retried_on_the_schedule_and_bounded_to_five_attempts()
    {
        await ParkExistingJobsAsync();
        var provider = new FakeOcrProvider
        {
            BeforeSubmit = (_, index) => index == 2
                ? FakeOcrProvider.Error(HttpStatusCode.TooManyRequests, "TooManyRequests", retryAfter: TimeSpan.FromHours(1))
                : FakeOcrProvider.Error(HttpStatusCode.ServiceUnavailable, "ServiceUnavailable"),
        };
        var (document, _) = await CaptureAsync(SampleOriginals.Png());
        var expected = new[]
        {
            TimeSpan.FromSeconds(30), TimeSpan.FromHours(1), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30),
        };

        for (var attempt = 1; attempt <= DocumentReadingWorker.MaxAttempts; attempt++)
        {
            var before = await DatabaseNowAsync();
            Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));
            var job = await JobAsync(document);
            Assert.Equal(attempt, job.AttemptCount);
            if (attempt < DocumentReadingWorker.MaxAttempts)
            {
                Assert.Equal(DocumentReadingState.Queued, job.State);
                var delay = job.DueAt - before;
                Assert.InRange(delay, expected[attempt - 1], expected[attempt - 1] + TimeSpan.FromSeconds(30));
                // Not due yet: nothing is claimed until the delay has passed.
                Assert.False(await Worker(provider).RunOnceAsync(CancellationToken.None));
                await MakeDueAsync(document);
            }
            else
            {
                Assert.Equal((DocumentReadingState.Failed, "provider_unavailable"), (job.State, job.FailureCode));
            }
        }

        Assert.Equal(DocumentReadingWorker.MaxAttempts, provider.Posts.Count());
        Assert.False(await Worker(provider).RunOnceAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_document_that_keeps_crashing_the_process_fails_once_its_attempts_are_used_up()
    {
        await ParkExistingJobsAsync();
        var provider = new FakeOcrProvider { RunningPolls = int.MaxValue };
        var (document, _) = await CaptureAsync(SampleOriginals.Png());
        // Each crash leaves the job Reading with a lease that later expires; a reclaim counts as a new attempt.
        await UseDatabaseAsync(db => db.Database.ExecuteSqlAsync($"""
            UPDATE document_reading_jobs
            SET state = 'reading', lease_token = gen_random_uuid(), lease_expires_at = now() - interval '1 second',
                attempt_count = {DocumentReadingWorker.MaxAttempts}
            WHERE document_id = {IdOf(document)}
            """));

        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));

        var job = await JobAsync(document);
        Assert.Equal((DocumentReadingState.Failed, "attempts_exhausted", DocumentReadingWorker.MaxAttempts + 1),
            (job.State, job.FailureCode, job.AttemptCount));
        Assert.Empty(provider.Requests);
        Assert.False(await Worker(provider).RunOnceAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_retry_after_a_failed_submission_reuses_the_saved_local_parse_of_a_mixed_pdf()
    {
        await ParkExistingJobsAsync();
        var provider = new FakeOcrProvider
        {
            BeforeSubmit = (_, index) => index == 1 ? FakeOcrProvider.Error(HttpStatusCode.ServiceUnavailable, "ServiceUnavailable") : null,
            Analyze = (_, _) => FakeOcrProvider.Succeeded((2, "Scanned line")),
        };
        var pdf = ReadingFixtures.Pdf(PdfPageSpec.Text("Typed page one"), PdfPageSpec.Scan());
        var (document, _) = await CaptureAsync(ReadingFixtures.PdfOriginal(pdf));

        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));
        Assert.Equal(DocumentReadingState.Queued, (await JobAsync(document)).State);
        var pending = Assert.Single(await RunFilesAsync(document));
        Assert.Equal(DocumentReadingRunFileStage.Pending, pending.Stage);
        Assert.NotNull(pending.FetchedText);
        Assert.DoesNotContain(_logs.Entries, e => e.Contains("Reusing the saved local parse"));

        await MakeDueAsync(document);
        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));

        Assert.Contains(_logs.Entries, e => e.Contains("Reusing the saved local parse"));
        Assert.Equal(["2", "2"], provider.Posts.Select(FakeOcrProvider.PagesOf));
        Assert.Equal(DocumentReadingState.Read, (await JobAsync(document)).State);
        var pages = await PagesAsync(document);
        Assert.Equal([DocumentTextExtractionMethod.TextLayer, DocumentTextExtractionMethod.Ocr], pages.Select(p => p.ExtractionMethod));
        Assert.Contains("Typed page one", pages[0].Text);
        Assert.Equal("Scanned line", pages[1].Text);
        Assert.Equal(
            [DocumentReadingRunFileStage.Failed, DocumentReadingRunFileStage.Retrieved],
            (await RunFilesAsync(document)).Select(r => r.Stage));
    }

    [Fact]
    public async Task A_retry_of_an_unparsable_pdf_goes_straight_to_whole_file_ocr()
    {
        await ParkExistingJobsAsync();
        var provider = new FakeOcrProvider
        {
            BeforeSubmit = (_, index) => index == 1 ? FakeOcrProvider.Error(HttpStatusCode.ServiceUnavailable, "ServiceUnavailable") : null,
            Analyze = (_, _) => FakeOcrProvider.Succeeded((1, "Recovered by OCR")),
        };
        var (document, _) = await CaptureAsync(TestOriginal.Pdf(5_000, "damaged.pdf"));

        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));
        await MakeDueAsync(document);
        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));

        Assert.Single(_logs.Entries, e => e.Contains("The local parser could not read file"));
        Assert.Contains(_logs.Entries, e => e.Contains("Reusing the saved local parse"));
        Assert.All(provider.Posts, p => Assert.Null(FakeOcrProvider.PagesOf(p)));
        Assert.Equal(["Recovered by OCR"], (await PagesAsync(document)).Select(p => p.Text));
    }

    [Fact]
    public async Task A_storage_outage_while_reading_the_original_is_transient()
    {
        await ParkExistingJobsAsync();
        var provider = new FakeOcrProvider();
        var (document, client) = await CaptureAsync(SampleOriginals.Png());
        var storage = new FaultyOriginalStorage(factory.Services.GetRequiredService<IOriginalStorage>()) { Unavailable = true };

        Assert.True(await Worker(provider, storage).RunOnceAsync(CancellationToken.None));

        var job = await JobAsync(document);
        Assert.Equal(DocumentReadingState.Queued, job.State);
        Assert.Empty(provider.Requests);
        Assert.Equal(SampleOriginals.Png().Bytes, await client.GetOriginalBytesAsync(Assert.Single(document.OriginalUrls())));

        await MakeDueAsync(document);
        storage.Unavailable = false;
        Assert.True(await Worker(provider, storage).RunOnceAsync(CancellationToken.None));
        Assert.Equal(DocumentReadingState.Read, (await JobAsync(document)).State);
    }

    [Fact]
    public async Task A_rejected_identity_fails_with_an_explicit_code_and_a_warning_instead_of_retrying()
    {
        await ParkExistingJobsAsync();
        var provider = new FakeOcrProvider
        {
            BeforeSubmit = (_, _) => FakeOcrProvider.Error(HttpStatusCode.Unauthorized, "Unauthorized"),
        };
        var (document, _) = await CaptureAsync(SampleOriginals.Png());

        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));

        var job = await JobAsync(document);
        Assert.Equal((DocumentReadingState.Failed, "provider_auth"), (job.State, job.FailureCode));
        Assert.Single(provider.Posts);
        Assert.Contains(_logs.Entries, e => e.StartsWith("Warning:", StringComparison.Ordinal) && e.Contains("provider_auth"));
    }

    [Fact]
    public async Task After_a_restart_following_submission_the_saved_operation_is_polled_without_a_second_submission()
    {
        await ParkExistingJobsAsync();
        using var shutdown = new CancellationTokenSource();
        var provider = new FakeOcrProvider
        {
            Analyze = (_, _) => FakeOcrProvider.Succeeded((1, "Resumed text")),
            // The process stops while the analysis is running.
            BeforePoll = async _ =>
            {
                await shutdown.CancelAsync();
                return FakeOcrProvider.Json(HttpStatusCode.OK, """{"status":"running"}""");
            },
        };
        var (document, _) = await CaptureAsync(SampleOriginals.Png());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Worker(provider).RunOnceAsync(shutdown.Token));

        var runFile = Assert.Single(await RunFilesAsync(document));
        Assert.Equal((DocumentReadingRunFileStage.Submitted, provider.IdOf(1)), (runFile.Stage, runFile.ProviderOperationId));
        var requeued = await JobAsync(document);
        // A graceful shutdown hands the attempt back: platform interruptions do not use up the retry budget.
        Assert.Equal((DocumentReadingState.Queued, 0), (requeued.State, requeued.AttemptCount));
        Assert.Empty(provider.DeletedIds);

        provider.BeforePoll = null;
        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));

        Assert.Single(provider.Posts);
        Assert.Equal(DocumentReadingState.Read, (await JobAsync(document)).State);
        Assert.Equal(["Resumed text"], (await PagesAsync(document)).Select(p => p.Text));
        Assert.Equal([provider.IdOf(1)], provider.DeletedIds);
    }

    [Fact]
    public async Task After_a_crash_following_retrieval_checkpointed_output_is_not_fetched_again()
    {
        await ParkExistingJobsAsync();
        var provider = new FakeOcrProvider
        {
            Analyze = (_, index) => FakeOcrProvider.Succeeded((1, $"photo {index}")),
            // The attempt breaks down while polling the second photo's saved operation.
            BeforePoll = id => id.StartsWith("op-2-", StringComparison.Ordinal) ? throw new InvalidOperationException("Injected crash") : Task.FromResult<HttpResponseMessage?>(null),
        };
        var (document, _) = await CaptureAsync(SampleOriginals.Png(), SampleOriginals.Jpeg());

        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));
        // An unexpected failure is transient: the job is queued again with both run files kept.
        Assert.Equal(DocumentReadingState.Queued, (await JobAsync(document)).State);
        Assert.Equal(
            [DocumentReadingRunFileStage.Retrieved, DocumentReadingRunFileStage.Submitted],
            (await RunFilesAsync(document)).Select(r => r.Stage));
        var requestsBefore = provider.Requests.Count;

        provider.BeforePoll = null;
        await MakeDueAsync(document);
        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));

        var resumed = provider.Requests.Skip(requestsBefore).ToList();
        Assert.DoesNotContain(resumed, r => r.Method == HttpMethod.Post);
        Assert.DoesNotContain(resumed, r => r.Uri.AbsolutePath.EndsWith("/" + provider.IdOf(1), StringComparison.Ordinal) && r.Method == HttpMethod.Get);
        Assert.Equal(["photo 1", "photo 2"], (await PagesAsync(document)).Select(p => p.Text));
        Assert.Equal(DocumentReadingState.Read, (await JobAsync(document)).State);
    }

    [Fact]
    public async Task A_lease_lost_during_submission_stops_the_attempt_and_keeps_the_operation_for_cleanup()
    {
        await ParkExistingJobsAsync();
        var (document, _) = await CaptureAsync(SampleOriginals.Png());
        var provider = new FakeOcrProvider();
        provider.BeforeSubmit = (_, _) =>
        {
            // Another worker's view: this worker's lease has expired meanwhile.
            ExpireLeaseAsync(document).GetAwaiter().GetResult();
            return null;
        };

        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));

        Assert.Empty(provider.Polls);
        Assert.Empty(await PagesAsync(document));
        var job = await JobAsync(document);
        Assert.Equal(DocumentReadingState.Reading, job.State);
        Assert.Null(job.PublishedRunGeneration);
        var runFile = Assert.Single(await RunFilesAsync(document));
        Assert.Equal((DocumentReadingRunFileStage.Failed, provider.IdOf(1)), (runFile.Stage, runFile.ProviderOperationId));
        Assert.Equal([provider.IdOf(1)], provider.DeletedIds);
        Assert.Contains(_logs.Entries, e => e.Contains("Lost the lease"));
    }

    [Fact]
    public async Task A_refused_heartbeat_cancels_a_running_analysis_before_publication()
    {
        await ParkExistingJobsAsync();
        var (document, _) = await CaptureAsync(SampleOriginals.Png());
        var provider = new FakeOcrProvider { RunningPolls = int.MaxValue };
        var expired = 0;
        provider.BeforePoll = async _ =>
        {
            if (Interlocked.Exchange(ref expired, 1) == 0)
            {
                await ExpireLeaseAsync(document);
            }

            return null;
        };
        var worker = Worker(provider, time: TimeProvider.System, analysisPollInterval: TimeSpan.FromMilliseconds(20));

        var run = worker.RunOnceAsync(CancellationToken.None);
        Assert.True(await run.WaitAsync(TimeSpan.FromSeconds(30)));

        Assert.Empty(await PagesAsync(document));
        Assert.Equal(DocumentReadingState.Reading, (await JobAsync(document)).State);
        Assert.Contains(_logs.Entries, e => e.Contains("Lost the lease"));
    }

    private DocumentReadingWorker Worker(
        FakeOcrProvider provider, IOriginalStorage? storage = null, TimeProvider? time = null,
        TimeSpan? analysisPollInterval = null)
    {
        var reader = new AzureDocumentTextReader(
            new HttpClient(provider), new FakeTokenCredential(),
            new DocumentReadingOptions { Auth = DocumentReadingAuthMode.AzureManagedIdentity, Endpoint = new Uri(FakeOcrProvider.Endpoint) });
        var loggers = LoggerFactory.Create(builder => builder.AddProvider(_logs).SetMinimumLevel(LogLevel.Information));
        var scopes = factory.Services.GetRequiredService<IServiceScopeFactory>();
        return new DocumentReadingWorker(
            scopes,
            DocumentReadingAvailability.Resolve(new DocumentReadingOptions(), isDevelopment: false),
            reader,
            new PdfTextReader(),
            new DocumentReadingCleanup(scopes, reader, loggers.CreateLogger<DocumentReadingCleanup>()),
            storage ?? factory.Services.GetRequiredService<IOriginalStorage>(),
            time ?? new FixedTimeProvider(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero)),
            loggers.CreateLogger<DocumentReadingWorker>())
        {
            AnalysisPollInterval = analysisPollInterval ?? TimeSpan.Zero,
            HeartbeatInterval = TimeSpan.FromMilliseconds(200),
        };
    }

    private async Task<(JsonElement Document, HttpClient Client)> CaptureAsync(params TestOriginal[] originals)
    {
        var client = await factory.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        return (await client.CaptureAsync(animalId, originals), client);
    }

    private void AssertLogsCarryNoContent(params string[] fragments)
    {
        foreach (var fragment in fragments)
        {
            Assert.DoesNotContain(_logs.Entries, e => e.Contains(fragment, StringComparison.Ordinal));
        }
    }

    private static Guid IdOf(JsonElement document) => document.GetProperty("id").GetGuid();

    private Task<DocumentReadingJob> JobAsync(JsonElement document) =>
        UseDatabaseAsync(db => db.DocumentReadingJobs.AsNoTracking().SingleAsync(j => j.DocumentId == IdOf(document)));

    private Task<List<DocumentTextPage>> PagesAsync(JsonElement document) =>
        UseDatabaseAsync(db => db.DocumentTextPages
            .AsNoTracking()
            .Where(p => p.DocumentId == IdOf(document))
            .OrderBy(p => p.FilePosition)
            .ThenBy(p => p.SourcePageNumber)
            .ToListAsync());

    private Task<List<DocumentReadingRunFile>> RunFilesAsync(JsonElement document) =>
        UseDatabaseAsync(db => db.DocumentReadingRunFiles
            .AsNoTracking()
            .Where(r => r.DocumentId == IdOf(document))
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .ToListAsync());

    private Task<DateTimeOffset> DatabaseNowAsync() =>
        UseDatabaseAsync(db => db.Database.SqlQuery<DateTimeOffset>($"SELECT now() AS \"Value\"").SingleAsync());

    // Jobs of earlier tests in this class would otherwise compete for claims.
    private Task ParkExistingJobsAsync() => UseDatabaseAsync(db => db.Database.ExecuteSqlRawAsync("""
        UPDATE document_reading_jobs
        SET state = 'queued', lease_token = NULL, lease_expires_at = NULL, due_at = now() + interval '100 years'
        WHERE state IN ('queued', 'reading')
        """));

    private Task MakeDueAsync(JsonElement document) => UseDatabaseAsync(db => db.Database.ExecuteSqlAsync(
        $"UPDATE document_reading_jobs SET due_at = now() - interval '1 second' WHERE document_id = {IdOf(document)}"));

    private Task ExpireLeaseAsync(JsonElement document) => UseDatabaseAsync(db => db.Database.ExecuteSqlAsync(
        $"UPDATE document_reading_jobs SET lease_expires_at = now() - interval '1 second' WHERE document_id = {IdOf(document)}"));

    private async Task<T> UseDatabaseAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
