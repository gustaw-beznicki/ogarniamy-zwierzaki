<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Read document content

- **Plan**: context/changes/read-document-content/plan.md
- **Scope**: Phase 3 of 5
- **Reviewed phases**: 3
- **Date**: 2026-10-06
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 4 warnings, 4 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | WARNING |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

Success criteria: `dotnet build` (0 warnings) and `dotnet test` (185/185) pass at 95c23b2. Manual 3.4 (live provider run) is pending, as expected.

## Findings

### F1 — Attempt budget: crash loops are unbounded, platform interruptions consume it

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: services/api/DocumentReading/DocumentReadingJobs.cs:33-43, services/api/DocumentReading/DocumentReadingWorker.cs:105, 546, 585-597
- **Detail**: `ClaimNextAsync` re-claims lease-expired `reading` jobs and increments `attempt_count` without an upper bound; `MaxAttempts` is checked only in `HandleFailureAsync`. A job that kills the process (OOM, native crash) is re-claimed on every F1 wake-up forever, burning the daily CPU quota (whose exhaustion stops the app and its originals). Conversely, the graceful shutdown requeue and lease-expiry reclaims after F1 sleep/recycle keep the incremented count, so platform interruptions eat into the five-attempt budget meant for real transient failures.
- **Fix**: At the start of `RunOnceAsync`, fail a claim whose `AttemptCount > MaxAttempts` with a terminal `attempts_exhausted` code before any work; make the shutdown requeue not count the attempt (`RequeueAsync(..., countAttempt: false)` decrementing `attempt_count`).
  - Strength: Bounds crash loops on F1 while keeping graceful interruptions free; both changes stay inside the existing lease-fenced repository methods.
  - Tradeoff: A job interrupted by crashes (not graceful shutdown) still counts each crash, which is the intended safety bound.
  - Confidence: HIGH — the claim SQL and the single `MaxAttempts` check were verified in code.
  - Blind spot: F1 recycles may not always deliver a graceful shutdown; those still count.
- **Decision**: FIXED 2026-10-11: a claim beyond MaxAttempts fails as `attempts_exhausted` before any work; the graceful shutdown requeue hands its attempt back (`countAttempt: false`).

### F2 — OCR result buffered up to 256 MiB in the API process

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: services/api/DocumentReading/AzureDocumentTextReader.cs:17, 279-312; services/api/DocumentReading/PdfTextReader.cs:193-198, 236-253
- **Detail**: `MaxResultBytes = 256 MiB`; `ParseCappedAsync` grows a MemoryStream, copies it with `ToArray()` and parses a JsonDocument, so peak memory can exceed 512 MiB in the API itself (not the heap-capped child). On F1 (about 1 GB shared with requests) a large legitimate result can OOM the API and make originals unavailable. The child's 48 MiB stdout is similarly buffered and copied, admitting far more than the 10M-character limit enforced later.
- **Fix**: Parse directly from the response/pipe stream through a length-limiting stream (`JsonDocument.ParseAsync` / `JsonSerializer.DeserializeAsync`), and lower both caps to values derived from the 2,000-page / 10M-character limits (for example 64 MiB OCR JSON, 24 MiB child output).
  - Strength: Removes the double copy and ties memory to the product limits that already define failure.
  - Tradeoff: Very verbose provider JSON for a legitimate large document could hit the lower cap and fail as `result_limit_exceeded`.
  - Confidence: MED — cap sizes need confirmation against real Read result sizes (3.4 live run).
  - Blind spot: Actual F1 memory headroom under load has not been measured.
- **Decision**: DEFERRED until manual 3.4: cap sizes are set from real result sizes observed there.

### F3 — Every retry or resume re-runs the local PDF parse

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: services/api/DocumentReading/DocumentReadingWorker.cs:276-318, 344-359
- **Detail**: A new attempt after a transient OCR error (abandoned Pending run file) parses the PDF again, and `ResumeAsync` with page ranges re-parses to merge local pages. A pathological PDF can cost about 5 CPU-minutes across attempts on F1; about a dozen such uploads in a day exhaust the 60 CPU-minute quota, stopping the app and its originals.
- **Fix A ⭐ Recommended**: Checkpoint the local page results (text plus the OCR page set) on the run file before submitting to OCR, and remember a parse timeout so later attempts go straight to whole-file OCR.
  - Strength: Each PDF is parsed at most once per document generation; uses the existing `FetchedText` checkpoint column, no schema change.
  - Tradeoff: Adds a checkpoint state for "local parsed, OCR pending" to the run-file flow and its tests.
  - Confidence: HIGH — the re-parse paths were confirmed in the worker.
  - Blind spot: Checkpoint size for large local-text PDFs (bounded by the 10M-character cap).
