<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Semantic Search with Fragments (S-04)

- **Plan**: context/changes/semantic-search-with-fragments/plan.md
- **Scope**: Full plan (completed phases only; Phase 5 not implemented)
- **Reviewed phases**: 1, 2, 3, 4
- **Date**: 2026-11-08
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 5 warnings, 5 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | WARNING |
| Scope Discipline | WARNING |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | WARNING |
| Success Criteria | PASS |

Success criteria evidence (2026-11-08): `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj` 273/273;
`dotnet build services/api/ogarniamy-zwierzaki-api.csproj` 0 errors; `infra/deploy.sh lint` clean;
`npm run check --prefix apps/web` 0 errors / 0 warnings; `npm run build --prefix apps/web` passes; i18n catalogues have
203 keys each; no Polish outside `pl.ts`. Manual rows 1.3, 1.4, 2.4, 3.3, 4.5, 4.6 were confirmed by the owner in this
session (what-if output, local DeveloperCredential run, Chrome walkthrough).

Account isolation in `services/api/Search/OwnedSearch.cs` is sound. Both the result query and `notSearchableCount` go
through the `EXISTS (animal_members … role = 'owner')` predicate with a parameterised user id. `EXISTS` is a semi-join
and the job tables join 1:1, so no row can be duplicated or leak in through another member row. The vector, threshold
and limit are parameters, the column lists are explicit, and the isolation tests cover search rows, the count,
another account's original (404) and inactive animals.

## Findings

### F1 — Embedding batch budget exceeds the deployment's rate limit

- **Severity**: ⚠️ WARNING
- **Impact**: 🔬 HIGH — architectural stakes; think carefully before deciding
- **Dimension**: Safety & Quality
- **Location**: services/api/DocumentEmbedding/AzureOpenAiEmbeddingClient.cs:26, infra/modules/embeddings.bicep:39
- **Detail**: One request may carry up to 250,000 estimated tokens (and 2,048 inputs). The deployment has capacity 50,
  that is 50K tokens per minute. If Azure OpenAI rejects a request whose estimated tokens exceed the per-minute
  allowance, a long document gets a 429 on every attempt and ends `failed` after about 43 minutes. The Phase 5 backfill
  queues every read document at once. Search uses the same deployment and does not retry, so user searches can turn
  into 503 `search_unavailable` during the backfill.
- **Fix A ⭐ Recommended**: Make the per-request token budget configurable (`Embedding:MaxEstimatedTokensPerRequest`,
  default well below the TPM, for example 16K) and pace the worker between batches.
  - Strength: Keeps every request inside the quota whatever the document size; small, local change in the client.
  - Tradeoff: More requests per long document; the backfill takes longer.
  - Confidence: MED — not verified that Azure rejects a single over-budget request; it is documented to count
    estimated tokens per request against TPM.
  - Blind spot: Actual token counts of real OCR text; measured during the Phase 5 backfill.
- **Fix B**: Raise the deployment capacity (the quota allows 350K TPM) and keep the client unchanged.
  - Strength: A Bicep parameter change only; also gives search more headroom during the backfill.
  - Tradeoff: A single huge document can still exceed any capacity; it costs nothing while idle (Standard is
    pay-per-token), but it moves the problem rather than removing it.
  - Confidence: MED — depends on how many tokens the largest real document produces.
  - Blind spot: Whether the Free Trial subscription allows increasing capacity without a quota request.
- **Decision**: FIXED (Fix A) — `Embedding:MaxEstimatedTokensPerRequest` (16,000) and `Embedding:WorkerTokensPerMinute` (40,000) with validation, a token-bucket `EmbeddingTokenPacer` used by the hosted worker per batch, docs and tests (282/282; break-check: pacer tests went red when the wait was removed).

### F2 — Failed embedding jobs are never re-queued

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: services/api/DocumentEmbedding/DocumentEmbeddingJobs.cs:214-239, docs/deployment.md ("Operating notes")
- **Detail**: Once a job is `failed` (for example after the accepted 401/403/404 retries during a misconfigured
  rollout, or after F1), only a new reading publish or an animal rename re-queues it. There is no Retry, no requeue
  on startup and no documented operator step. Such documents stay out of search and are counted forever as
  "still being processed" (`OwnedSearch.cs:131-133`).
