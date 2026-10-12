# SOP: Operate document reading

**Purpose**: understand, inspect, pause, resume and roll back background document reading (OCR) in Azure.
**When**: after a reading incident (provider outage, configuration error, unexpected CPU use), when owners report documents stuck in "queued" or "failed", and during a rollback.
**Safety**: every inspection step is read-only and returns no document content. Every step that changes Azure (app settings, deployments, database rows, firewall or role assignments) needs the owner's explicit approval.

A document whose reading status is "read" has its text stored with page provenance; nothing searches it yet.

## Ground rules

- Inspect and record **IDs, states, codes, counts and timings only.** Never select, copy or paste `fetched_text`,
  `document_text_pages.text`, file names (`document_files.original_name`), blob keys of a real owner's document, provider
  payloads, tokens or connection strings. The SQL below is written so that it cannot return content.
- Reading never decides whether an original is available. A reading outage, a paused worker or a misconfigured provider
  leaves capture, document lists and original downloads working, and `/api/health` still checks only the database.
- Only an owner's Retry or a separately approved SQL change queues a failed job again. Never delete jobs, run files,
  text pages or originals to "fix" reading.

## What runs where

- The worker is a `BackgroundService` inside the API process on the App Service (no WebJob, no queue service). It runs
  only when `DocumentReading:Auth` resolves to a working setup **and** `DocumentReading:WorkerEnabled` is `true`.
  Otherwise it logs one line at startup and stays off.
- Concurrency is one job at a time per API instance. The F1 plan has one instance.
- Local PDF text is read by a child process: the API's own assembly started with the hidden `pdf-text-helper` command.
  It ships inside the API deployment (there is no separate binary or package to deploy) and runs with a 60-second
  timeout, a 256 MiB GC heap limit, one processor and an allowlisted environment (no connection strings or managed
  identity variables). Its output is capped at 24 MiB; more falls back to OCR.
- OCR is Azure Document Intelligence `prebuilt-read`, API `2024-11-30`, called with the API's managed identity. The
  original bytes are sent in the request; there is no public URL or SAS. Analyze requests are never retried
  automatically by the HTTP client.

## Job states

One row per Stored document in `document_reading_jobs` (primary key `document_id`).

| State | Meaning | Leaves it when |
| --- | --- | --- |
| `queued` | Waiting for a worker, due at `due_at`. New captures and the migration backfill start here. | A worker claims it (`reading`). |
| `reading` | Claimed with a lease (`lease_token`, `lease_expires_at`, 2 minutes, renewed every 30 seconds). | Published (`read`), failed (`failed`), requeued after a transient failure (`queued`), or reclaimed by a worker once the lease has expired (stays `reading`, new token, attempt counted). |
| `read` | A complete text revision is published; `published_run_generation = run_generation`. | Never by itself. A repeated capture completion leaves it unchanged. |
| `failed` | Stopped with a sanitized `failure_code`. | Owner Retry (or an approved operator requeue): back to `queued` in a new `run_generation` with `attempt_count = 0`. |

Counters and limits:

- `attempt_count` is incremented by every claim, including a reclaim after a crash. A claim beyond 5 attempts fails at
  once with `attempts_exhausted`, before any work. A graceful shutdown (app restart, F1 unload with notice) hands the
  current attempt back and requeues the job immediately.
- Transient failures are retried after 30 s, 2 min, 10 min and 30 min (a longer provider `Retry-After` wins). A
  transient failure on the fifth attempt fails the job with that transient code.
- One attempt may run for 30 minutes (`attempt_deadline_exceeded` after that).
- Owner Retry: 3 accepted retries per document per hour (`owner_retry_count`, `owner_retry_window_started_at`); a fourth
  answers 429 `retry_limited` with the time it becomes possible again. Retry of a `queued`, `reading` or `read` job is a
  no-op.

### Run files and provider cleanup

`document_reading_run_files` holds one row per original per run generation.

| `stage` | Meaning |
| --- | --- |
| `pending` | Started, nothing confirmed as submitted. A submission whose response was lost stays here and is abandoned by the next attempt. |
| `submitted` | Submitted to the provider; `provider_operation_id` is saved and polled again after a restart instead of resubmitting. |
| `retrieved` | Output checkpointed (`fetched_text`); never fetched twice. |
| `failed` | Abandoned (terminal failure, lost lease or unknown submission outcome). Never resumed; only its cleanup continues. |

A `pending` or `submitted` PDF run file may also hold the saved local parse in `fetched_text`, so a later attempt does
not parse again. Only the latest copy per original is kept, and it is dropped when the job is read or failed.

