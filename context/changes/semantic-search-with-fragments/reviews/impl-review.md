<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Semantic Search with Fragments (S-04)

- **Plan**: context/changes/semantic-search-with-fragments/plan.md
- **Scope**: Full plan (phases 1–4 were reviewed in depth on 2026-11-08, whose ten findings were fixed in 60fbee7; this run re-checks those fixes and reviews Phase 5)
- **Reviewed phases**: 1, 2, 3, 4, 5
- **Date**: 2026-11-09
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 3 warnings, 6 observations

The previous report (phases 1–4, all findings resolved) is in git history at commit 60fbee7.

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | WARNING |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | WARNING |
| Success Criteria | WARNING |

Automated criteria re-run on 2026-11-09: `infra/deploy.sh lint` pass; `dotnet build` 0 errors; `dotnet test` 287/287; `npm run check` 0 errors; `npm run build` pass. Deploy of PR #45 and its smoke checks passed on main.

## Findings

### F1 — Long documents can never finish embedding

- **Severity**: ⚠️ WARNING
- **Impact**: 🔬 HIGH — architectural stakes; think carefully before deciding
- **Dimension**: Safety & Quality
- **Location**: services/api/DocumentEmbedding/DocumentEmbeddingWorker.cs:136, :186 (with :40); services/api/DocumentEmbedding/EmbeddingTokenPacer.cs:42
- **Detail**: Every batch of one job, including the pacer's `Task.Delay`, runs under a single 5-minute `AttemptDeadline`. At 40,000 tokens per minute one attempt can send roughly 200–240K estimated tokens (about 130 dense pages), while reading accepts up to 2,000 pages. A larger document re-sends every batch from zero on each attempt (paid tokens), hits `attempt_deadline_exceeded`, and is `failed` after five attempts; reserved tokens stay owed and delay the next jobs. The SOP requeue includes `attempt_deadline_exceeded`, so a requeue repeats the loop. Contradicts the F1 (previous review) fix's "whatever the document size".
- **Fix A ⭐ Recommended**: Scale the attempt deadline (and lease) to the work: `AttemptDeadline + estimatedTokens / WorkerTokensPerMinute`, with a fake-clock test for a document larger than one deadline's worth of tokens.
  - Strength: Every readable document becomes searchable; keeps the pacing guarantee.
  - Tradeoff: Long leases for very large documents; lease and heartbeat logic must stay consistent.
  - Confidence: MED — worker already computes estimates per batch; lease interplay needs care.
  - Blind spot: Not checked how the lease heartbeat behaves across a long paced attempt.
- **Fix B**: Fail fast with a terminal `text_too_long` code when estimated tokens / TPM exceed the deadline, and drop `attempt_deadline_exceeded` from the SOP requeue.
  - Strength: Small change; no wasted paid tokens; clear operator signal.
  - Tradeoff: Very long documents stay unsearchable by design (counted as not searchable).
  - Confidence: HIGH — a pre-check before the first batch.
  - Blind spot: Product acceptance of an unsearchable class of documents.
- **Decision**: FIXED (Fix A) — `EmbeddingTokenPacer.EstimateWait`; the worker extends the lease (`DocumentEmbeddingJobs.ExtendLeaseAsync`) and the attempt deadline by the expected pacing wait before the first batch; SOP and architecture updated; tests for the estimate and a paced document with a one-second deadline (break-check: the worker test went red without the deadline extension).

### F2 — Provisional 0.70 threshold is untested, yet 5.4 is ticked

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Success Criteria
- **Location**: infra/main.bicep:56; context/changes/semantic-search-with-fragments/plan.md (Progress 5.4); notes/threshold-calibration.md (Decision)
- **Detail**: All recorded queries ran at 0.30 and the API returns no scores. "serce Czarka" finding the cardiomyopathy document is confirmed only at 0.30; the note's Decision field says `_pending_`. For text-embedding-3-large, relevant short-query/chunk pairs often score well below 0.7 (general model behaviour, not measured here), so search may silently show the empty state for most queries once PR #46 deploys.
- **Fix**: After PR #46 deploys, rerun "kardiomiopatia", "serce Czarka" and one unrelated query on Azure; lower the value if the expected documents disappear; until then mark 5.4 as provisional (untick or annotate).
  - Strength: Cheap, direct evidence on the deployed value.
  - Tradeoff: Another deploy if the value changes.
  - Confidence: HIGH — the browser checks already exist from this session.
  - Blind spot: Order-only checks still do not give `F`/`E`; the scoring query does.
- **Decision**: FIXED — Progress 5.4 unticked until 0.70 is checked on Azure after PR #46 deploys; queued in follow-ups/review-fixes.md.

