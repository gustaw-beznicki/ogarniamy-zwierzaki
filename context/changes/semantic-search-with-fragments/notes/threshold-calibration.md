# Threshold calibration (S-04, Phase 5)

**Status**: pending. The owner has not run the calibration yet. `searchMinSimilarity` in `infra/main.bicep` stays at
the provisional `0.30` until the Decision section below is filled in.

## Purpose

`Search:MinSimilarity` is one absolute cosine-similarity cutoff: a document is returned only when its best chunk
reaches it. Too low and unrelated documents appear; too high and vocabulary mismatches ("serce" in the query,
"kardiomiopatia" in the document) disappear, which is the product's central claim. The value can only be chosen
against the owner's real archive (roadmap Open Question 1), so this note records how it was measured and what was
decided.

## Privacy rules for this note

- Record only query wording the owner approves, document IDs (or their first 8 characters), animal IDs or names the
  owner approves, page numbers and scores.
- Never paste fragments, chunk text, document text, diagnoses or anything else read from a document. Describe an
  expected document by its ID, not by its content.
- Keep query vectors and any per-query files outside the repository and delete them after the run.

## Method

The search API returns no score (by design; see `services/api/Search/SearchResult.cs`), and there is no score view in
the app. Scores are observed in a developer-only run that repeats the API's own search statement read-only, with an
owner predicate and without the threshold, so every candidate shows its similarity.

### 1. Prerequisites

- The backfill is complete: the "Backfill progress" query in
  [Operate semantic search](../../../../docs/sop/operate-semantic-search.md#from-the-database) returns `0`.
- The owner's own sign-in holds Cognitive Services OpenAI User on the embeddings account (granted through
  `infra/bootstrap/embedding-access.bicep` with `developerPrincipalIds`).
- A temporary, separately approved database access path, as described in the SOP and in
  [Check incomplete uploads](../../../../docs/sop/check-incomplete-uploads.md): only the API's managed identity can
  sign in to the Azure database, so this needs, for example, a temporary Entra administrator and firewall rule for
  the owner. Remove it after the run; never widen access permanently.

### 2. Prepare about 20 queries

Write them in Polish, as the owner would type them, in a local file outside the repository. Cover:

- vocabulary mismatches, the main case: a lay word for a term the document uses (for example "serce Czarka" for a
  cardiomyopathy document);
- the same topics with and without the animal's name, to see how much the name moves the scores (see the warning
  below);
- a query that is only an animal's name;
- exact terms that appear in a document (a high-score reference);
- at least three unrelated queries that should return nothing (the empty state);
- topics present for one animal but asked about another animal.