| `cleanup_state` | Meaning |
| --- | --- |
| `not_needed` | Nothing was sent to the provider (for example a PDF read entirely from its text layer). |
| `pending` | A provider result may exist; deletion is due at `next_cleanup_at`. Retried after 1 min, 5 min, 30 min, then every 2 hours (`cleanup_attempt_count`). |
| `acknowledged` | The provider answered 204 to the deletion, or an authenticated `NotFound` for the result. A deletion request, not proof of physical erasure (the provider answers 204 even to a DELETE of a result that no longer exists). |

Cleanup is independent of the job: a failed, retried or read job keeps its pending cleanups. A submission whose response
was lost before the operation ID was saved has no known result ID; it cannot be deleted early and relies on the
provider's bounded retention (up to 24 hours). That is the documented retention limitation.

## Failure codes

Classes: **transient** is retried automatically (up to 5 attempts), **terminal** fails the job at once,
**configuration** fails the job at once with a warning that names the settings to check. Every failed job can be
retried by its owner.

| Code | Class | Meaning | What to do |
| --- | --- | --- | --- |
| `provider_unavailable` | transient | Throttling (429), provider 5xx, network error or timeout. | Nothing; check provider health if it persists. |
| `provider_result_missing` | transient | A saved operation's result is gone (expired or deleted); the run file is abandoned and the next attempt submits again. | Nothing. |
| `storage_unavailable` | transient | Blob Storage could not be reached while reading the original. | Check the storage role and Blob Storage health; originals have the same dependency. |
| `attempt_deadline_exceeded` | transient | One attempt ran longer than 30 minutes. **Kept transient on purpose**: it is more likely provider slowness than document size. Real size limits are separate terminal codes. | Nothing for one occurrence. If one document keeps hitting it until it fails, look at provider latency in the logs before treating it as a size problem. |
| `unexpected_error` | transient | An unclassified exception; only its type is logged. | Look up the exception type in the log; repeated occurrences are a bug. |
| `pdf_unreadable` | transient (rare) | Stored only when a PDF parses differently on resume than when its OCR pages were submitted. An ordinary local parser failure is not stored: the file goes to whole-file OCR instead. | Nothing. |
| `encrypted_pdf` | terminal | Password-protected PDF. | Owner keeps the original; reading is not possible. |
| `page_limit_exceeded` | terminal | More than 2,000 PDF pages. | None; nothing is truncated. |
| `text_limit_exceeded` | terminal | More than 10,000,000 extracted characters. | None. |
| `result_limit_exceeded` | terminal | The OCR result JSON exceeded 64 MiB, or the provider rejected the content length. | None. |
| `unsupported_image_dimensions` | terminal | The provider rejected the image size. | None; the original stays openable. |
| `unreadable_content` | terminal | The provider could not read the file content. | None. |
| `empty_document` | terminal | No text on any page of any original. | None. |
| `original_unavailable` | terminal | The original's blob is missing. | Follow [Recover document originals](recover-originals.md); then the owner retries. |
| `provider_rejected`, `provider_protocol_error` | terminal | The provider failed the analysis or answered in an unexpected shape. | Check the API version and model; repeated occurrences need investigation. |
| `attempts_exhausted` | terminal | Claimed for a sixth time: earlier attempts ended without handing the job back, typically a process crash while reading it. Stops crash loops from burning the F1 CPU quota. | Treat as a bug report for that document ID; do not retry repeatedly. |
| `provider_auth` | configuration | No token for the identity, or 401/403 from the provider: missing Cognitive Services User role or wrong identity. | Check the role assignment ([deployment guide](../deployment.md), step 3 of setting up a new environment), then requeue (below). |
| `provider_config` | configuration | 404 or similar: wrong endpoint, model or API version, or reading unavailable. | Check `DocumentReading__Endpoint`, `ModelId`, `ApiVersion`, then requeue. |

## Configuration

The keys and defaults are documented in the [local development guide](../local-development.md) (Configuration). In Azure they are App Service
settings (`DocumentReading__Auth`, `__Endpoint`, `__ModelId`, `__ApiVersion`, `__WorkerEnabled`) set by
`infra/modules/app-service.bicep`, where `WorkerEnabled` is `true` (the worker is on in production).

- Every approved deployment rewrites the App Service settings from Bicep. A value changed by hand with
  `az webapp config appsettings set` lasts only until the next deployment. Use it for an emergency pause, then make the
  durable change in Bicep through a pull request.
- Changing any app setting restarts the API (a graceful shutdown: the current job is requeued without counting the
  attempt).
- The startup log line states the result: "Document reading is available: AzureManagedIdentity against ... Worker
  enabled.", "Document reading is disabled: ..." or the warning "Document reading is unavailable: <reason> Originals are
  still stored and opened, but no document is read."

## Inspecting reading

### From logs (no database access)

`az webapp log tail --resource-group rg-ogarniamy-mvp --name <api app>` or the portal Log stream. Useful lines (all
carry IDs, codes and timings only):

