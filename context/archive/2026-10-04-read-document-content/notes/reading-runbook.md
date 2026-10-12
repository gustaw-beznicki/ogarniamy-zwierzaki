# Document reading runbook (Phase 5)

Post-deployment evidence for manual checks 5.4 and 5.5, and the deploy incident record.

The operating procedures that were written here (states, failure codes, configuration, inspection queries, pausing and
resuming the worker, F1 sleep and CPU quota, rollback) moved to
[`docs/sop/operate-document-reading.md`](../../../../docs/sop/operate-document-reading.md) on 2026-10-12
(implementation review F4), so they stay with the project documentation after this change is archived. The one-time
rollout steps are in [`infra-rollout.md`](infra-rollout.md).

Accepted limitation recorded by the implementation review (2026-10-12): no account-wide or daily CPU budget for reading
(F3, parked in the roadmap as "Reading CPU budget"). Idle polling (phase 3 review F7) was fixed by the same review (F5):
the idle wait backs off from 5 seconds to a minute, so the CPU figures below predate it.

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
| F1 wake-up: document captured, app left idle until unloaded (time idle: not measured), next request wakes it, job and pending cleanup complete afterwards | PARTIAL: not run as an idle test. Equivalent evidence: jobs queued while the worker was off (backfill of existing documents) were claimed within 1 s of the new container starting (23:56:56) and read. A dedicated idle unload and wake-up was not observed; the owner accepted this gap with 5.4 on 2026-10-12 (implementation review F7), since startup recovery is the same code path. The 10-minute stop of status polling (4.3) was likewise not exercised by hand |

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
