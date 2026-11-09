# SOP: Operate semantic search

**Purpose**: understand, inspect, pause, resume and recover background embedding and the search it feeds in Azure.
**When**: after an embedding incident (provider outage, missing role, wrong endpoint, throttling), when owners report
that documents stay "still being processed" in search, during the first backfill, and during a rollback.
**Safety**: every inspection step is read-only and returns no document content. Every step that changes Azure (app
settings, deployments, database rows or role assignments) needs the owner's explicit approval.

## Ground rules

- Inspect and record **IDs, states, codes, counts and timings only.** Never select, copy or paste
  `document_chunks.text`, `document_chunks.embedding`, `document_text_pages.text`, animal names, search queries or
  provider payloads. The SQL below is written so that it cannot return content.
- Search never decides whether an original is available. An embedding outage, a paused worker or a misconfigured
  provider leaves capture, reading, document lists and original downloads working; search answers 503
  `search_unavailable` only when the query itself cannot be embedded.
- Never delete chunks, jobs, text pages or originals to "fix" search. Chunks are replaced only by a newer embedding of
  the same document.

## What runs where

- The worker is a `BackgroundService` inside the API process (one job at a time). It runs only when `Embedding:Auth`
  resolves to a working setup **and** `Embedding:WorkerEnabled` is `true`; otherwise it logs one line at startup.
- Embedding uses the Azure OpenAI deployment `text-embedding-3-large` (regional Standard, `swedencentral`, capacity 50 =
  50K tokens per minute) with the API's managed identity and the Cognitive Services OpenAI User role.
- The worker sends at most `Embedding:MaxEstimatedTokensPerRequest` (16,000) estimated tokens per request and a
  sustained `Embedding:WorkerTokensPerMinute` (40,000) per minute, leaving the rest of the deployment's limit to searches.
  Its in-memory token bucket starts full, so up to about twice that can go out in the first minute after startup. During
  a deployment the old and new instances briefly run side by side with separate pacers and search rate limiters, which
  doubles both limits for that window; throttled (429) requests are retried. Change `WorkerTokensPerMinute` together
  with the deployment capacity in `infra/modules/embeddings.bicep`.
- Each search embeds the query once (no retry) and runs one exact, owner-scoped cosine query. One account may send
  `Search:MaxRequestsPerMinute` (30) searches per minute; more answer 429 `search_rate_limited`, which the search screen
  shows as its "search unavailable" notice.

## Job states

One row per read document in `document_embedding_jobs` (primary key `document_id`).

| State | Meaning | Leaves it when |
| --- | --- | --- |
| `queued` | Waiting, due at `due_at`. A reading publish, an animal rename and the migration backfill put jobs here. | A worker claims it (`embedding`). |
| `embedding` | Claimed with a lease (`lease_token`, `lease_expires_at`, 10 minutes, extended by the expected pacing wait for a long document). | Embedded, failed, requeued after a transient or stale result, or reclaimed after the lease expired. |
| `embedded` | The document's chunks match `embedded_run_generation` and `embedded_animal_name`. | A new reading publish or a rename of the animal queues it again. |
| `failed` | Stopped with a sanitized `failure_code`. | Only a new reading publish, a rename, or the approved operator requeue below. There is no owner Retry for embedding. |

Transient and configuration failures are retried after 30 s, 2 min, 10 min and 30 min (a longer provider `Retry-After`
wins, capped at one hour); the fifth failure fails the job. A failed document is counted in search's "still being processed" note until it
is embedded.

## Failure codes

| Code | Class | Meaning | What to do |
| --- | --- | --- | --- |
| `provider_unavailable` | transient | Throttling (429), provider 5xx, network error or timeout. | Nothing for single jobs. Many of them during a backfill: lower `Embedding__WorkerTokensPerMinute` or raise the capacity. |
| `attempt_deadline_exceeded` | transient | One attempt ran longer than 5 minutes plus the pacing wait its document needs. | Nothing for one occurrence; repeated for one document points at provider slowness. |
| `unexpected_error` | transient | An unclassified exception; only its type is logged. | Look up the type in the log; repeated occurrences are a bug. |
| `provider_auth` | configuration | No token, or 401/403: missing Cognitive Services OpenAI User role or wrong identity. | Check the role ([deployment guide](../deployment.md), "Rolling out semantic search"), then requeue. |
| `provider_config` | configuration | 404 or similar: wrong endpoint or deployment name. | Check `Embedding__Endpoint` and `Embedding__Deployment`, then requeue. |
| `provider_rejected`, `provider_protocol_error` | terminal | The provider refused the input or answered in an unexpected shape. | Check the deployment and model version; repeated occurrences need investigation. |
| `text_unavailable` | terminal | The document has no published text any more. | None; a new reading publish queues it again. |
| `attempts_exhausted` | terminal | Claimed for a sixth time after earlier attempts ended without a result (crash loop). | Treat as a bug report for that document ID. |