- `The document reading worker started.` / `The document reading worker is not running: <reason>`
- `Reading document <id>: run <n>, attempt <n>.` then `Document <id> read in <ms> ms.`
- `Reading document <id> failed transiently (<code>) after <ms> ms on attempt <n>; retrying in <delay>.`
- `Reading document <id> failed (<code>) ...` and the configuration warning naming the settings to check.
- `Submitted file <fileId> of document <id> for OCR as run file <runFileId>.`, `Resuming saved OCR operation ...`
- `Provider acknowledged result deletion for run file <runFileId> ...` / `Provider result deletion ... failed (<code>); retrying in <delay>.`
- `Lost the lease on document <id> ...`, `Document <id> is left to lease expiry at shutdown ...`

### From the database

Access: locally `docker compose exec postgres psql -U postgres -d ogarniamy`. In Azure only the API's managed identity
can sign in; querying needs a temporary, separately approved access path, removed afterwards (as in
[Check incomplete uploads](check-incomplete-uploads.md)). All queries below are read-only and return no content.

Overview by state:

```sql
SELECT state, count(*) AS jobs, min(due_at) AS oldest_due, max(attempt_count) AS max_attempts
FROM document_reading_jobs
GROUP BY state
ORDER BY state;
```

Due work the worker should pick up on its next pass (queued and due, or reading with an expired lease):

```sql
SELECT document_id, state, run_generation, attempt_count, due_at, lease_expires_at, updated_at
FROM document_reading_jobs
WHERE (state = 'queued' AND due_at <= now()) OR (state = 'reading' AND lease_expires_at <= now())
ORDER BY due_at, document_id;
```

Rows here while the app is awake and the worker is enabled mean the worker is stuck or off; while F1 sleeps they are
expected.

Failed jobs by code:

```sql
SELECT failure_code, count(*) AS jobs, min(completed_at) AS first, max(completed_at) AS last
FROM document_reading_jobs
WHERE state = 'failed'
GROUP BY failure_code
ORDER BY jobs DESC;
```

Jobs close to the attempt budget (possible crash loop):

```sql
SELECT document_id, state, run_generation, attempt_count, due_at, updated_at
FROM document_reading_jobs
WHERE attempt_count >= 3 AND state IN ('queued', 'reading')
ORDER BY attempt_count DESC, updated_at;
```

Pending provider cleanup (due now, and overdue by more than an hour):

```sql
SELECT count(*) FILTER (WHERE next_cleanup_at <= now()) AS due_now,
       count(*) FILTER (WHERE next_cleanup_at <= now() - interval '1 hour') AS overdue_1h,
       count(*) AS pending_total,
       max(cleanup_attempt_count) AS max_cleanup_attempts,
       min(created_at) AS oldest
FROM document_reading_run_files
WHERE cleanup_state = 'pending';
```

```sql
SELECT id AS run_file_id, document_id, run_generation, stage, cleanup_attempt_count, next_cleanup_at, updated_at
FROM document_reading_run_files
WHERE cleanup_state = 'pending'
ORDER BY next_cleanup_at
LIMIT 50;
```

Provider operation IDs are internal identifiers, not content, but there is no reason to copy them into notes.

Submissions with an unknown outcome (abandoned `pending` run files without an operation ID; each one may be a provider
analysis that can only expire):

```sql
SELECT count(*) AS unknown_submissions, max(updated_at) AS latest
FROM document_reading_run_files
WHERE stage IN ('pending', 'failed') AND provider_operation_id IS NULL AND cleanup_state = 'not_needed'
  AND updated_at < now() - interval '1 hour';
```

This also counts run files that never reached the provider (for example PDFs read locally that then failed), so treat
it as an upper bound.

Stored documents without a reading job (should be zero; a release from before document reading running against this
schema would create them):

```sql
SELECT count(*) AS stored_without_job
FROM documents d
WHERE d.storage_state = 'stored'
  AND NOT EXISTS (SELECT 1 FROM document_reading_jobs j WHERE j.document_id = d.id);
```

## Pausing and resuming the worker

**Pause** (provider incident, unexpected CPU use, suspected bad release):

1. Set `DocumentReading__WorkerEnabled=false` (approved): `az webapp config appsettings set --resource-group
   rg-ogarniamy-mvp --name <api app> --settings DocumentReading__WorkerEnabled=false`. The API restarts; the current
   job is requeued without counting the attempt.
2. Confirm the startup line "The document reading worker is not running: DocumentReading:WorkerEnabled is false."
3. To keep the pause across deployments, commit `'false'` for `DocumentReading__WorkerEnabled` in
   `infra/modules/app-service.bicep` through a pull request.