- **Fix**: Document an owner runbook step that re-queues failed jobs (an `UPDATE document_embedding_jobs SET
  state = 'queued', due_at = now(), attempt_count = 0, failure_code = NULL WHERE state = 'failed'` with explicit
  columns) in a new `docs/sop/operate-semantic-search.md`, linked from `docs/deployment.md`, as part of Phase 5.
  - Strength: Matches how document reading is operated (`docs/sop/operate-document-reading.md`); no code change.
  - Tradeoff: Recovery stays manual.
  - Confidence: HIGH — the job row has all fields needed for a clean requeue.
  - Blind spot: Whether the "still being processed" wording should exclude failed embeddings; not decided here.
- **Decision**: FIXED — new `docs/sop/operate-semantic-search.md` (states, failure codes, read-only queries, backfill progress, pause/resume, approved requeue UPDATE with explicit columns, rollback note), linked from `docs/deployment.md` and the README SOP index; SQL checked against a local pgvector database.

### F3 — Error state goes beyond the planned generic error

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Scope Discipline
- **Location**: apps/web/src/components/SearchView.tsx:107-118, apps/web/src/i18n/en.ts (`searchUnavailable*`, `searchGoToAnimals`)
- **Detail**: The plan asks for a "generic `Alert` with Retry" and defers the dedicated "search unavailable" notice to
  S-05. The implementation shows "Search is unavailable right now. No substitute results are shown. All documents and
  originals remain available on the animal profiles." with Retry and "Go to animals", for every non-401 error
  (also 400 `invalid_query`).
- **Fix A ⭐ Recommended**: Accept it and record it as an addendum note in the plan's Phase 4 block and in the S-05 roadmap
  item, so S-05 starts from this notice.
  - Strength: The copy is accurate (originals stay available) and was confirmed in the Chrome walkthrough; no rework.
  - Tradeoff: S-05 scope shrinks implicitly; the plan becomes a slightly moving target.
  - Confidence: HIGH — the text makes no false promise and shows no substitute results.
  - Blind spot: S-05's notice may need different wording per failure type.
- **Fix B**: Reduce it to a generic error ("Something went wrong") with Retry only.
  - Strength: Keeps the S-04/S-05 split exactly as planned.
  - Tradeoff: Throws away accurate, already-tested copy; S-05 re-adds it.
  - Confidence: MED — a matter of scope preference.
  - Blind spot: None significant.
- **Decision**: FIXED (Fix A) — accepted; addendum added under Phase 4 in `plan.md` and a note on the S-05 item in `context/foundation/roadmap.md`.

### F4 — No rate limit on POST /api/search

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: services/api/Search/SearchEndpoints.cs:19, services/api/Program.cs
- **Detail**: Every authenticated search makes one paid Azure OpenAI call and one exact vector scan. Nothing limits
  requests per user. One signed-in account, or a script holding its session, can use up the shared 50K TPM and
  starve the worker and other users (see F1).
- **Fix**: Add a per-user fixed-window limiter (ASP.NET Core `AddRateLimiter`, for example 30 requests per minute keyed
  by user id) on `/api/search`, returning 429 ProblemDetails with a sanitized code; record it as a plan addendum or a
  follow-up change.
  - Strength: The built-in middleware is a few lines; it also bounds cost on the Free Trial credit.
  - Tradeoff: A new error code the UI must show (the generic error already covers it).
  - Confidence: MED — not planned; may be better as a separate small change.
  - Blind spot: Whether the API has other paid endpoints that should share the policy (reading Retry).
- **Decision**: FIXED — `SearchRateLimit` (per-account fixed window, `Search:MaxRequestsPerMinute` = 30, 429 `search_rate_limited` with Retry-After), `UseRateLimiter` after authorization, test (283/283; break-check: the test went red without `RequireRateLimiting`), docs and a plan addendum.

