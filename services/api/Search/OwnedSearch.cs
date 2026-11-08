using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ogarniamy_zwierzaki_api.Data;
using ogarniamy_zwierzaki_api.DocumentEmbedding;
using ogarniamy_zwierzaki_api.Documents;
using Pgvector;

namespace ogarniamy_zwierzaki_api.Search;

// Semantic search over the signed-in user's own documents. Both queries reach documents only through Stored documents
// of animals the user owns (animal_members rows with the owner role), whether or not the animal is active, and only
// through chunks of the reading job's published run generation, so another account's text and superseded revisions can
// never appear. A cross-account fragment is a critical failure: keep the owner predicate in every statement here.
//
// Never logs the query, fragments or provider payloads; only counts, failure codes and timings.
public sealed class OwnedSearch(
    AppDbContext db,
    IEmbeddingClient embeddings,
    EmbeddingAvailability availability,
    IOptions<SearchOptions> options,
    ILogger<OwnedSearch> logger)
{
    public const int MaxQueryLength = 500;

    public const int MaxResults = 20;

    // The trimmed query, or null when it is empty or longer than MaxQueryLength characters.
    public static string? TryNormalizeQuery(string? query)
    {
        var trimmed = query?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxQueryLength ? null : trimmed;
    }

    // Null when search cannot answer (embedding is unavailable or the provider failed): the caller must not show partial
    // or substitute results. The query must already be normalized.
    public async Task<SearchResponse?> SearchAsync(string userId, string query, CancellationToken cancellationToken = default)
    {
        if (!availability.IsAvailable)
        {
            logger.LogWarning("Search is unavailable: embedding is not available.");
            return null;
        }

        if (SearchOptions.TryRead(options, out var reason) is not { } settings)
        {
            logger.LogWarning("Search is unavailable: {Reason}", reason);
            return null;
        }

        Vector queryVector;
        try
        {
            // The query text goes to the provider as it is, without a prefix.
            var vectors = await embeddings.EmbedAsync([query], cancellationToken);
            if (vectors.Count != 1 || vectors[0].Length != EmbeddingOptions.StoredDimensions)
            {
                throw EmbeddingFailure.Terminal(EmbeddingFailure.ProviderProtocol);
            }

            queryVector = new Vector(vectors[0]);
        }
        catch (EmbeddingFailure failure)
        {
            logger.LogWarning("Search is unavailable: the embedding provider failed ({FailureCode}).", failure.Code);
            return null;
        }

        var minSimilarity = settings.MinSimilarity;
        var rows = await FindBestChunksAsync(userId, queryVector, minSimilarity, cancellationToken);
        var notSearchable = await CountNotSearchableAsync(userId, cancellationToken);

        logger.LogInformation(
            "Search returned {ResultCount} documents; {NotSearchableCount} documents are not searchable yet.",
            rows.Count, notSearchable);
        return new SearchResponse(
            rows.Select(r => new SearchResult(
                r.DocumentId, r.AnimalId, r.AnimalName, r.AnimalIsActive, r.EventDate, r.FileId, r.FilePosition, r.SourcePageNumber,
                r.FilePageCount, r.Fragment, StoredDocumentFileResponse.OriginalUrlFor(r.DocumentId, r.FileId))).ToList(),
            notSearchable);
    }

    // Exact cosine search (no index): each owned document's best chunk of its published generation, kept when its
    // similarity reaches the threshold, best first. Similarity is 1 minus pgvector's cosine distance.
    private async Task<List<SearchRow>> FindBestChunksAsync(
        string userId, Vector queryVector, double minSimilarity, CancellationToken cancellationToken) =>
        await db.Database.SqlQuery<SearchRow>($"""
            WITH query AS (
                SELECT {queryVector}::vector AS embedding
            ),
            owned_documents AS (
                SELECT d.id, d.animal_id, d.event_date, j.published_run_generation
                FROM documents AS d
                JOIN document_reading_jobs AS j ON j.document_id = d.id
                WHERE d.storage_state = 'stored'
                    AND j.published_run_generation IS NOT NULL
                    AND EXISTS (SELECT 1 FROM animal_members AS m
                        WHERE m.animal_id = d.animal_id AND m.user_id = {userId} AND m.role = 'owner')
            ),
            best_chunks AS (
                SELECT DISTINCT ON (c.document_id)
                    c.document_id, c.document_file_id, c.file_position, c.source_page_number, c.run_generation, c.text,
                    1 - (c.embedding <=> query.embedding) AS similarity
                FROM document_chunks AS c
                JOIN owned_documents AS o
                    ON o.id = c.document_id AND c.run_generation = o.published_run_generation
                CROSS JOIN query
                ORDER BY c.document_id, c.embedding <=> query.embedding, c.chunk_index
            )
            SELECT
                b.document_id AS document_id,
                o.animal_id AS animal_id,
                a.name AS animal_name,
                a.is_active AS animal_is_active,
                o.event_date AS event_date,
                b.document_file_id AS file_id,
                b.file_position AS file_position,
                b.source_page_number AS source_page_number,
                (SELECT count(*)::int FROM document_text_pages AS p
                    WHERE p.document_id = b.document_id AND p.run_generation = b.run_generation
                        AND p.document_file_id = b.document_file_id) AS file_page_count,
                b.text AS fragment
            FROM best_chunks AS b
            JOIN owned_documents AS o ON o.id = b.document_id
            JOIN animals AS a ON a.id = o.animal_id
            WHERE b.similarity >= {minSimilarity}
            ORDER BY b.similarity DESC, b.document_id
            LIMIT {MaxResults}
            """).ToListAsync(cancellationToken);

    // The user's Stored documents whose text is still being read, or whose embedding is not current for the published
    // generation. A failed reading is excluded: the document page shows it.
    private async Task<int> CountNotSearchableAsync(string userId, CancellationToken cancellationToken) =>
        await db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value"
            FROM documents AS d
            JOIN document_reading_jobs AS j ON j.document_id = d.id
            LEFT JOIN document_embedding_jobs AS e ON e.document_id = d.id
            WHERE d.storage_state = 'stored'
                AND EXISTS (SELECT 1 FROM animal_members AS m
                    WHERE m.animal_id = d.animal_id AND m.user_id = {userId} AND m.role = 'owner')
                AND j.state <> 'failed'
                AND (j.state IN ('queued', 'reading')
                    OR e.document_id IS NULL
                    OR e.state <> 'embedded'
                    OR e.embedded_run_generation IS DISTINCT FROM j.published_run_generation)
            """).SingleAsync(cancellationToken);
}
