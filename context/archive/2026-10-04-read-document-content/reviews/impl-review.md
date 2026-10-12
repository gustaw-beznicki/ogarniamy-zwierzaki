<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Read document content

- **Plan**: context/changes/read-document-content/plan.md
- **Scope**: Full plan
- **Reviewed phases**: 1, 2, 3, 4, 5
- **Date**: 2026-10-12
- **Verdict**: NEEDS ATTENTION (all findings fixed in triage, 2026-10-12)
- **Findings**: 0 critical, 4 warnings, 5 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | WARNING |
| Scope Discipline | WARNING |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | WARNING |
| Success Criteria | WARNING |

Success criteria at c73ebb6: `dotnet build` (0 errors), `dotnet test` (217/217), `npm run check` (0 errors, 0 warnings), `npm run build` and `infra/deploy.sh lint` pass. `infra/deploy.sh what-if` was not re-run (needs an Azure session; 5.2 evidence is in notes). All manual items are ticked; see F7.

The phase 3 review (impl-review-phase-3.md) remains valid: F1–F5 fixes are present, F6 is documented as transient. Its deferred F7 and F8 reappear here as F5 and F6.

## Findings

### F1 — `SELECT r.*` in hand-written cleanup query

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: services/api/DocumentReading/DocumentReadingJobs.cs:361
- **Detail**: `ListDueCleanupsAsync` uses `FromSql($"SELECT r.* FROM document_reading_run_files AS r JOIN document_reading_jobs ...")`. AGENTS.md forbids `SELECT *` in hand-written SQL (rule added in dbb27eb, after this code landed) and prefers LINQ when no row lock is needed. The query takes no lock and is the only `SELECT *` in `services/`.
- **Fix**: Rewrite as LINQ (join or `Any()` on `DocumentReadingJobs` with the same stage/generation/state filter), projecting straight to `DocumentReadingCleanupWork`; existing cleanup tests cover it.
- **Decision**: FIXED 2026-10-12: rewritten as LINQ with an `Any()` subquery on jobs (`DateTimeOffset.UtcNow` translates to `now()`); build clean, 59 DocumentReading tests pass.

### F2 — Local parse copied into every OCR run file and never cleared

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: services/api/DocumentReading/DocumentReadingWorker.cs:383-384, services/api/DocumentReading/DocumentReadingJobs.cs:87-92
- **Detail**: Each OCR submission for a PDF serializes the whole local parse (up to 24 MiB) into the new run file's `fetched_text` (review F3 fix). An abandoned attempt (transient failure, missing result) makes the next attempt save another full copy. `ListRunFilesAsync` loads every run file of the generation with `FetchedText` on each claim, so memory per claim grows with attempts (up to 5 plus abandoned files) on F1, next to the 256 MiB child. Failed/abandoned run files keep this medical text indefinitely; only `Retrieved` overwrites it.
- **Fix A ⭐ Recommended**: Load only what is needed (project run files without `FetchedText`, then fetch the latest checkpoint row) and null `fetched_text` on run files that are abandoned or failed once a newer checkpoint exists.
  - Strength: Bounds memory to one checkpoint per file and removes stale text copies, without a schema change.
  - Tradeoff: Touches the resume path that 3.3/3.4 verified; needs a test for the copy-forward and clearing.
  - Confidence: HIGH — copy and full load verified in code.
  - Blind spot: Real sizes are small today (synthetic documents); the risk is large text PDFs retried several times.
- **Fix B**: Record as an accepted limitation in the runbook and revisit if run-file sizes grow.
  - Strength: No code change after deployment acceptance.
  - Tradeoff: Medical text duplicates stay in the database with no cleanup path.
  - Confidence: MED — depends on how often PDFs need OCR retries.
  - Blind spot: No measurement of current `fetched_text` sizes in production.
- **Decision**: FIXED 2026-10-12 via Fix A (variant): `StartRunFileAsync` drops earlier saved parses of the same original and generation when it carries one forward, and `PublishAsync`/`FailAsync` drop all saved parses of the document in their transaction, so at most one copy per original is stored and loaded per claim (the load itself is unchanged). Retrieved output is kept as the plan specifies. New repository test; 218 tests pass.

