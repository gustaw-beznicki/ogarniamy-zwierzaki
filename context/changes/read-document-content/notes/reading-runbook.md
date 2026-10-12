# Document reading runbook (Phase 5)

Operating notes for background document reading on App Service F1: states, failure codes, configuration, inspection,
pausing and resuming the worker, F1 sleep and CPU quota, rollback, and the template for the post-deployment evidence
(manual checks 5.4 and 5.5).

Status: **written, not yet exercised in Azure.** Nothing in this file has been run against production. Every step that
changes Azure (app settings, deployments, database rows, firewall or role assignments) needs the owner's explicit
approval. The one-time rollout steps (Document Intelligence account, Cognitive Services User role) are in
[`infra-rollout.md`](infra-rollout.md); this runbook covers operation after them.

Semantic search (S-04) does not exist yet. A document whose reading status is "read" has its text stored with page
provenance; nothing searches it.

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

| `cleanup_state` | Meaning |
| --- | --- |
| `not_needed` | Nothing was sent to the provider (for example a PDF read entirely from its text layer). |
| `pending` | A provider result may exist; deletion is due at `next_cleanup_at`. Retried after 1 min, 5 min, 30 min, then every 2 hours (`cleanup_attempt_count`). |
| `acknowledged` | The provider answered 204 to the deletion, or an authenticated `NotFound` for the result. A deletion request, not proof of physical erasure (see `live-provider-run.md`: DELETE of a missing result also answers 204). |

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
| `attempt_deadline_exceeded` | transient | One attempt ran longer than 30 minutes. **Kept transient on purpose** (implementation review F6): it is more likely provider slowness than document size. Real size limits are separate terminal codes. | Nothing for one occurrence. If one document keeps hitting it until it fails, look at provider latency in the logs before treating it as a size problem. |
| `unexpected_error` | transient | An unclassified exception; only its type is logged. | Look up the exception type in the log; repeated occurrences are a bug. |
| `pdf_unreadable` | transient (rare) | Stored only when a PDF parses differently on resume than when its OCR pages were submitted. An ordinary local parser failure is not stored: the file goes to whole-file OCR instead. | Nothing. |
| `encrypted_pdf` | terminal | Password-protected PDF. | Owner keeps the original; reading is not possible. |
| `page_limit_exceeded` | terminal | More than 2,000 PDF pages. | None; nothing is truncated. |
| `text_limit_exceeded` | terminal | More than 10,000,000 extracted characters. | None. |
| `result_limit_exceeded` | terminal | The OCR result JSON exceeded 64 MiB, or the provider rejected the content length. | None. |
| `unsupported_image_dimensions` | terminal | The provider rejected the image size. | None; the original stays openable. |
| `unreadable_content` | terminal | The provider could not read the file content. | None. |
| `empty_document` | terminal | No text on any page of any original. | None. |
| `original_unavailable` | terminal | The original's blob is missing. | Follow `docs/sop/recover-originals.md`; then the owner retries. |
| `provider_rejected`, `provider_protocol_error` | terminal | The provider failed the analysis or answered in an unexpected shape. | Check the API version and model; repeated occurrences need investigation. |
| `attempts_exhausted` | terminal | Claimed for a sixth time: earlier attempts ended without handing the job back, typically a process crash while reading it. Stops crash loops from burning the F1 CPU quota. | Treat as a bug report for that document ID; do not retry repeatedly. |
| `provider_auth` | configuration | No token for the identity, or 401/403 from the provider: missing Cognitive Services User role or wrong identity. | Check the role assignment (`infra-rollout.md`, step 2), then requeue (below). |
| `provider_config` | configuration | 404 or similar: wrong endpoint, model or API version, or reading unavailable. | Check `DocumentReading__Endpoint`, `ModelId`, `ApiVersion`, then requeue. |

## Configuration

The keys and defaults are documented in `docs/local-development.md` (Configuration). In Azure they are App Service
settings (`DocumentReading__Auth`, `__Endpoint`, `__ModelId`, `__ApiVersion`, `__WorkerEnabled`) set by
`infra/modules/app-service.bicep`, with `WorkerEnabled` fixed to `false` there.

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
`docs/sop/check-incomplete-uploads.md`). All queries below are read-only and return no content.

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

Stored documents without a reading job (should be zero; a pre-S-03 release running against this schema would create
them):

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
3. To keep the pause across deployments, commit `'false'` in `infra/modules/app-service.bicep` (already the value
   until the worker is activated).

While paused: new captures still get `queued` jobs, owners see "queued", Retry still requeues failed jobs, and **provider
cleanup also stops** (it runs in the worker loop). Pending deletions then rely on the provider's 24-hour retention until
the worker resumes, so keep pauses short when cleanup is pending.

