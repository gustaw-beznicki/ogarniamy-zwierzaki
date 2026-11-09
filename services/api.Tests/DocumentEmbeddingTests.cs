using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using ogarniamy_zwierzaki_api.Animals;
using ogarniamy_zwierzaki_api.Data;
using ogarniamy_zwierzaki_api.DocumentEmbedding;
using ogarniamy_zwierzaki_api.DocumentReading;
using ogarniamy_zwierzaki_api.Documents;
using Pgvector.EntityFrameworkCore;

namespace ogarniamy_zwierzaki_api.Tests;

// The embedding pipeline: the chunker, the enqueue points (reading publication and rename), the worker driven one job
// at a time (RunOnceAsync) against the real pgvector database with a fake provider, stale-work fencing, retries, and the
// backfill migration. The autonomous loop never runs in tests (ApiFactory pins it off). Synthetic text only.
public sealed class DocumentEmbeddingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string PreviousMigration = "20261005194436_DocumentReading";

    private static readonly DateOnly EventDate = new(2026, 9, 1);

    private readonly CapturedLogs _logs = new();

    // ---- Chunker -------------------------------------------------------------------------------------------------

    [Fact]
    public void A_short_page_is_one_chunk_with_offsets_that_slice_the_page_text()
    {
        var fileId = Guid.NewGuid();
        var page = Page(fileId, 0, 1, "\n  Synthetic short note about a visit.  \n");

        var chunk = Assert.Single(PageChunker.Chunk([page]));

        Assert.Equal("Synthetic short note about a visit.", chunk.Text);
        Assert.Equal(page.Text.IndexOf('S'), chunk.StartOffset);
        Assert.Equal(page.Text[chunk.StartOffset..chunk.EndOffset], chunk.Text);
        Assert.Equal((fileId, 0, 1), (chunk.DocumentFileId, chunk.FilePosition, chunk.SourcePageNumber));
    }

    [Fact]
    public void Blank_pages_give_no_chunks()
    {
        var fileId = Guid.NewGuid();

        Assert.Empty(PageChunker.Chunk([Page(fileId, 0, 1, ""), Page(fileId, 0, 2, " \n\t ")]));
        Assert.Empty(PageChunker.Chunk([]));
    }

    [Fact]
    public void A_long_page_is_cut_on_line_boundaries_with_about_fifteen_percent_overlap()
    {
        var text = string.Join('\n', Enumerable.Range(1, 120).Select(i => $"Line {i:000}: synthetic observation of a calm patient."));
        var page = Page(Guid.NewGuid(), 0, 1, text);

        var chunks = PageChunker.Chunk([page]);

        Assert.True(chunks.Count >= 4);
        Assert.All(chunks, c =>
        {
            Assert.True(c.Text.Length <= PageChunker.TargetLength);
            Assert.Equal(text[c.StartOffset..c.EndOffset], c.Text);
        });
        for (var i = 0; i < chunks.Count; i++)
        {
            // Every chunk starts at a line start and every chunk but the last ends at a line end.
            Assert.True(chunks[i].StartOffset == 0 || text[chunks[i].StartOffset - 1] == '\n');
            if (i < chunks.Count - 1)
            {
                Assert.Equal('\n', text[chunks[i].EndOffset]);
                var overlap = chunks[i].EndOffset - chunks[i + 1].StartOffset;
                Assert.InRange(overlap, 100, 260);
                Assert.True(chunks[i + 1].StartOffset > chunks[i].StartOffset);
            }
        }

        Assert.Equal(0, chunks[0].StartOffset);
        Assert.Equal(text.Length, chunks[^1].EndOffset);
    }

    [Fact]
    public void Prose_without_line_breaks_is_cut_on_whitespace_and_a_line_without_any_is_cut_hard()
    {
        var prose = string.Join(' ', Enumerable.Repeat("synthetic", 400));
        var proseChunks = PageChunker.Chunk([Page(Guid.NewGuid(), 0, 1, prose)]);

        Assert.True(proseChunks.Count > 2);
        Assert.All(proseChunks, c =>
        {
            Assert.True(c.Text.Length <= PageChunker.TargetLength);
            Assert.Equal("synthetic", c.Text.Split(' ')[0]);
            Assert.Equal("synthetic", c.Text.Split(' ')[^1]);
        });

        var solid = new string('x', 3_000);
        var solidChunks = PageChunker.Chunk([Page(Guid.NewGuid(), 0, 1, solid)]);

        Assert.True(solidChunks.Count >= 3);
        Assert.All(solidChunks, c => Assert.True(c.Text.Length <= PageChunker.TargetLength));
        Assert.Equal(solid.Length, solidChunks[^1].EndOffset);
        Assert.True(solidChunks.Zip(solidChunks.Skip(1)).All(p => p.Second.StartOffset < p.First.EndOffset));
    }

    [Fact]
    public void Chunks_never_cross_pages_and_are_ordered_by_file_page_and_offset()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var longText = string.Join('\n', Enumerable.Range(1, 60).Select(i => $"Row {i:00} of a synthetic table of values."));
        var pages = new[]
        {
            Page(second, 1, 1, "Second original, first page."),
            Page(first, 0, 2, longText),
            Page(first, 0, 1, "First original, first page."),
        };

        var chunks = PageChunker.Chunk(pages);

        Assert.Equal(
            chunks.OrderBy(c => c.FilePosition).ThenBy(c => c.SourcePageNumber).ThenBy(c => c.StartOffset),
            chunks);
        Assert.Equal("First original, first page.", chunks[0].Text);
        Assert.Equal("Second original, first page.", chunks[^1].Text);
        Assert.All(chunks, c =>
        {
            var source = pages.Single(p => p.DocumentFileId == c.DocumentFileId && p.SourcePageNumber == c.SourcePageNumber);
            Assert.Equal(source.Text[c.StartOffset..c.EndOffset], c.Text);
        });
        Assert.Equal(chunks, PageChunker.Chunk(pages));
    }

    // ---- Enqueue points ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Publishing_reading_queues_exactly_one_embedding_job_also_when_published_again()
    {
        await ParkAllJobsAsync();
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);

        var documentId = await ReadAsync(userId, animalId, "Synthetic first revision.");

        var job = Assert.Single(await JobsAsync(documentId));
        Assert.Equal(DocumentEmbeddingState.Queued, job.State);
        Assert.Equal(0, job.AttemptCount);
        Assert.Null(job.EmbeddedRunGeneration);

        Assert.True(await Worker(new FakeEmbeddingProvider()).RunOnceAsync(CancellationToken.None));
        Assert.Equal(DocumentEmbeddingState.Embedded, (await JobAsync(documentId)).State);

        // An owner Retry re-publishes: the same single job is queued again, keeping the previous result until replaced.
        await RepublishAsync(documentId, "Synthetic second revision.");
        var again = Assert.Single(await JobsAsync(documentId));
        Assert.Equal(DocumentEmbeddingState.Queued, again.State);
        Assert.Equal(0, again.AttemptCount);
        Assert.Equal(1, again.EmbeddedRunGeneration);

        // Publishing the same revision again (a replayed enqueue) still leaves one queued job.
        await EnqueueAsync(documentId);
        await EnqueueAsync(documentId);
        Assert.Equal(DocumentEmbeddingState.Queued, Assert.Single(await JobsAsync(documentId)).State);
    }

    [Fact]
    public async Task A_document_that_is_not_read_has_no_embedding_job()
    {
        await ParkAllJobsAsync();
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);

        var document = await factory.AddStoredAsync(userId, animalId, EventDate);

        Assert.Empty(await JobsAsync(document.Id));
    }

    [Fact]
    public async Task Renaming_requeues_only_that_animals_documents_with_published_text()
    {
        await ParkAllJobsAsync();
        var userId = await factory.CreateUserAsync();
        var renamed = await factory.AddAnimalAsync(userId, "Czarek");
        var other = await factory.AddAnimalAsync(userId, "Burek");
        var first = await ReadAsync(userId, renamed, "Synthetic note one.");
        var second = await ReadAsync(userId, renamed, "Synthetic note two.");
        var untouched = await ReadAsync(userId, other, "Synthetic note three.");
        await EmbedAllAsync(new FakeEmbeddingProvider());
        // Stored but not read yet: nothing to embed, so a rename must not invent a job for it.
        var unread = (await factory.AddStoredAsync(userId, renamed, EventDate)).Id;
        // Being read again (a Retry): its published text is still searchable, so the rename must re-embed it too.
        await ExecuteAsync($"""
            UPDATE document_reading_jobs
            SET state = 'queued', run_generation = run_generation + 1, completed_at = NULL,
                due_at = now() + interval '100 years'
            WHERE document_id = '{second}'
            """);

        // A rename to the same name changes nothing and queues nothing.
        await RenameAsync(userId, renamed, "Czarek");
        Assert.All(await JobsAsync(first, second, untouched), j => Assert.Equal(DocumentEmbeddingState.Embedded, j.State));

        await RenameAsync(userId, renamed, "Pimpek");

        Assert.All(await JobsAsync(first, second), j =>
        {
            Assert.Equal(DocumentEmbeddingState.Queued, j.State);
            Assert.Equal("Czarek", j.EmbeddedAnimalName);
        });
        Assert.Equal(DocumentEmbeddingState.Embedded, Assert.Single(await JobsAsync(untouched)).State);
        Assert.Empty(await JobsAsync(unread));

        // Re-embedding uses the new name and replaces the stored result.
        var provider = new FakeEmbeddingProvider();
        await EmbedAllAsync(provider);
        Assert.All(provider.Calls.SelectMany(c => c), input => Assert.StartsWith("Pimpek\n\n", input));
        Assert.All(await JobsAsync(first, second), j =>
        {
            Assert.Equal(DocumentEmbeddingState.Embedded, j.State);
            Assert.Equal("Pimpek", j.EmbeddedAnimalName);
        });
    }

    // ---- Worker --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_worker_stores_chunks_with_vectors_provenance_and_version()
    {
        await ParkAllJobsAsync();
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId, "Czarek");
        var longPage = string.Join('\n', Enumerable.Range(1, 80).Select(i => $"Finding {i:00}: synthetic heart rhythm observation."));
        var documentId = await ReadAsync(userId, animalId, "Synthetic cover page.", longPage);
        var provider = new FakeEmbeddingProvider();

        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));

        var job = await JobAsync(documentId);
        Assert.Equal((DocumentEmbeddingState.Embedded, 1, "Czarek"), (job.State, job.EmbeddedRunGeneration, job.EmbeddedAnimalName));
        Assert.Null(job.LeaseToken);
        var chunks = await ChunksAsync(documentId);
        Assert.True(chunks.Count >= 3);
        Assert.Equal(Enumerable.Range(0, chunks.Count), chunks.Select(c => c.ChunkIndex));
        Assert.Equal("Synthetic cover page.", chunks[0].Text);
        Assert.Equal(1, chunks[0].SourcePageNumber);
        Assert.All(chunks.Skip(1), c => Assert.Equal(2, c.SourcePageNumber));
        Assert.All(chunks, c =>
        {
            Assert.Equal(1, c.RunGeneration);
            Assert.Equal(EmbeddingOptions.StoredDimensions, c.Embedding.ToArray().Length);
            Assert.Equal(EmbeddingInput.Version("text-embedding-3-large", 1536), c.EmbeddingVersion);
        });

        // One provider call, with the animal's name in front of every chunk.
        var inputs = Assert.Single(provider.Calls);
        Assert.Equal(chunks.Select(c => $"Czarek\n\n{c.Text}"), inputs);
        Assert.Equal(FakeEmbeddingProvider.Embed(inputs[0]), chunks[0].Embedding.ToArray());

        // The stored vectors are queryable with pgvector's cosine distance.
        var query = new Pgvector.Vector(FakeEmbeddingProvider.Embed("Czarek synthetic cover page"));
        var nearest = await UseDatabaseAsync(db => db.DocumentChunks
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.Embedding.CosineDistance(query))
            .Select(c => c.ChunkIndex)
            .FirstAsync());
        Assert.Equal(0, nearest);

        Assert.False(await Worker(provider).RunOnceAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_paced_document_gets_more_time_than_one_attempt_deadline()
    {
        await ParkAllJobsAsync();
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId, "Czarek");
        var documentId = await ReadAsync(userId, animalId, "Synthetic pacing page with a short line of text.");
        // A drained bucket of 600 tokens per minute: this document's 29 estimated tokens wait about three seconds, longer
        // than the one-second attempt deadline.
        var pacer = new EmbeddingTokenPacer(600, TimeProvider.System);
        pacer.Reserve(600);
        var provider = new FakeEmbeddingProvider();

        Assert.True(await Worker(provider, pacer, TimeSpan.FromSeconds(1)).RunOnceAsync(CancellationToken.None));

        var job = await JobAsync(documentId);
        Assert.Equal((DocumentEmbeddingState.Embedded, 1, (string?)null), (job.State, job.AttemptCount, job.FailureCode));
        Assert.Single(provider.Calls);
    }

    [Fact]
    public async Task The_documents_of_an_inactive_animal_are_embedded_like_any_other()
    {
        await ParkAllJobsAsync();
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);
        var documentId = await ReadAsync(userId, animalId, "Synthetic note of a retired patient.");
        await UseScopeAsync(async services =>
        {
            var animals = services.GetRequiredService<OwnedAnimals>();
            var animal = await animals.FindAsync(userId, animalId);
            await animals.SetActivityAsync(userId, animalId, animal!.Version, false);
        });

        await Worker(new FakeEmbeddingProvider()).RunOnceAsync(CancellationToken.None);

        Assert.Equal(DocumentEmbeddingState.Embedded, (await JobAsync(documentId)).State);
        Assert.NotEmpty(await ChunksAsync(documentId));
    }

    [Fact]
    public async Task A_result_computed_for_a_stale_reading_generation_is_discarded_and_the_job_requeued()
    {
        await ParkAllJobsAsync();
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);
        var documentId = await ReadAsync(userId, animalId, "Synthetic first revision.");
        // The revision changes behind the worker's back, without an enqueue, so only the fence under the row lock can
        // notice.
        var provider = new FakeEmbeddingProvider
        {
            OnEmbed = (_, _) => ExecuteAsync($"UPDATE document_reading_jobs SET run_generation = 2, published_run_generation = 2 WHERE document_id = '{documentId}'"),
        };

        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));

        var job = await JobAsync(documentId);
        Assert.Equal(DocumentEmbeddingState.Queued, job.State);
        Assert.Equal(0, job.AttemptCount);
        Assert.Null(job.LeaseToken);
        Assert.Null(job.EmbeddedRunGeneration);
        Assert.Empty(await ChunksAsync(documentId));
        Assert.Contains(_logs.Entries, e => e.Contains("discarded"));
    }

    [Fact]
    public async Task A_result_computed_for_a_stale_animal_name_is_discarded_and_the_next_run_uses_the_new_name()
    {
        await ParkAllJobsAsync();
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId, "Czarek");
        var documentId = await ReadAsync(userId, animalId, "Synthetic first revision.");
        var provider = new FakeEmbeddingProvider
        {
            OnEmbed = (_, index) => index == 1
                ? ExecuteAsync($"UPDATE animals SET name = 'Burek' WHERE id = '{animalId}'")
                : Task.CompletedTask,
        };

        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));

        Assert.Equal(DocumentEmbeddingState.Queued, (await JobAsync(documentId)).State);
        Assert.Empty(await ChunksAsync(documentId));

        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));

        var job = await JobAsync(documentId);
        Assert.Equal((DocumentEmbeddingState.Embedded, "Burek"), (job.State, job.EmbeddedAnimalName));
        Assert.Equal("Burek\n\nSynthetic first revision.", provider.Calls[1][0]);
        Assert.Single(await ChunksAsync(documentId));
    }

    [Fact]
    public async Task A_reading_published_while_embedding_fences_the_old_run_through_its_lease()
    {
        await ParkAllJobsAsync();
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);
        var documentId = await ReadAsync(userId, animalId, "Synthetic first revision.");
        var provider = new FakeEmbeddingProvider
        {
            OnEmbed = (_, _) => RepublishAsync(documentId, "Synthetic second revision."),
        };

        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));

        // The enqueue of the second publication reset the job, so the first run wrote nothing.
        var job = await JobAsync(documentId);
        Assert.Equal(DocumentEmbeddingState.Queued, job.State);
        Assert.Empty(await ChunksAsync(documentId));
        Assert.Contains(_logs.Entries, e => e.Contains("Lost the lease"));

        provider.OnEmbed = null;
        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));
        Assert.Equal(2, (await JobAsync(documentId)).EmbeddedRunGeneration);
        Assert.Equal("Synthetic second revision.", Assert.Single(await ChunksAsync(documentId)).Text);
    }

    [Fact]
    public async Task A_transient_failure_retries_with_backoff_and_succeeds_later()
    {
        await ParkAllJobsAsync();
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);
        var documentId = await ReadAsync(userId, animalId, "Synthetic note.");
        var provider = new FakeEmbeddingProvider
        {
            OnEmbed = (_, _) => throw EmbeddingFailure.Transient(EmbeddingFailure.ProviderUnavailable, TimeSpan.FromHours(1)),
        };

        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));

        var job = await JobAsync(documentId);
        Assert.Equal((DocumentEmbeddingState.Queued, 1), (job.State, job.AttemptCount));
        Assert.Null(job.FailureCode);
        // A longer Retry-After wins over the 30-second schedule.
        Assert.True(job.DueAt > DateTimeOffset.UtcNow.AddMinutes(50));
        Assert.False(await Worker(provider).RunOnceAsync(CancellationToken.None));

        provider.OnEmbed = null;
        await MakeDueAsync(documentId);
        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));
        var retried = await JobAsync(documentId);
        Assert.Equal((DocumentEmbeddingState.Embedded, 2), (retried.State, retried.AttemptCount));
        Assert.Single(await ChunksAsync(documentId));
    }

    [Fact]
    public async Task Transient_failures_stop_after_the_fifth_attempt()
    {
        await ParkAllJobsAsync();
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);
        var documentId = await ReadAsync(userId, animalId, "Synthetic note.");
        var provider = new FakeEmbeddingProvider
        {
            OnEmbed = (_, _) => throw EmbeddingFailure.Transient(EmbeddingFailure.ProviderUnavailable),
        };

        for (var attempt = 1; attempt < DocumentEmbeddingWorker.MaxAttempts; attempt++)
        {
            Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));
            var queued = await JobAsync(documentId);
            Assert.Equal((DocumentEmbeddingState.Queued, attempt), (queued.State, queued.AttemptCount));
            await MakeDueAsync(documentId);
        }

        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));

        var job = await JobAsync(documentId);
        Assert.Equal(DocumentEmbeddingState.Failed, job.State);
        Assert.Equal(EmbeddingFailure.ProviderUnavailable, job.FailureCode);
        Assert.False(await Worker(provider).RunOnceAsync(CancellationToken.None));
        Assert.Equal(DocumentEmbeddingWorker.MaxAttempts, provider.Calls.Count);
    }

    [Fact]
    public async Task A_terminal_failure_stops_at_once_keeps_the_original_and_is_requeued_only_by_a_new_enqueue()
    {
        await ParkAllJobsAsync();
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);
        var documentId = await ReadAsync(userId, animalId, "Synthetic note.");
        var provider = new FakeEmbeddingProvider
        {
            OnEmbed = (_, _) => throw EmbeddingFailure.Terminal(EmbeddingFailure.ProviderRejected),
        };

        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));

        var job = await JobAsync(documentId);
        Assert.Equal((DocumentEmbeddingState.Failed, EmbeddingFailure.ProviderRejected), (job.State, job.FailureCode));
        Assert.Empty(await ChunksAsync(documentId));
        Assert.False(await Worker(provider).RunOnceAsync(CancellationToken.None));
        Assert.Single(provider.Calls);

        // Embedding trouble never touches reading: the stored text and the document stay as they were.
        var reading = await UseDatabaseAsync(db => db.DocumentReadingJobs.AsNoTracking().SingleAsync(j => j.DocumentId == documentId));
        Assert.Equal(DocumentReadingState.Read, reading.State);
        Assert.Single(await UseDatabaseAsync(db => db.DocumentTextPages.AsNoTracking().Where(p => p.DocumentId == documentId).ToListAsync()));

        await EnqueueAsync(documentId);
        var requeued = await JobAsync(documentId);
        Assert.Equal(DocumentEmbeddingState.Queued, requeued.State);
        Assert.Null(requeued.FailureCode);
    }

    [Fact]
    public async Task A_configuration_failure_is_retried_and_then_fails_with_an_explicit_code()
    {
        await ParkAllJobsAsync();
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);
        var documentId = await ReadAsync(userId, animalId, "Synthetic note.");
        var provider = new FakeEmbeddingProvider
        {
            OnEmbed = (_, _) => throw EmbeddingFailure.Configuration(EmbeddingFailure.ProviderAuth),
        };

        for (var attempt = 1; attempt < DocumentEmbeddingWorker.MaxAttempts; attempt++)
        {
            Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));
            var queued = await JobAsync(documentId);
            Assert.Equal((DocumentEmbeddingState.Queued, attempt), (queued.State, queued.AttemptCount));
            await MakeDueAsync(documentId);
        }

        Assert.True(await Worker(provider).RunOnceAsync(CancellationToken.None));

        var job = await JobAsync(documentId);
        Assert.Equal((DocumentEmbeddingState.Failed, EmbeddingFailure.ProviderAuth), (job.State, job.FailureCode));
        Assert.Contains(_logs.Entries, e => e.Contains(EmbeddingFailure.ProviderAuth) && e.StartsWith("Warning"));
    }

    [Fact]
    public async Task Competing_claims_never_hand_out_one_job_twice()
    {
        await ParkAllJobsAsync();
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId);
        var first = await ReadAsync(userId, animalId, "Synthetic note one.");
        var second = await ReadAsync(userId, animalId, "Synthetic note two.");

        var concurrent = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            UseJobsAsync(jobs => jobs.ClaimNextAsync(TimeSpan.FromMinutes(5)))));

        var claims = concurrent.OfType<DocumentEmbeddingClaim>().ToList();
        Assert.Equal(new[] { first, second }.Order(), claims.Select(c => c.DocumentId).Order());
        Assert.Equal(2, claims.Select(c => c.LeaseToken).Distinct().Count());
        Assert.All(claims, c => Assert.Equal(1, c.AttemptCount));
    }

    [Fact]
    public async Task Logs_carry_ids_and_codes_but_never_text()
    {
        await ParkAllJobsAsync();
        var userId = await factory.CreateUserAsync();
        var animalId = await factory.AddAnimalAsync(userId, "Zorrobelle");
        var succeeding = await ReadAsync(userId, animalId, "Synthetic cardiomyopathy findings, quokka edition.");
        var failing = await ReadAsync(userId, animalId, "Synthetic hepatitis findings, wombat edition.");
        var transient = await ReadAsync(userId, animalId, "Synthetic nephritis findings, numbat edition.");
        var provider = new FakeEmbeddingProvider
        {
            OnEmbed = (inputs, _) => inputs[0].Contains("wombat")
                ? throw EmbeddingFailure.Terminal(EmbeddingFailure.ProviderRejected)
                : inputs[0].Contains("numbat")
                    ? throw EmbeddingFailure.Transient(EmbeddingFailure.ProviderUnavailable)
                    : Task.CompletedTask,
        };

        await EmbedAllAsync(provider);

        Assert.Equal(DocumentEmbeddingState.Embedded, (await JobAsync(succeeding)).State);
        Assert.Equal(DocumentEmbeddingState.Failed, (await JobAsync(failing)).State);
        Assert.Equal(DocumentEmbeddingState.Queued, (await JobAsync(transient)).State);
        Assert.NotEmpty(_logs.Entries);
        foreach (var secret in new[] { "cardiomyopathy", "quokka", "hepatitis", "wombat", "nephritis", "numbat", "Zorrobelle", "Synthetic" })
        {
            Assert.DoesNotContain(_logs.Entries, e => e.Contains(secret, StringComparison.OrdinalIgnoreCase));
        }

        Assert.Contains(_logs.Entries, e => e.Contains(succeeding.ToString()) && e.Contains("embedded"));
        Assert.Contains(_logs.Entries, e => e.Contains(EmbeddingFailure.ProviderRejected));
    }

    // ---- Backfill migration --------------------------------------------------------------------------------------

    [Fact]
    public async Task The_migration_queues_one_job_for_every_document_already_read_and_is_safe_to_run_again()
    {
        await using var db = NewDatabase("embedding_backfill");
        var migrator = db.GetService<IMigrator>();
        var userId = Guid.NewGuid().ToString();
        var animalId = Guid.NewGuid();
        var read = Guid.NewGuid();
        var queued = Guid.NewGuid();
        var failed = Guid.NewGuid();

        await migrator.MigrateAsync(PreviousMigration);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO users (id, user_name, email, email_confirmed, phone_number_confirmed, two_factor_enabled,
                lockout_enabled, access_failed_count)
            VALUES ({userId}, 'embedding@example.test', 'embedding@example.test', false, false, false, true, 0)
            """);
        await db.Database.ExecuteSqlAsync($"INSERT INTO animals (id, name, created_at) VALUES ({animalId}, 'Czarek', now())");
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO animal_members (animal_id, user_id, role, created_at) VALUES ({animalId}, {userId}, 'owner', now())
            """);
        foreach (var documentId in new[] { read, queued, failed })
        {
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO documents (id, animal_id, event_date, capture_time_zone, storage_state, created_at, uploaded_at)
                VALUES ({documentId}, {animalId}, '2026-09-01', 'Europe/Warsaw', 'stored', now(), now())
                """);
        }

        // The reading jobs of documents stored after the DocumentReading migration are created by capture; here by hand.
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO document_reading_jobs (document_id, state, run_generation, due_at, attempt_count, owner_retry_count,
                created_at, updated_at, completed_at, published_run_generation)
            VALUES ({read}, 'read', 1, now(), 1, 0, now(), now(), now(), 1)
            """);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO document_reading_jobs (document_id, state, run_generation, due_at, attempt_count, owner_retry_count,
                created_at, updated_at)
            VALUES ({queued}, 'queued', 1, now(), 0, 0, now(), now())
            """);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO document_reading_jobs (document_id, state, run_generation, due_at, attempt_count, owner_retry_count,
                failure_code, created_at, updated_at, completed_at)
            VALUES ({failed}, 'failed', 1, now(), 1, 0, 'unreadable_content', now(), now(), now())
            """);

        await migrator.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        await AssertOnlyReadDocumentQueuedAsync(db, read);

        // Rolling the schema back and forward again, and running the backfill by hand, queue the same work once.
        await migrator.MigrateAsync(PreviousMigration);
        await migrator.MigrateAsync();
        await AssertOnlyReadDocumentQueuedAsync(db, read);
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO document_embedding_jobs (document_id, state, due_at, attempt_count, requested_at, created_at, updated_at)
            SELECT document_id, 'queued', now(), 0, now(), now(), now() FROM document_reading_jobs WHERE state = 'read'
            ON CONFLICT (document_id) DO NOTHING
            """);
        await AssertOnlyReadDocumentQueuedAsync(db, read);

        await db.Database.EnsureDeletedAsync();
    }

    private static async Task AssertOnlyReadDocumentQueuedAsync(AppDbContext db, Guid readDocument)
    {
        var job = Assert.Single(await db.DocumentEmbeddingJobs.AsNoTracking().ToListAsync());
        Assert.Equal(readDocument, job.DocumentId);
        Assert.Equal(DocumentEmbeddingState.Queued, job.State);
        Assert.Equal(0, job.AttemptCount);
        Assert.Null(job.LeaseToken);
        Assert.Null(job.EmbeddedRunGeneration);
    }

    // ---- Helpers -------------------------------------------------------------------------------------------------

    private static DocumentTextPage Page(Guid fileId, int filePosition, int pageNumber, string text) => new()
    {
        DocumentFileId = fileId,
        FilePosition = filePosition,
        SourcePageNumber = pageNumber,
        ExtractionMethod = DocumentTextExtractionMethod.Ocr,
        Text = text,
    };

    private DocumentEmbeddingWorker Worker(
        FakeEmbeddingProvider provider, EmbeddingTokenPacer? pacer = null, TimeSpan? attemptDeadline = null)
    {
        var loggers = LoggerFactory.Create(builder => builder.AddProvider(_logs).SetMinimumLevel(LogLevel.Information));
        return new DocumentEmbeddingWorker(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            EmbeddingAvailability.Resolve(new EmbeddingOptions(), isDevelopment: false),
            provider,
            new DocumentEmbeddingWakeUp(),
            TimeProvider.System,
            loggers.CreateLogger<DocumentEmbeddingWorker>())
        {
            Pacer = pacer,
            AttemptDeadline = attemptDeadline ?? TimeSpan.FromMinutes(5), // the worker's default
        };
    }

    // Runs the worker until no job is due.
    private async Task EmbedAllAsync(FakeEmbeddingProvider provider)
    {
        var worker = Worker(provider);
        while (await worker.RunOnceAsync(CancellationToken.None))
        {
        }
    }

    // Stored document whose reading is published through the real publication path (which queues the embedding job).
    // One text is a photo; several texts are the pages of a PDF.
    private async Task<Guid> ReadAsync(string userId, Guid animalId, params string[] pageTexts)
    {
        var document = pageTexts.Length == 1
            ? await factory.AddStoredAsync(userId, animalId, EventDate)
            : await factory.AddStoredAsync(userId, animalId, EventDate, DocumentFile.PdfContentType);
        var claim = Assert.IsType<DocumentReadingClaim>(await UseReadingJobsAsync(jobs => jobs.ClaimNextAsync(TimeSpan.FromMinutes(5))));
        Assert.Equal(document.Id, claim.DocumentId);
        Assert.Equal(
            DocumentReadingPublication.Published,
            await UseReadingJobsAsync(jobs => jobs.PublishAsync(claim, ReadingPages(document.Files[0].Id, pageTexts))));
        return document.Id;
    }

    // What an owner Retry does: the finished reading starts the next generation and publishes a new revision.
    private async Task RepublishAsync(Guid documentId, params string[] pageTexts)
    {
        await ExecuteAsync($"""
            UPDATE document_reading_jobs
            SET state = 'queued', run_generation = run_generation + 1, attempt_count = 0, failure_code = NULL,
                completed_at = NULL, due_at = now() - interval '1 second'
            WHERE document_id = '{documentId}'
            """);
        var claim = Assert.IsType<DocumentReadingClaim>(await UseReadingJobsAsync(jobs => jobs.ClaimNextAsync(TimeSpan.FromMinutes(5))));
        Assert.Equal(documentId, claim.DocumentId);
        var fileId = await UseDatabaseAsync(db => db.DocumentFiles.AsNoTracking().Where(f => f.DocumentId == documentId).Select(f => f.Id).FirstAsync());
        Assert.Equal(
            DocumentReadingPublication.Published,
            await UseReadingJobsAsync(jobs => jobs.PublishAsync(claim, ReadingPages(fileId, pageTexts))));
    }

    private static List<DocumentTextPage> ReadingPages(Guid fileId, string[] texts) =>
        texts.Select((text, index) => Page(fileId, 0, index + 1, text)).ToList();

    private Task RenameAsync(string userId, Guid animalId, string name) => UseScopeAsync(async services =>
    {
        var animals = services.GetRequiredService<OwnedAnimals>();
        var animal = await animals.FindAsync(userId, animalId);
        var result = await animals.RenameAsync(userId, animalId, animal!.Version, name);
        Assert.Equal(AnimalMutationOutcome.Updated, result.Outcome);
    });

    // Reading and embedding jobs left by earlier tests of the class must not be claimed by this one.
    private async Task ParkAllJobsAsync()
    {
        await ExecuteAsync("""
            UPDATE document_reading_jobs
            SET state = 'queued', lease_token = NULL, lease_expires_at = NULL, due_at = now() + interval '100 years'
            WHERE state IN ('queued', 'reading')
            """);
        await ExecuteAsync("""
            UPDATE document_embedding_jobs
            SET state = 'queued', lease_token = NULL, lease_expires_at = NULL, due_at = now() + interval '100 years'
            WHERE state IN ('queued', 'embedding')
            """);
    }

    // The statement reading publication runs in its transaction, run on its own.
    private Task EnqueueAsync(Guid documentId) => UseScopeAsync(async services =>
    {
        await DocumentEmbeddingJobs.EnqueueDocumentAsync(services.GetRequiredService<AppDbContext>(), documentId);
    });

    private Task MakeDueAsync(Guid documentId) => ExecuteAsync(
        $"UPDATE document_embedding_jobs SET due_at = now() - interval '1 second' WHERE document_id = '{documentId}'");

    private Task ExecuteAsync(string sql) => UseDatabaseAsync(db => db.Database.ExecuteSqlRawAsync(sql));

    private async Task<DocumentEmbeddingJob> JobAsync(Guid documentId) => Assert.Single(await JobsAsync(documentId));

    private Task<List<DocumentEmbeddingJob>> JobsAsync(params Guid[] documentIds) =>
        UseDatabaseAsync(db => db.DocumentEmbeddingJobs.AsNoTracking().Where(j => documentIds.Contains(j.DocumentId)).ToListAsync());

    private Task<List<DocumentChunk>> ChunksAsync(Guid documentId) =>
        UseDatabaseAsync(db => db.DocumentChunks.AsNoTracking().Where(c => c.DocumentId == documentId).OrderBy(c => c.ChunkIndex).ToListAsync());

    // Each call gets its own scope (and DbContext), as separate worker iterations would.
    private async Task<T> UseScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    private Task UseScopeAsync(Func<IServiceProvider, Task> action) =>
        UseScopeAsync<int>(async services =>
        {
            await action(services);
            return 0;
        });

    private Task<T> UseDatabaseAsync<T>(Func<AppDbContext, Task<T>> action) =>
        UseScopeAsync(services => action(services.GetRequiredService<AppDbContext>()));

    private Task<T> UseJobsAsync<T>(Func<DocumentEmbeddingJobs, Task<T>> action) =>
        UseScopeAsync(services => action(services.GetRequiredService<DocumentEmbeddingJobs>()));

    private Task<T> UseReadingJobsAsync<T>(Func<DocumentReadingJobs, Task<T>> action) =>
        UseScopeAsync(services => action(services.GetRequiredService<DocumentReadingJobs>()));

    // A separate database on the class's PostgreSQL container, migrated by the test itself.
    private AppDbContext NewDatabase(string prefix)
    {
        var connection = new NpgsqlConnectionStringBuilder(
            factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("Default"))
        {
            Database = $"{prefix}_{Guid.NewGuid():N}",
        };
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection.ConnectionString, npgsql => npgsql.UseVector())
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options);
    }
}