### F3 — No account-wide or global CPU budget for reading on F1

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: services/api/DocumentReading/PdfTextReader.cs:11, services/api/DocumentReading/OwnedDocumentReading.cs (MaxOwnerRetries)
- **Detail**: The local parse may use up to 60 s of CPU per run generation; Retry is limited only to 3/hour per document and uploads are not rate-limited anywhere in `services/api`. About 60 hard PDFs (uploads or Retries spread across documents) can exhaust the F1 quota of 60 CPU minutes per day, which stops the app and makes originals unavailable. The plan amendment names exactly this as a guardrail break. Today's exposure is limited to signed-in owners (single-user MVP); measured usage is about 1 CPU minute/day.
- **Fix A ⭐ Recommended**: Document it as an accepted limitation in the runbook's F1 section with a B1 trigger note, and queue a per-account upload/Retry rate limit for a later change.
  - Strength: Matches current exposure (authenticated, one owner) and the owner's cost decision.
  - Tradeoff: The invariant stays protected only by trust in signed-in users.
  - Confidence: MED — risk grows when more accounts exist.
  - Blind spot: Real CPU cost of a worst-case 60 s parse on F1 not measured.
- **Fix B**: Add a daily parse/OCR CPU budget that pauses the worker (not the app) when reached.
  - Strength: Protects original retrieval regardless of who uploads.
  - Tradeoff: New persistent counter and worker state; more code after acceptance.
  - Confidence: MED — design straightforward but untested on F1.
  - Blind spot: Process CPU measurement inside the child vs. App Service quota accounting may differ.
- **Decision**: FIXED 2026-10-12 via Fix A: documented as a known limitation in docs/deployment.md (CPU quota) and the runbook; follow-up parked in roadmap as `reading-cpu-budget`.

### F4 — Operations docs stale and the runbook lives only in the change folder

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Plan Adherence
- **Location**: docs/deployment.md:23, docs/local-development.md:105, infra/modules/app-service.bicep:110, context/changes/read-document-content/notes/reading-runbook.md:281-283
- **Detail**: `docs/deployment.md:23` still says app-service.bicep keeps `WorkerEnabled` at `false` (it is `true` since 331bb1b). `docs/local-development.md:105` and runbook lines 281-283 say the worker starts with cleanup on start, without the 60 s startup delay added in faad5af. The Bicep comment and the failure-code table, inspection queries and pause/recovery steps exist only in `notes/reading-runbook.md`, which moves to `context/archive/` on `/10x-archive` (and the plan asked to document job states, inspection and paused-worker recovery as project docs).
- **Fix A ⭐ Recommended**: Fix the stale lines, move the durable operator reference (job states, failure codes, inspection queries, pause/resume, F1 sleep/quota) into `docs/` and point the Bicep comment there; keep the incident/evidence log in notes.
  - Strength: Operational knowledge survives archiving; README/docs stay documentation per lessons.md.
  - Tradeoff: A docs move of moderate size before archive.
  - Confidence: HIGH — stale lines verified against current code and Bicep.
  - Blind spot: None significant.
- **Fix B**: Fix only the three stale statements and repoint the Bicep comment to docs/deployment.md.
  - Strength: Minimal change.
  - Tradeoff: Failure codes and inspection queries end up only in the archive.
  - Confidence: HIGH.
  - Blind spot: Future operators may not look in context/archive.
- **Decision**: FIXED 2026-10-12 via Fix A: operating procedures moved to new `docs/sop/operate-document-reading.md` (linked from README and docs/deployment.md, startup delay and saved-parse behaviour included); stale `WorkerEnabled=false` (deployment.md) and startup wording (local-development.md) fixed; Bicep comment points to the SOP; runbook keeps only the 5.4/5.5 evidence and incident record. `infra/deploy.sh lint` passes.