## Inspecting embedding

### From logs

`az webapp log tail --resource-group rg-ogarniamy-mvp --name <api app>`. Useful lines (IDs, counts, codes and timings
only): `Embedding is available: ...` or `Embedding is unavailable: <reason>` at startup, `The embedding worker started`
or `The embedding worker is not running: <reason>`, `Embedding document <id>: attempt <n>.`, `Document <id> embedded:
<n> chunks from <n> pages in <ms> ms.`, and the warning naming the settings to check after a provider rejection.

### From the database

Access: locally `docker compose exec postgres psql -U postgres -d ogarniamy`. In Azure only the API's managed identity
can sign in; querying needs a temporary, separately approved access path, removed afterwards (as in
[Check incomplete uploads](check-incomplete-uploads.md)).

Overview by state:

```sql
SELECT state, count(*) AS jobs, min(due_at) AS oldest_due, max(attempt_count) AS max_attempts
FROM document_embedding_jobs
GROUP BY state
ORDER BY state;
```

Failed jobs by code:

```sql
SELECT failure_code, count(*) AS jobs, min(updated_at) AS first, max(updated_at) AS last
FROM document_embedding_jobs
WHERE state = 'failed'
GROUP BY failure_code
ORDER BY jobs DESC;
```

Backfill progress (stored documents not yet searchable, across all owners: the same rule as search's "still being
processed" count; failed readings are excluded):

```sql
SELECT count(*) AS not_searchable
FROM documents d
JOIN document_reading_jobs r ON r.document_id = d.id
LEFT JOIN document_embedding_jobs e ON e.document_id = d.id
WHERE d.storage_state = 'stored'
  AND r.state <> 'failed'
  AND (r.state IN ('queued', 'reading')
    OR e.document_id IS NULL
    OR e.state <> 'embedded'
    OR e.embedded_run_generation IS DISTINCT FROM r.published_run_generation);
```

## Pausing and resuming

**Pause**: `az webapp config appsettings set --resource-group rg-ogarniamy-mvp --name <api app> --settings
Embedding__WorkerEnabled=false` (restarts the app; the current job is handed back), and commit `'false'` in
`infra/modules/app-service.bicep` to keep it. Queued jobs wait. Search matches only chunks of a document's current
published reading: a document whose new reading is published while paused drops out of search until it is embedded
again, while documents queued only by an animal rename stay searchable under the previous name.

**Resume**: set `Embedding__WorkerEnabled=true` through the approved release (Bicep). The worker waits 60 seconds after
startup, then claims due jobs one at a time at the paced rate.

## Requeueing failed jobs

Failed jobs stay failed after the cause is fixed. Requeueing them is a separately approved database change. Review the
count first with "Failed jobs by code", fix the cause (role, endpoint, deployment, capacity), then:

```sql
-- Approved mutation only.
UPDATE document_embedding_jobs
SET state = 'queued', due_at = now(), attempt_count = 0, failure_code = NULL,
    lease_token = NULL, lease_expires_at = NULL, updated_at = now()
WHERE state = 'failed'
  AND failure_code IN ('provider_auth', 'provider_config', 'provider_unavailable', 'attempt_deadline_exceeded');
```

A requeued document stays searchable only if its stored chunks belong to its current published reading (for example
after a failed rename re-embedding). A document whose re-embedding failed after a new reading is out of search until
the requeued job is `embedded`. Watch the overview query until the requeued jobs are `embedded`.

## Rollback

Code rollbacks keep the schema (migrations stay forward). The `DocumentEmbedding` migration's `Down` drops
`document_chunks`, `document_embedding_jobs` and the `vector` extension; never run it as part of a rollback. Everything
it drops can be rebuilt from stored text, but search is empty until the backfill completes again.
