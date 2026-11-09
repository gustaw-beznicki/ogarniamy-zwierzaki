# Threshold calibration (S-04, Phase 5)

**Status**: decided for the MVP on synthetic documents. The owner set `searchMinSimilarity` in `infra/main.bicep` to
`0.37` on 2026-11-09 from the scored run below; repeat the scored run on the real archive once it is on Azure.

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

## Preliminary run on synthetic documents (2026-11-09)

The owner's Azure account held no documents after the Phase 5 deploy, so six fictional one-page PDFs (each marked as
a test document, invented clinic and vet) were uploaded to the only animal, Czarek: echocardiography with
hypertrophic cardiomyopathy (`echo`), vaccination certificate (`vacc`), blood count and biochemistry (`blood`),
dental scaling and extraction (`teeth`), dermatology consult with an elimination diet (`skin`), and parasite
prevention (`para`). All six were read and embedded within about a minute (`notSearchableCount` 0). Queries ran
through the app's `/api/search` with the deployed threshold `0.30`; the API returns no scores, so only order and
count were recorded. Every query answered in 207–280 ms from the browser on a warm app.

| Query | Results (in order) | Top result right? | Irrelevant results returned? |
| --- | --- | --- | --- |
| serce Czarka | echo > vacc > teeth > skin > para > blood | yes | yes (5) |
| kardiomiopatia | echo | yes | no |
| szmer w sercu | echo > teeth | yes | yes (1) |
| wścieklizna | vacc > teeth > para > blood > echo > skin | yes | yes (5) |
| kiedy następne szczepienie | vacc > para > teeth > echo > skin > blood | yes | yes (4–5) |
| wyniki krwi | blood > echo > skin > teeth > para > vacc | yes | yes (5) |
| nerki kreatynina | blood > echo > teeth > para > skin > vacc | yes | yes (5) |
| usunięcie kamienia nazębnego | teeth > para > vacc | yes | yes (2) |
| ząb | teeth > skin > vacc > para > echo | yes | yes (4) |
| swędzenie i łysienie | skin > para > teeth > vacc | yes | yes (2–3) |
| dieta eliminacyjna | teeth > skin > para > blood | **no** (skin is 2nd) | yes (3) |
| pchły i kleszcze | para > skin > vacc > teeth > echo > blood | yes | yes (4–5) |
| odrobaczanie | para > teeth > skin > vacc > blood > echo | yes | yes (5) |
| złamana łapa (unrelated) | teeth > echo > skin > vacc > para > blood | — | yes (6) |
| przepis na sernik (unrelated) | teeth > skin | — | yes (2) |
| kastracja (unrelated) | teeth > para > vacc > skin > echo > blood | — | yes (6) |
| rentgen klatki piersiowej (unrelated) | echo > teeth > blood > vacc > para > skin | — | yes (6) |
| USG brzucha (unrelated) | blood > echo > teeth > skin > para > vacc | — | yes (6) |

Observations:

- Ranking is good: the expected document is first in 12 of 13 related queries; "serce Czarka" puts the
  cardiomyopathy document first.
- `0.30` is far too permissive: unrelated queries (a cheesecake recipe, a broken paw) return documents, so the empty
  state almost never appears. Queries naming the animal match everything, as expected from the animal-name prefix.
- Each synthetic document fits in one chunk, so these numbers say nothing about fragment choice within long documents.
- Isolation check on Azure (same day): a second account with its own animal ran eight queries (including
  "serce Czarka", "kardiomiopatia" and "Czarek") and got 0 results each, and the owner's document details and
  reading endpoints answered 404. Latency there was 201–248 ms, with 664 ms for the first search of the session;
  a cold start after F1 idle time was not measured.
- Choosing the value still needs scores (step 4 of the method). The real archive should be used for the final pick;
  these six documents are only a sanity check and can stay as test data.

## Scored run on synthetic documents (2026-11-09)