### F5 — Idle worker loop polls every 5 s and runs cleanup serially before claims

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: services/api/DocumentReading/DocumentReadingWorker.cs:71, 112-141; services/api/DocumentReading/DocumentReadingCleanup.cs:12, 24-40
- **Detail**: Phase 3 review F7 was deferred and not addressed: while awake the worker makes two DB round-trips every 5 s with nothing due (measured about 3-4 CPU min/day, runbook:365). Cleanup runs up to 20 sequential deletions (30 s timeout each) before every claim, so a provider outage can delay claims by up to ~10 minutes per pass.
- **Fix**: Back off the idle interval (5 s → 60 s, reset after work or an in-process signal on completion/Retry) and time-box the cleanup pass.
- **Decision**: FIXED 2026-10-12: idle wait doubles from 5 s to 60 s and resets after a claim; new `DocumentReadingWakeUp` singleton is notified by capture completion and an accepted Retry; a cleanup pass starts no new deletion after 30 s. New hosted-loop test (verified to fail without the wake-up); docs/architecture.md and the SOP updated; 219 tests pass.

### F6 — No worker test for an ambiguous OCR submission

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Success Criteria
- **Location**: services/api.Tests/DocumentReadingWorkerTests.cs
- **Detail**: Remainder of phase 3 review F8: Retry-After clock and hosted-loop start are now tested, but no test drives a submission whose outcome is unknown (timeout/HttpRequestException leaving a Pending run file with no operation ID) through abandonment, the sanitized warning, no cleanup obligation and resubmission. The plan's bounded-retention limitation rests on this path.
- **Fix**: Add one worker test with the fake provider timing out on Analyze, then succeeding.
- **Decision**: FIXED 2026-10-12: `A_submission_with_an_unknown_outcome_is_abandoned_without_a_cleanup_obligation_and_sent_again` (lost response on Analyze → Pending without ID → abandoned with the warning, no cleanup, resubmitted, read; only the known result deleted).

### F7 — 5.4 ticked with the idle wake-up only partially observed

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Success Criteria
- **Location**: context/changes/read-document-content/notes/reading-runbook.md:350; plan.md Progress 4.3, 5.4
- **Detail**: The runbook marks "F1 wake-up after idle unload" as PARTIAL (startup recovery only, "still to be observed"); 4.3 notes the 10-minute polling stop was not exercised. Both are disclosed in the Progress annotations and accepted by the owner, so this is not a silent tick, but the evidence gap remains.
- **Fix**: Observe one idle unload and wake-up on F1 and record it in the runbook evidence table (or record the gap as accepted in the evidence row).
- **Decision**: FIXED 2026-10-12: gap recorded as accepted in the runbook's F1 wake-up evidence row (idle unload/wake-up and the 10-minute polling stop not exercised by hand).

### F8 — Singleton OCR adapter pins one HttpClient for the process lifetime

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: services/api/DocumentReading/DocumentReadingSetup.cs:31-39
- **Detail**: `AzureDocumentTextReader` is a singleton holding one `HttpClient` from `IHttpClientFactory.CreateClient`, so handler rotation never happens and DNS changes of the endpoint are not picked up until restart. F1 restarts often, so the practical effect is small.
- **Fix**: Configure the named client's primary handler as `SocketsHttpHandler { PooledConnectionLifetime = 5 min }`.
- **Decision**: FIXED 2026-10-12: named client's primary handler is `SocketsHttpHandler` with `PooledConnectionLifetime` 5 min and cookies off. Owner asked whether the shared client could mix data between people: no, it is used only by the worker/cleanup under the API's one service identity, sets token and body per request, and owner isolation is enforced by the status/Retry queries and lease-fenced writes.

### F9 — Worker startup delay not recorded in the plan

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Scope Discipline
- **Location**: services/api/DocumentReading/DocumentReadingWorker.cs:83-86, 101-110
- **Detail**: faad5af added a 60 s `StartupDelay` before the first claim after an F1 deploy 502. It is justified, tested and in docs/architecture.md and docs/deployment.md, but the plan (Phase 3 worker defaults) does not mention it, and it lengthens F1 wake-up latency by about a minute.
- **Fix**: Add a short plan addendum recording the startup delay and its reason.
- **Decision**: FIXED 2026-10-12: "Plan addendum 2026-10-12" in plan.md records the startup delay and the F2/F4/F5 changes.