For each query, note in advance which documents should be found (by ID, from the animal's document history).

### 3. Embed each query

The query must be embedded exactly as the API does it: trimmed, without a prefix, same deployment and dimensions
(`services/api/Search/OwnedSearch.cs`, `services/api/DocumentEmbedding/AzureOpenAiEmbeddingClient.cs`).

```bash
ENDPOINT="https://<embeddings account>.openai.azure.com"   # output embeddingsEndpoint of infra/main.bicep
TOKEN="$(az account get-access-token --scope https://cognitiveservices.azure.com/.default --query accessToken --output tsv)"
Q='<query, trimmed>'
jq -n --arg q "$Q" '{model: "text-embedding-3-large", input: [$q], dimensions: 1536}' \
  | curl -sS -X POST "${ENDPOINT}/openai/v1/embeddings" \
      -H "Authorization: Bearer ${TOKEN}" -H "Content-Type: application/json" --data-binary @- \
  | jq -c '.data[0].embedding' > q01.vec
```

`q01.vec` holds `[0.0123,...]` (1,536 numbers), the text form pgvector accepts. Each call is a billed embedding of a
few tokens.

### 4. Score the archive (read-only)

Find the owner's user ID once (returns only the ID):

```sql
SELECT id FROM users WHERE normalized_email = upper('<owner email>');
```

Then, per query, run the API's search statement without the threshold. It returns IDs, positions and scores only,
never text:

```sql
\set user_id '<owner user id>'
\set qv `cat q01.vec`
WITH query AS (
    SELECT :'qv'::vector AS embedding
),
owned_documents AS (
    SELECT d.id, d.animal_id, j.published_run_generation
    FROM documents AS d
    JOIN document_reading_jobs AS j ON j.document_id = d.id
    WHERE d.storage_state = 'stored'
        AND j.published_run_generation IS NOT NULL
        AND EXISTS (SELECT 1 FROM animal_members AS m
            WHERE m.animal_id = d.animal_id AND m.user_id = :'user_id' AND m.role = 'owner')
),
best_chunks AS (
    SELECT DISTINCT ON (c.document_id)
        c.document_id, c.file_position, c.source_page_number, c.chunk_index,
        1 - (c.embedding <=> query.embedding) AS similarity
    FROM document_chunks AS c
    JOIN owned_documents AS o
        ON o.id = c.document_id AND c.run_generation = o.published_run_generation
    CROSS JOIN query
    ORDER BY c.document_id, c.embedding <=> query.embedding, c.chunk_index
)
SELECT b.document_id, o.animal_id, b.file_position, b.source_page_number, b.chunk_index,
    round(b.similarity::numeric, 4) AS similarity
FROM best_chunks AS b
JOIN owned_documents AS o ON o.id = b.document_id
ORDER BY b.similarity DESC, b.document_id
LIMIT 20;
```

Record the top results in the table. To see what a result is about, open the document in the app as the owner; do
not query its text.

### 5. Confirm in the app

After choosing a value, commit it (step "Decision") and, once deployed, repeat a few queries on the search screen,
including "serce Czarka" and one unrelated query, to confirm the results and the empty state match the table.

## Known pitfalls

- **Animal-name bias.** Chunks are embedded as the animal's name, a blank line and the chunk text
  (`services/api/DocumentEmbedding/EmbeddingInput.cs`), while queries are embedded as typed. A query that names an
  animal therefore scores every document of that animal higher, whatever its topic, so the false matches to watch
  for are other documents of the same animal. Compare each topic with and without the name.
- **Fragile thresholds.** An earlier, superseded experiment with a different embedding provider (the Mistral line)
  found thresholds fragile and the animal-name prefix biasing scores. Expect a narrow gap between the weakest true
  match and the strongest false match, and record that gap, not only the chosen value.
- One global cutoff may stop fitting once more accounts exist (roadmap Open Question 1); a relative cutoff and
  removing OCR boilerplate before embedding are out of scope for S-04 (plan, "What We're NOT Doing").

## Results

Run date: _pending_. Documents embedded at the time: _pending_. Embedding version: `chunker-v1/text-embedding-3-large/1536d/input-v1`.

| # | Query (approved wording) | Kind | Expected documents | Returned documents (ID prefix: score), top 5 | Top score | Lowest expected score | Highest false-match score | False match? |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | serce Czarka | synonym + animal name | | | | | | |
| 2 | | | | | | | | |
| 3 | | | | | | | | |
| 4 | | | | | | | | |
| 5 | | | | | | | | |
| 6 | | | | | | | | |
| 7 | | | | | | | | |
| 8 | | | | | | | | |
| 9 | | | | | | | | |
| 10 | | | | | | | | |
| 11 | | | | | | | | |
| 12 | | | | | | | | |
| 13 | | | | | | | | |
| 14 | | | | | | | | |
| 15 | | | | | | | | |
| 16 | | | | | | | | |
| 17 | | unrelated | none | | | — | | |
| 18 | | unrelated | none | | | — | | |
| 19 | | unrelated | none | | | — | | |
| 20 | | animal name only | | | | | | |

A false match is a returned document the owner would not expect for that query.

## Decision rule

1. Take the highest false-match score over all queries (`F`) and the lowest expected-document score (`E`).
2. Choose the lowest threshold that removes every false match: the smallest two-decimal value above `F`.
3. If that value is above `E`, a true match would be lost. Record which query and document, and let the owner choose
   between losing it and keeping that false match; prefer keeping vocabulary-mismatch matches such as "serce Czarka".
4. Commit the value as `searchMinSimilarity` in `infra/main.bicep`; it reaches the app as `Search__MinSimilarity` on
   the next approved deployment.

## Decision

_Pending the owner's calibration run._

- `F` (highest false-match score): _pending_
- `E` (lowest expected score): _pending_
- Chosen threshold: _pending_ (provisional `0.30` until then)
- Trade-offs accepted: _pending_
- "serce Czarka" finds the cardiomyopathy document at the chosen threshold: _pending_
- Approved by the owner on: _pending_