While paused: new captures still get `queued` jobs, owners see "queued", Retry still requeues failed jobs, and **provider
cleanup also stops** (it runs in the worker loop). Pending deletions then rely on the provider's 24-hour retention until
the worker resumes, so keep pauses short when cleanup is pending.

**Resume**: set `DocumentReading__WorkerEnabled=true` through the approved release (Bicep) or, temporarily, the same
`az webapp config appsettings set` command. The worker waits 60 seconds after the app starts, then runs due cleanup
and claims due and lease-expired jobs one at a time. Check the overview query until no due work remains.

**Jobs that failed during the incident** stay failed. Owners can press Retry. For many jobs failed with a configuration
code after the cause is fixed, an operator requeue is a separately approved database change that mirrors Retry without
touching the owner's retry counters:

```sql
-- Approved mutation only. Review the count first with the "Failed jobs by code" query.
UPDATE document_reading_jobs
SET state = 'queued', run_generation = run_generation + 1, attempt_count = 0, due_at = now(),
    failure_code = NULL, completed_at = NULL, updated_at = now()
WHERE state = 'failed' AND failure_code IN ('provider_auth', 'provider_config');
```

Pending cleanups of the old generation are kept.

**Forward reconciliation** after a release from before document reading stored documents without jobs (approved mutation, the same statement
the `DocumentReading` migration ran as its backfill; conflict-safe):

```sql
INSERT INTO document_reading_jobs (document_id, state, run_generation, due_at, attempt_count,
    owner_retry_count, created_at, updated_at)
SELECT id, 'queued', 1, now(), 0, 0, now(), now()
FROM documents
WHERE storage_state = 'stored'
ON CONFLICT (document_id) DO NOTHING;
```

## F1 sleep and CPU quota

The API runs on App Service F1: no Always On, a daily CPU quota of 60 CPU minutes, and the
app unloads after idle time.

- **Sleep.** When F1 unloads the app, nothing runs: new jobs, transient retries, provider cleanup and backfill wait until
  the next request wakes it (any page that calls `/api/*`, including the smoke test's `/api/health`). The worker waits 60 seconds
  after the app starts (so a deploy or restart, which runs old and new containers side by side, is not slowed down by
  reading), then runs due cleanup first, then due and lease-expired jobs. A job interrupted by an unload without notice is reclaimed
  after its 2-minute lease expires and counts as an attempt. Owners may see "queued" for a long time on a quiet day;
  that is the documented limitation, not an outage.
- **CPU quota.** Exceeding 60 CPU minutes in a day stops the app until the quota resets, which also makes originals
  unavailable. Guards: one job at a time, a 60-second, single-processor local parse that is saved and reused across
  attempts, and the 5-attempt budget that stops crash loops.
  Known limitation: there is no account-wide or daily budget. Retry is limited per
  document only and uploads are not rate-limited, so about 60 hard PDFs a day could exhaust the quota. Pause the
  worker if CPU time climbs unexpectedly.
- **Idle polling.** While awake and idle, the worker checks for due cleanup and jobs every 5 seconds at first, doubling
  the wait up to once a minute. A completed capture or an accepted Retry wakes it at once, and any claimed job resets
  the wait. A cleanup pass starts no new deletion after 30 seconds, so an unreachable provider delays the next claim
  by about a minute at most.
- **What to watch**, daily for the first week after activation, then weekly:
  - CPU time: the app's Quotas page in the portal (CPU (Day)), or `az monitor metrics list --resource <api app resource
    id> --metric CpuTime --interval PT1H --aggregation Total --offset 1d` (seconds per hour; sum for the day).
  - The overview and due-work queries above (a backlog that survives wake-ups means the worker is not keeping up).
  - Restarts or "quota exceeded" events in the App Service activity log.
- **B1 trigger.** Move to B1 (with Always On) when either holds: daily CPU time reaches about 45 CPU minutes (75 % of the
  quota) on more than one day in a week, or the quota is hit once; or cold starts break the about-two-second search
  target once search exists. The B1 switch is a separate, approved
  infrastructure change.

## Rollback

- **Stop reading only**: pause the worker (above). Capture and originals are unaffected.
- **Code rollback**: revert the release on `main` and approve the deploy, or re-run an earlier successful deploy. Keep
  the schema, `document_reading_jobs`, `document_reading_run_files` (with their cleanup obligations),
  `document_text_pages`, the Document Intelligence account, its role assignment and every original. A release from before
  document reading ignores the reading tables; documents it stores have no job and are reconciled forward (above) before reading is
  enabled again.
- Never drop the reading tables or delete text, jobs or run files as part of a rollback; pending cleanups would be lost
  and could not be retried.
- Original retrieval does not depend on reading: with reading `Disabled`, misconfigured or paused, originals open
  normally.