### F3 — Cold-start latency not measured, yet 5.5 is ticked

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Success Criteria
- **Location**: context/changes/semantic-search-with-fragments/plan.md (Progress 5.5); context/foundation/infrastructure.md:61
- **Detail**: Row 5.5 requires the cold-start time noted against the B1 trigger, and infrastructure.md says the cold-start search latency decides trigger (2). Only warm latency was recorded (201–280 ms; 664 ms first search of a session); the note says the cold start was not measured.
- **Fix**: Measure one search after F1 idle time (about 20 minutes) and record it in the calibration note and infrastructure.md.
- **Decision**: FIXED — Progress 5.5 unticked until a cold-start search is measured and recorded; queued in follow-ups/review-fixes.md.

### F4 — "Stored chunks stay searchable" is overstated

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: docs/deployment.md:127; docs/sop/operate-semantic-search.md:109, :128; infra/modules/app-service.bicep:145
- **Detail**: Search matches only chunks whose `run_generation` equals the document's current `published_run_generation` (services/api/Search/OwnedSearch.cs:105). A document re-read while the worker is paused, or whose re-embedding failed after a re-read, drops out of search until embedded again. Rename-only requeues do stay searchable.
- **Fix**: Reword to "chunks of documents whose text has not changed stay searchable; a re-read document is searchable again once embedded".
- **Decision**: FIXED — deployment.md, the SOP and the app-service.bicep comment now say a re-read document drops out of search until re-embedded; rename-only requeues stay searchable.

### F5 — Token rate can exceed 40K per minute

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: services/api/DocumentEmbedding/EmbeddingTokenPacer.cs:21; docs/architecture.md:92; docs/deployment.md:126; docs/local-development.md:78; context/foundation/infrastructure.md:62
- **Detail**: The bucket starts full and refills continuously, so up to about 80K estimated tokens can go out in the first minute after startup (deployment limit 50K). During a deploy the old and new containers also run side by side with separate in-memory pacers and search limiters, doubling both limits for that window. Retries absorb the resulting 429s, but the docs say "at most 40,000 per minute".
- **Fix**: Start the bucket empty (or document the burst) and add one sentence on deploy overlap to the SOP pacing paragraph.
- **Decision**: FIXED (docs) — 40,000 described as the sustained rate with a one-minute startup burst; deploy overlap noted in the SOP and deployment.md.

### F6 — Stale or incomplete doc claims

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: docs/architecture.md:90, :97; docs/testing.md:24-26; README.md:73, :75, :79; docs/deployment.md:136-140
- **Detail**: (a) Rename "re-queues that animal's read documents" — the code selects `published_run_generation IS NOT NULL`, including documents being re-read. (b) 503 also follows invalid `Search:*` settings, not only provider failure. (c) The query is trimmed, not embedded "as typed". (d) testing.md omits `EmbeddingTokenPacerTests` and `EmbeddingAvailabilityTests`. (e) README's test sentence omits embedding and search isolation; its smoke description omits anonymous `POST /api/search`. (f) README and deployment.md rollback sections do not mention embedding tables or point to the SOP rollback note.
- **Fix**: Correct each line against the code.
- **Decision**: FIXED — rename wording, 503 on invalid Search settings, trimmed query, new tests in testing.md, README test/smoke/rollback lines and a deployment.md rollback paragraph.

### F7 — SOP backfill query disagrees with the in-app count

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: docs/sop/operate-semantic-search.md:97-103 vs services/api/Search/OwnedSearch.cs:132-146
- **Detail**: "Backfill progress" filters on `r.state = 'read'` and ignores `storage_state`; `notSearchableCount` uses `published_run_generation` over stored documents. A document being re-read with a stale embedding appears in the app count but not in the SOP query, so the calibration prerequisite "returns 0" can pass while owners still see documents being processed.
- **Fix**: Align the SOP predicate with `CountNotSearchableAsync` (without the owner filter).
- **Decision**: FIXED — SOP backfill query aligned with CountNotSearchableAsync (stored documents, published generation); checked against a local pgvector database.

### F8 — Retry-After cap applied only to embedding

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: services/api/DocumentReading/AzureDocumentTextReader.cs:338 vs services/api/DocumentEmbedding/AzureOpenAiEmbeddingClient.cs:230-240
- **Detail**: The one-hour cap was added for embedding only; the reading client still honours an unbounded provider `Retry-After`, the same parking risk the previous review fixed for embedding. Outside this change's files.
- **Fix**: Apply the same cap in the reading client in a follow-up change.
- **Decision**: DEFERRED — queued in follow-ups/review-fixes.md (outside this change's files).

### F9 — New-environment rollout no longer staged

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: docs/deployment.md:118-122; infra/modules/app-service.bicep:146-147
- **Detail**: With `Embedding__WorkerEnabled=true` in Bicep, a new environment deployed before the role grant fails embedding jobs with `provider_auth` and needs a requeue; the plan's S-03-style order (worker off → grant → enable) is replaced by "grant first or requeue". Works and is documented; recorded as accepted drift. The roadmap still shows S-04 `in-progress`, which `/10x-archive` flips.
- **Fix**: None required; keep as documented.
- **Decision**: ACCEPTED — grant-first-or-requeue rollout is documented in docs/deployment.md; the roadmap flips S-04 at archive.