- **Fix B**: Keep re-parsing but add a daily local-parse CPU budget that defers further parses to the next day.
  - Strength: Simple global guard against quota exhaustion from any cause.
  - Tradeoff: Needs durable per-day accounting and delays legitimate work.
  - Confidence: MED — more moving parts than the checkpoint.
  - Blind spot: Interaction with Retry-After and the attempt budget.
- **Decision**: FIXED 2026-10-11 (Fix A): the local parse (or the parser failure) is saved on the OCR run file before submission and reused by later attempts and resumes of the same run generation; no schema change.

### F4 — Cleanup acknowledges any 404, including a misconfigured endpoint

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: services/api/DocumentReading/AzureDocumentTextReader.cs:83
- **Detail**: `DeleteResultAsync` treats any 404 (and 200) as acknowledged. Elsewhere the adapter treats 404 as `provider_config` (wrong endpoint/model). With a wrong endpoint or model, cleanup obligations are silently marked acknowledged. The plan requires an authenticated result-not-found.
- **Fix**: Acknowledge 404 only when the error body's code identifies a missing result (for example `NotFound`/`ModelNotFound` distinction per the v4 error schema); otherwise keep cleanup pending with backoff and log a configuration warning.
- **Decision**: FIXED 2026-10-11: only a 404 whose body code is `NotFound` is acknowledged; any other 404 keeps the cleanup pending with a configuration hint. Live check on 2026-10-11: the service answers 204 to DELETE of a missing result (even with a wrong model id) and `{"error":{"code":"404"}}` for a wrong path.

### F5 — Child environment uses a denylist

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: services/api/DocumentReading/PdfTextReader.cs:289-298
- **Detail**: Only `ConnectionStrings*`, `CUSTOMCONNSTR_`, `POSTGRESQLCONNSTR_` and `APPSETTING_` are removed. Managed-identity variables (`IDENTITY_ENDPOINT`, `IDENTITY_HEADER`, `MSI_*`) and other prefixes pass through, so a parser exploit in the child could mint tokens for Blob Storage or Document Intelligence.
- **Fix**: Clear the environment and add back an allowlist (`PATH`, `HOME`, `TMPDIR`, `DOTNET_ROOT`, the `DOTNET_*` limits).
- **Decision**: FIXED 2026-10-11: the child environment is cleared and rebuilt from an allowlist.

### F6 — Attempt deadline classified as transient

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: services/api/DocumentReading/DocumentReadFailure.cs:67
- **Detail**: `attempt_deadline_exceeded` (30-minute attempt deadline) is retried. The plan lists processing-limit errors as terminal; the deadline is more likely provider slowness than document size, so transient is defensible, but it is undocumented.
- **Fix**: Keep transient and document the choice in the Phase 5 runbook (or make it terminal if the owner reads the deadline as a processing limit).
- **Decision**: KEEP transient; document in the Phase 5 runbook.

### F7 — Idle polling every 5 s on F1

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: services/api/DocumentReading/DocumentReadingWorker.cs:61-101
- **Detail**: While awake, every idle tick runs a cleanup query and a claim query. Cheap, but steady CPU and database traffic against the F1 quota.
- **Fix**: Back off the idle poll (5 s growing to 60 s, reset after a claim) and run the cleanup listing every N ticks or after a job.
- **Decision**: DEFERRED (low impact; revisit with the Phase 5 F1 CPU measurement).

### F8 — Test gaps around startup and ambiguous submissions

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Success Criteria
- **Location**: services/api.Tests/DocumentReadingWorkerTests.cs
- **Detail**: No test drives the hosted loop's start (cleanup before claim, F1 wake-up behaviour) or the abandonment of an ambiguous Pending submission. Absolute-date Retry-After uses `DateTimeOffset.UtcNow` instead of the injected `TimeProvider`.
- **Fix**: Add the two tests and use `time.GetUtcNow()` for the Retry-After date.
- **Decision**: DEFERRED to Phase 4 test work.
