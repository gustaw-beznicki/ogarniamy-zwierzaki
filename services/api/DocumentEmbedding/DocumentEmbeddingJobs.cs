using System.Data;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ogarniamy_zwierzaki_api.Data;
using Pgvector;

namespace ogarniamy_zwierzaki_api.DocumentEmbedding;

// TRUSTED SYSTEM BOUNDARY. Job and chunk access for the background embedding worker, and the enqueue statements run by
// the transactions that publish reading or rename an animal. It deliberately bypasses owner scoping: it addresses jobs
// by document ID with no user at all. It must never be injected into an HTTP handler or used to answer a user's
// request; owner-facing reads (search) go through an owner-scoped service that reaches documents only through the
// user's animal memberships. No animal-activity predicate applies here: documents of inactive animals are embedded
// like any other.
//
// Fencing: ClaimNextAsync hands out a unique lease token. Publishing and failing lock the job row and require that
// token and an unexpired lease, compared with database time. Every enqueue resets the job to Queued and clears its
// lease, so a worker that was embedding stale input finds its lease gone and writes nothing. PublishAsync additionally
// checks, under the same row lock, that the reading revision and the animal name are still the ones embedded.
public sealed partial class DocumentEmbeddingJobs(AppDbContext db)
{
    // Queues (or re-queues) the embedding of one document, in the caller's transaction: reading publication. Idempotent
    // upsert: a repeated call leaves exactly one Queued job, due now, with its attempts reset. A previous result
    // (embedded_*) is kept until the new one replaces it.
    public static Task EnqueueDocumentAsync(AppDbContext db, Guid documentId, CancellationToken cancellationToken = default) =>
        db.Database.ExecuteSqlAsync($"""
            INSERT INTO document_embedding_jobs (document_id, state, due_at, attempt_count, requested_at, created_at, updated_at)
            VALUES ({documentId}, 'queued', now(), 0, now(), now(), now())
            ON CONFLICT (document_id) DO UPDATE
            SET state = 'queued', due_at = now(), attempt_count = 0, lease_token = NULL, lease_expires_at = NULL,
                failure_code = NULL, requested_at = now(), updated_at = now()
            """, cancellationToken);

    // Queues (or re-queues) the embedding of every read document of one animal, in the caller's transaction: a rename
    // that changed the name. Documents whose reading is not read (queued, reading, failed) are not touched; publishing
    // their text queues them with the name current at that time. Returns how many documents were queued.
    public static Task<int> EnqueueAnimalDocumentsAsync(
        AppDbContext db, Guid animalId, CancellationToken cancellationToken = default) =>
        db.Database.ExecuteSqlAsync($"""
            INSERT INTO document_embedding_jobs (document_id, state, due_at, attempt_count, requested_at, created_at, updated_at)
            SELECT d.id, 'queued', now(), 0, now(), now(), now()
            FROM documents d
            JOIN document_reading_jobs r ON r.document_id = d.id
            WHERE d.animal_id = {animalId} AND r.state = 'read'
            ON CONFLICT (document_id) DO UPDATE
            SET state = 'queued', due_at = now(), attempt_count = 0, lease_token = NULL, lease_expires_at = NULL,
                failure_code = NULL, requested_at = now(), updated_at = now()
            """, cancellationToken);

    // Claims the next due job: a Queued job whose DueAt has passed, or an Embedding job whose lease expired. Concurrent
    // claimers skip each other's locked rows, so each job goes to at most one of them. Increments AttemptCount without
    // an upper bound (a reclaim after a crash counts too); the worker fails a claim beyond its budget. Null when nothing
    // is due.
    public async Task<DocumentEmbeddingClaim?> ClaimNextAsync(TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);
        var token = Guid.NewGuid();