### F5 — Configuration table in local-development.md is split

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: docs/local-development.md:74
- **Detail**: A blank line between the `DocumentReading:WorkerEnabled` row and the new `Embedding:*` rows ends the
  table; the `Embedding:*` and `Search:MinSimilarity` rows render as plain text.
- **Fix**: Remove the blank line.
- **Decision**: FIXED — blank line removed; the table is continuous again.

### F6 — Bad similarity setting or malformed provider result returns 500

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: services/api/Search/SearchOptions.cs:8, services/api/Search/OwnedSearch.cs:49
- **Detail**: `Search:MinSimilarity` is bound without validation: an unparsable value makes every search 500, and a
  value outside 0–1 silently returns nothing or everything. `vectors.Single()` throws (500 instead of 503) if a client
  returns no vector. Embedding settings, by contrast, are validated and degrade with a logged reason.
- **Fix**: Validate `MinSimilarity` in [0, 1] at startup (`ValidateOnStart`) and map a vector-count mismatch to the
  existing 503 `search_unavailable` path.
- **Decision**: FIXED (degrade variant, owner choice) — `SearchOptions.TryRead` validates the section; invalid values make search answer 503 `search_unavailable` with a startup warning (`SearchSetup.LogSearchConfiguration`) instead of stopping the API; a wrong vector count or size is 503 too. Tests 286/286; break-check: 3 tests went red with the checks removed.

### F7 — Rename skips documents that are being re-read

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: services/api/DocumentEmbedding/DocumentEmbeddingJobs.cs (`EnqueueAnimalDocumentsAsync`)
- **Detail**: The rename enqueue selects only documents whose reading job is `state = 'read'`. A document with a
  published, embedded generation whose reading job is currently `reading` or `failed` after a Retry keeps old-name
  chunks until a later publish.
- **Fix**: Select documents by an existing published generation (or an existing embedding job) instead of the current
  reading state.
- **Decision**: FIXED — rename enqueue now selects documents with `published_run_generation IS NOT NULL`; the rename test covers a document being read again (286/286; break-check: the test went red with the old predicate).

### F8 — Result avatars always look active

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: apps/web/src/components/SearchView.tsx:147
- **Detail**: `AnimalAvatar` gets a hard-coded `isActive: true`, so documents of inactive animals show the active colour,
  unlike `AnimalDocuments`.
- **Fix**: Add `animalIsActive` to `SearchResult` (API record, SQL column list, `api.ts`) and pass it through.
- **Decision**: FIXED — `animalIsActive` added to `SearchResult`/`SearchRow`/SQL/`api.ts` and passed to the avatar; contract and isolation tests updated (286/286; break-check: the isolation test went red when the column was faked); plan addendum.

### F9 — Polish "other" plural form is ungrammatical

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: apps/web/src/i18n/pl.ts:206
- **Detail**: `notSearchableCount_other` reads "{count} dokumentu jest jeszcze przetwarzane, więc nie ma ich w wynikach."
  Integer counts never select this form, so users do not see it.
- **Fix**: Change it to "{count} dokumentu jest jeszcze przetwarzanych, więc nie ma ich w wynikach." in line with the
  other `_other` forms in `pl.ts`.
- **Decision**: FIXED — `notSearchableCount_other` corrected in `pl.ts`.

### F10 — Hardening odds and ends

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: scripts/smoke.sh, services/api/DocumentEmbedding/AzureOpenAiEmbeddingClient.cs:227-230, services/api/Data/Migrations/20261108142419_DocumentEmbedding.cs:22-44
- **Detail**: (a) The smoke test checks 401 for every other protected route but not anonymous `POST /api/search`.
  (b) A provider `Retry-After` is honoured without an upper bound. (c) `document_chunks.document_file_id` has no
  foreign key to `document_files`, and `ix_document_chunks_document_id` duplicates the primary key's leading column.
- **Fix**: Add the smoke check in Phase 5; cap `Retry-After` at one hour; leave (c) for a later migration.
- **Decision**: FIXED (a, b); (c) deferred — smoke check 2e (anonymous `POST /api/search` → 401) and docs; `Retry-After` capped at one hour with a test (287/287; break-check: the test went red without the cap). The chunk-file foreign key and the redundant index are left for a later migration.