The provisional `0.70` (deployed with PR #46) returned no result for any of the 18 queries above, including
"kardiomiopatia" and "serce Czarka". Scores were then computed outside the database: the same six documents' text
(`pdftotext`, blank lines removed, prefixed with `Czarek` and a blank line as the API does) and the 18 queries were
embedded with the deployed `text-embedding-3-large` deployment at 1536 dimensions, using the owner's own Entra token,
and compared by cosine similarity. Only synthetic text was sent. Each document is one chunk, so these are the scores
search compares with the threshold (the app's text extraction may differ slightly in whitespace).

| Query | Kind | Scores, highest first |
| --- | --- | --- |
| serce Czarka | synonym + animal name | echo 0.472, vacc 0.409, teeth 0.396, para 0.389, blood 0.387, skin 0.380 |
| kardiomiopatia | exact term | echo 0.377, blood 0.281, teeth 0.247, skin 0.246, vacc 0.233, para 0.220 |
| szmer w sercu | synonym | echo 0.400, teeth 0.299, blood 0.284, skin 0.278, para 0.271, vacc 0.269 |
| wścieklizna | exact term | vacc 0.403, teeth 0.358, para 0.342, blood 0.335, echo 0.327, skin 0.303 |
| kiedy następne szczepienie | question | vacc 0.492, para 0.451, echo 0.389, teeth 0.381, blood 0.367, skin 0.367 |
| wyniki krwi | synonym | blood 0.533, echo 0.404, para 0.384, skin 0.375, teeth 0.367, vacc 0.362 |
| nerki kreatynina | exact term | blood 0.479, echo 0.384, para 0.342, teeth 0.342, vacc 0.333, skin 0.323 |
| usunięcie kamienia nazębnego | exact phrase | teeth 0.507, para 0.338, vacc 0.303, echo 0.292, skin 0.292, blood 0.286 |
| ząb | synonym | teeth 0.429, skin 0.341, para 0.327, vacc 0.326, echo 0.323, blood 0.310 |
| swędzenie i łysienie | synonym | skin 0.472, para 0.352, teeth 0.322, vacc 0.313, echo 0.308, blood 0.300 |
| dieta eliminacyjna | exact phrase | skin 0.364, teeth 0.353, para 0.321, blood 0.317, echo 0.288, vacc 0.281 |
| pchły i kleszcze | exact phrase | para 0.457, skin 0.420, vacc 0.391, echo 0.388, teeth 0.382, blood 0.373 |
| odrobaczanie | exact term | para 0.457, teeth 0.409, skin 0.390, vacc 0.384, blood 0.380, echo 0.373 |
| złamana łapa | unrelated | echo 0.448, teeth 0.447, para 0.413, skin 0.412, vacc 0.406, blood 0.395 |
| przepis na sernik | unrelated | teeth 0.306, skin 0.289, para 0.277, echo 0.275, vacc 0.271, blood 0.256 |
| kastracja | unrelated | teeth 0.420, para 0.379, echo 0.371, vacc 0.365, skin 0.363, blood 0.351 |
| rentgen klatki piersiowej | unrelated | echo 0.461, teeth 0.412, blood 0.402, vacc 0.397, para 0.381, skin 0.368 |
| USG brzucha | unrelated | blood 0.391, echo 0.380, teeth 0.355, skin 0.351, para 0.337, vacc 0.317 |

Findings:

- The expected document scores 0.364–0.533 (`E` = 0.364, "dieta eliminacyjna"); unrelated queries reach 0.461
  (`F`, "rentgen klatki piersiowej"). The ranges overlap, so no absolute threshold removes every false match without
  losing true ones: the decision rule's step 3 applies.
- The animal-name prefix lifts every document by a similar amount, which is why queries naming the animal, and
  generic medical queries, match most documents.
- The gap between the first and second document is large for specific queries (0.06–0.17) and small for unrelated ones
  (0.001–0.05), so a relative cut on top of the absolute one would separate them better. A relative cut is excluded from
  S-04 ("What We're NOT Doing"); it is a candidate follow-up.

## Decision rule

1. Take the highest false-match score over all queries (`F`) and the lowest expected-document score (`E`).
2. Choose the lowest threshold that removes every false match: the smallest two-decimal value above `F`.
3. If that value is above `E`, a true match would be lost. Record which query and document, and let the owner choose
   between losing it and keeping that false match; prefer keeping vocabulary-mismatch matches such as "serce Czarka".
4. Commit the value as `searchMinSimilarity` in `infra/main.bicep`; it reaches the app as `Search__MinSimilarity` on
   the next approved deployment.

## Decision

`0.37`, chosen by the owner on 2026-11-09 from the scored synthetic run, keeping vocabulary-mismatch matches as the
decision rule prefers.

- `F` (highest false-match score): 0.461 ("rentgen klatki piersiowej")
- `E` (lowest expected score): 0.364 ("dieta eliminacyjna")
- Chosen threshold: `0.37`
- Trade-offs accepted: "dieta eliminacyjna" loses its document (0.364); generic or unrelated medical queries
  ("złamana łapa", "kastracja", "rentgen klatki piersiowej") still return documents; "przepis na sernik" returns none.
- "serce Czarka" finds the cardiomyopathy document at the chosen threshold: yes by score (0.472, first); to be
  confirmed in the deployed app after the release that carries `0.37`
- Approved by the owner on: 2026-11-09