        // One statement in its own implicit transaction: the row lock lasts only for the claim itself.
        var claimed = await db.Database.SqlQuery<Guid>($"""
            UPDATE document_embedding_jobs
            SET state = 'embedding', lease_token = {token}, lease_expires_at = now() + {leaseDuration},
                attempt_count = attempt_count + 1, updated_at = now()
            WHERE document_id = (
                SELECT document_id FROM document_embedding_jobs
                WHERE (state = 'queued' AND due_at <= now()) OR (state = 'embedding' AND lease_expires_at <= now())
                ORDER BY due_at, document_id
                LIMIT 1
                FOR UPDATE SKIP LOCKED)
            RETURNING document_id AS "Value"
            """).ToListAsync(cancellationToken);
        if (claimed.Count == 0)
        {
            return null;
        }

        var documentId = claimed[0];
        return await db.DocumentEmbeddingJobs
            .AsNoTracking()
            .Where(j => j.DocumentId == documentId && j.LeaseToken == token)
            .Select(j => new DocumentEmbeddingClaim(j.DocumentId, token, j.LeaseExpiresAt!.Value, j.AttemptCount))
            .SingleOrDefaultAsync(cancellationToken);
    }

    // The published text revision of a document and its animal's current name, read in one snapshot. Null when the
    // document has no published revision (it was never read), which an embedding job cannot be for.
    public async Task<DocumentEmbeddingSource?> LoadSourceAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var head = await (
            from job in db.DocumentReadingJobs.AsNoTracking()
            join document in db.Documents.AsNoTracking() on job.DocumentId equals document.Id
            join animal in db.Animals.AsNoTracking() on document.AnimalId equals animal.Id
            where job.DocumentId == documentId && job.PublishedRunGeneration != null
            select new { RunGeneration = job.PublishedRunGeneration!.Value, AnimalName = animal.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (head is null)
        {
            return null;
        }

        var pages = await db.DocumentTextPages
            .AsNoTracking()
            .Where(p => p.DocumentId == documentId && p.RunGeneration == head.RunGeneration)
            .OrderBy(p => p.FilePosition)
            .ThenBy(p => p.SourcePageNumber)
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new DocumentEmbeddingSource(head.RunGeneration, head.AnimalName, pages);
    }

    // Replaces the document's chunks and marks the job Embedded, in one transaction, only while the claim's lease is
    // current and the reading job's published_run_generation and the animal's name still equal the source the chunks were
    // computed from (checked under the job row lock). Otherwise the result is discarded: Stale re-queues the job at once
    // (handing the attempt back); LeaseLost writes nothing. chunks[i] is stored with vectors[i].
    public async Task<DocumentEmbeddingPublication> PublishAsync(
        DocumentEmbeddingClaim claim, DocumentEmbeddingSource source, string embeddingVersion,
        IReadOnlyList<PageChunk> chunks, IReadOnlyList<float[]> vectors, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(embeddingVersion);
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentNullException.ThrowIfNull(vectors);
        if (chunks.Count != vectors.Count)
        {
            throw new ArgumentException("There must be exactly one vector per chunk.", nameof(vectors));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockCurrentLeaseAsync(claim, cancellationToken))
        {
            return DocumentEmbeddingPublication.LeaseLost;
        }

        var current = await db.Database.SqlQuery<int>($"""
            SELECT 1 AS "Value"
            FROM document_reading_jobs r
            JOIN documents d ON d.id = r.document_id
            JOIN animals a ON a.id = d.animal_id
            WHERE r.document_id = {claim.DocumentId} AND r.published_run_generation = {source.RunGeneration}
                AND a.name = {source.AnimalName}
            """).ToListAsync(cancellationToken);
        if (current.Count == 0)
        {
            await db.Database.ExecuteSqlAsync($"""
                UPDATE document_embedding_jobs
                SET state = 'queued', due_at = now(), lease_token = NULL, lease_expires_at = NULL,
                    attempt_count = GREATEST(attempt_count - 1, 0), updated_at = now()
                WHERE document_id = {claim.DocumentId}
                """, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return DocumentEmbeddingPublication.Stale;
        }

        try
        {
            await db.Database.ExecuteSqlAsync(
                $"DELETE FROM document_chunks WHERE document_id = {claim.DocumentId}", cancellationToken);
            db.DocumentChunks.AddRange(chunks.Select((chunk, index) => new DocumentChunk
            {
                DocumentId = claim.DocumentId,
                ChunkIndex = index,
                RunGeneration = source.RunGeneration,
                DocumentFileId = chunk.DocumentFileId,
                FilePosition = chunk.FilePosition,
                SourcePageNumber = chunk.SourcePageNumber,
                StartOffset = chunk.StartOffset,
                EndOffset = chunk.EndOffset,
                Text = chunk.Text,
                Embedding = new Vector(vectors[index]),
                EmbeddingVersion = embeddingVersion,
            }));
            await db.SaveChangesAsync(cancellationToken);
            await db.Database.ExecuteSqlAsync($"""
                UPDATE document_embedding_jobs
                SET state = 'embedded', embedded_run_generation = {source.RunGeneration},
                    embedded_animal_name = {source.AnimalName}, failure_code = NULL,
                    lease_token = NULL, lease_expires_at = NULL, updated_at = now()
                WHERE document_id = {claim.DocumentId}
                """, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }

        return DocumentEmbeddingPublication.Embedded;
    }

    // A transient failure: the job goes back to Queued, due after delay (database time). With countAttempt false (a
    // graceful shutdown, not a failure) the claim's attempt is handed back. False when the lease is no longer current.
    public async Task<bool> RequeueAsync(
        DocumentEmbeddingClaim claim, TimeSpan delay, CancellationToken cancellationToken = default, bool countAttempt = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockCurrentLeaseAsync(claim, cancellationToken))
        {
            return false;
        }

        await db.Database.ExecuteSqlAsync($"""
            UPDATE document_embedding_jobs
            SET state = 'queued', due_at = now() + {delay}, lease_token = NULL, lease_expires_at = NULL,
                attempt_count = GREATEST(attempt_count - {(countAttempt ? 0 : 1)}, 0), updated_at = now()
            WHERE document_id = {claim.DocumentId}
            """, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    // A terminal failure: the job becomes Failed with a sanitized code (lowercase letters, digits and underscores, at
    // most 64; never a provider message). Stored chunks of an earlier result are kept. Only a new enqueue queues the job
    // again. False when the lease is no longer current.
    public async Task<bool> FailAsync(
        DocumentEmbeddingClaim claim, string failureCode, CancellationToken cancellationToken = default)
    {
        if (failureCode is null || !FailureCodePattern().IsMatch(failureCode))
        {
            throw new ArgumentException("A failure code is 1-64 lowercase letters, digits or underscores.", nameof(failureCode));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await LockCurrentLeaseAsync(claim, cancellationToken))
        {
            return false;
        }

        await db.Database.ExecuteSqlAsync($"""
            UPDATE document_embedding_jobs
            SET state = 'failed', failure_code = {failureCode}, lease_token = NULL, lease_expires_at = NULL,
                updated_at = now()
            WHERE document_id = {claim.DocumentId}
            """, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    [GeneratedRegex("^[a-z0-9_]{1,64}$")]
    private static partial Regex FailureCodePattern();

    // Inside the caller's transaction: locks the job row when the claim still holds the lease (same token, not expired
    // by database time). The lock keeps an enqueue or a reclaim from interleaving with the caller's writes.
    private async Task<bool> LockCurrentLeaseAsync(DocumentEmbeddingClaim claim, CancellationToken cancellationToken) =>
        (await db.Database.SqlQuery<int>($"""
            SELECT 1 AS "Value" FROM document_embedding_jobs
            WHERE document_id = {claim.DocumentId} AND state = 'embedding' AND lease_token = {claim.LeaseToken}
                AND lease_expires_at > clock_timestamp()
            FOR UPDATE
            """).ToListAsync(cancellationToken)).Count == 1;
}