**Resume**: set `DocumentReading__WorkerEnabled=true` through the approved release (Bicep) or, temporarily, the same
`az webapp config appsettings set` command. On start the worker first runs due cleanup, then claims due and
lease-expired jobs one at a time. Check the overview query until no due work remains.

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

**Forward reconciliation** after a pre-S-03 release stored documents without jobs (approved mutation, the same statement
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

The API stays on App Service F1 (plan amendment 2026-10-06): no Always On, a daily CPU quota of 60 CPU minutes, and the
app unloads after idle time.

- **Sleep.** When F1 unloads the app, nothing runs: new jobs, transient retries, provider cleanup and backfill wait until
  the next request wakes it (any page that calls `/api/*`, including the smoke test's `/api/health`). On start the worker
  runs due cleanup first, then due and lease-expired jobs. A job interrupted by an unload without notice is reclaimed
  after its 2-minute lease expires and counts as an attempt. Owners may see "queued" for a long time on a quiet day;
  that is the documented limitation, not an outage.
- **CPU quota.** Exceeding 60 CPU minutes in a day stops the app until the quota resets, which also makes originals
  unavailable. Guards: one job at a time, a 60-second, single-processor local parse that is saved and reused across
  attempts, and the 5-attempt budget that stops crash loops.
- **Idle polling (review F7, deferred).** While awake, the worker runs a cleanup query and a claim query every
  5 seconds even when nothing is due. It is cheap but constant; check its share in the first CPU measurements. A backoff
  (5 s growing to 60 s) is the planned fix if it matters.
- **What to watch**, daily for the first week after activation, then weekly:
  - CPU time: the app's Quotas page in the portal (CPU (Day)), or `az monitor metrics list --resource <api app resource
    id> --metric CpuTime --interval PT1H --aggregation Total --offset 1d` (seconds per hour; sum for the day).
  - The overview and due-work queries above (a backlog that survives wake-ups means the worker is not keeping up).
  - Restarts or "quota exceeded" events in the App Service activity log.
- **B1 trigger.** Move to B1 (with Always On) when either holds: daily CPU time reaches about 45 CPU minutes (75 % of the
  quota) on more than one day in a week, or the quota is hit once; or cold starts break the about-two-second search
  target once S-04 exists. Record each reading in the evidence table below. The B1 switch is a separate, approved
  infrastructure change.

## Rollback

- **Stop reading only**: pause the worker (above). Capture and originals are unaffected.
- **Code rollback**: revert the release on `main` and approve the deploy, or re-run an earlier successful deploy. Keep
  the schema, `document_reading_jobs`, `document_reading_run_files` (with their cleanup obligations),
  `document_text_pages`, the Document Intelligence account, its role assignment and every original. A pre-S-03 release
  ignores the reading tables; documents it stores have no job and are reconciled forward (above) before reading is
  enabled again.
- Never drop the reading tables or delete text, jobs or run files as part of a rollback; pending cleanups would be lost
  and could not be retried.
- Original retrieval does not depend on reading: with reading `Disabled`, misconfigured or paused, originals open
  normally. Check 5.5 confirms this in Azure.

## Post-deployment verification evidence (manual 5.4 and 5.5)

Fill in after the separately approved deployment and worker activation. Record IDs shortened to their first 8
characters, states, codes, counts and timings only; no text, file names, provider payloads or credentials. Use two
synthetic accounts with synthetic fixtures (for example `services/api.Tests/Fixtures/Reading/synthetic-note.png`).

Status: **5.4 and 5.5 accepted by the owner on 2026-10-12** (the idle wake-up was observed only as startup recovery).

### Deployment

| Item | Value |
| --- | --- |
| Deployed commit | `4afbb78` on main (merge of #27 after #30 `9d0014c`; reading code from #28 `a91aa38` and #29 `d4b28df`) |
| Deploy run / approval date | Run 38185382270, approved 2026-10-11 23:50 UTC. Infra, API and web deployed; the run is marked failed only because smoke check 2c got one 502 at 23:57:48 during a container swap (see "Deploy incident" below) |
| `infra/deploy.sh what-if` scope matched (one Document Intelligence account and `DocumentReading__*` settings; no SKU, network, PostgreSQL or role changes) | Yes (2026-10-11, before #29): account unchanged, no SKU, network or role changes; the remaining diff is the known what-if noise |
| `scripts/smoke.sh` result (including check 2d) | Re-run by hand 2026-10-12 00:06 UTC: all checks PASS, including 2d (GET and POST 401). The #29 deploy smoke also passed 2d |
| Startup line shows `AzureManagedIdentity` and the worker state | Yes: "Document reading is available: AzureManagedIdentity against di-ogarniamy-…, model prebuilt-read, API 2024-11-30. Worker enabled." (23:56:52 UTC) |
| OCR identity: API managed identity holds Cognitive Services User on the account (role assignment ID or "verified in portal") | Verified in use: documents read with managed identity and results deleted (204); role granted by the owner with the bootstrap template on 2026-10-11 |
| Worker activation date and method | 2026-10-11 23:56 UTC, by #30 (`DocumentReading__WorkerEnabled=true` in `infra/modules/app-service.bicep`) |

### Isolation (two synthetic accounts)

| Check | Result |
| --- | --- |
| Account A captures a synthetic document; status goes queued, reading, read | PASS: 8-page synthetic scan `29deca23`: queued, reading (00:07:39), read (00:07:40, 4.7 s); 60-page scan `05f681dc` read in 11.8 s. Results deleted (204) right after each checkpoint |
| Account B gets 404 for A's reading status, Retry and original | PASS: status 404, original 404, detail 404, Retry 404 |
| Anonymous requests get 401 for status and Retry (no redirect) | PASS (smoke check 2d) |
| Inactive animal (S-06 API): A still opens the original and sees the status | PASS: after `PUT …/activity` (200, inactive) A gets status 200 (read) and original 200 with identical sha256; B still 404 |

### Recovery

| Check | Result |
| --- | --- |
| Restart during reading (`az webapp restart`, approved): job requeued or reclaimed, read once, no second Analyze for a saved operation | PASS with a caveat: the restart was issued while `05f681dc` was reading. App Service starts the new container before stopping the old one, so the old container finished the job (one Analyze POST, read once) and was stopped 30 s later. A hard stop in the middle of OCR (saved operation resumed, no resubmission) was verified locally in 3.4 (`live-provider-run.md`) |
| Pending cleanup after restart: acknowledged on the next pass | PASS for this run: every deletion was acknowledged before the restart completed; no cleanup was left pending |
| F1 wake-up: document captured, app left idle until unloaded (time idle: not measured), next request wakes it, job and pending cleanup complete afterwards | PARTIAL: not run as an idle test. Equivalent evidence: jobs queued while the worker was off (backfill of existing documents) were claimed within 1 s of the new container starting (23:56:56) and read. An idle unload and wake-up is still to be observed |

### Deploy incident (run 38185382270)

- Timeline (UTC): new container up 23:56:56; the worker immediately read the backfilled documents (`070df6ca` 9.3 s, `65bc0ac4` 16.0 s, `74cbf62a` 3.5 s) and claimed `e7926af9` at 23:57:25. Smoke check 2c waited 19 s and got 502 at 23:57:48. The platform cold-started replacement containers at 23:58:02 and 23:58:13; the old container stopped without an application error in the log. Health was 500 once shortly after, then 200.
- The same pattern was reproduced by the restart test: the new container's startup probe failed after 5.5 s (`ContainerStartupFailure`) while the old container was reading the 60-page scan, and was retried.
- Reading: on F1 a deploy or restart overlaps old and new containers on one small CPU share. While the worker is busy right after start, the warm-up probe and proxied requests can time out (502). Originals were unavailable for under a minute; no data was affected.
- Addressed in the startup-hardening PR: the worker waits `StartupDelay` (60 s) after the app starts before its first claim, EF Core command logging is Warning (every 5-second poll used to log two SQL commands), and smoke checks 2b-2d retry while the answer is 000 or 5xx (a wrong status still fails at once).
- `e7926af9` (owner document) was being read when its container stopped at 23:57-23:58. The owner confirmed on 2026-10-12 that it shows "content read": it was reclaimed after the lease expired and finished, so production also recovered a job interrupted by a real container stop.

### F1 CPU

| Date | CPU minutes that day | Documents read | Notes (idle polling share, B1 trigger approaching?) |
| --- | --- | --- | --- |
| 2026-10-11 (UTC) | 0.8 (metric) | at least 4 backfilled documents | Metric has a gap from 23:38 to 00:00 (container swaps), so this is a lower bound. Far below the 60-minute quota; no B1 trigger |
| 2026-10-12 (UTC, to 00:12) | 0.5 (metric) | 2 synthetic | Idle polling about 0.1-0.2 s per minute (about 3-4 CPU minutes a day while awake). Check the Quotas blade for a full day before drawing conclusions |

### Owner review (5.5)

| Item | Result |
| --- | --- |
| OCR quality evidence reviewed (`ocr-feasibility.md`) | Accepted by the owner, 2026-10-12 |
| Retention and cleanup limitations reviewed (unknown submissions, 204 is not proof of erasure, cleanup paused with the worker) | Accepted by the owner, 2026-10-12 |
| This runbook's rollback reviewed | Accepted by the owner, 2026-10-12 |
| Original opens with reading disabled or paused | Yes: originals opened in production after #28/#29 with the worker off; covered by tests |
