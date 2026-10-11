# Read document content — Plan Brief

> Full plan: `context/changes/read-document-content/plan.md`
> Research: `context/changes/read-document-content/research.md`

## What & Why

Read stored veterinary PDFs/photos automatically and retain source-page text for future semantic search. Show reading progress and failure without making original retrieval depend on OCR.

## Starting Point

S-02 supplies stored metadata, immutable private originals, owner-scoped retrieval and real PostgreSQL/Azurite tests. Its verification is still pending; background reading is new work.

## Desired End State

Owners see queued/reading/content-read/failed status and can retry failed reading. Restarts resume durable work and provider cleanup, while originals remain accessible; content-read does not claim semantic search exists.

## Key Decisions Made

| Decision | Choice | Why | Source |
| --- | --- | --- | --- |
| Provider/privacy | Azure prebuilt-read v4, explicit retention exception up to 24 hours after analysis; prompt deletion and durable cleanup retry | Owner accepted the documented hosted-provider behavior; no-training rule remains | Owner, 2026-10-04; PRD |
| PDF routing | Local PdfPig text, page-level OCR fallback including raster-image pages | Avoid external processing for local-only readable PDFs without missing scans behind captions | Owner + external research; Plan |
| Durable work | PostgreSQL jobs, leased API BackgroundService, complete text revisions | Reuse the database and recover without a broker/commit gap | Research + Plan |
| Retry | Five automatic attempts; owner Retry with durable limit of three accepted retries/document/hour | Recover temporary failures without unbounded resubmission | Owner + Plan |
| Status | Separate reading endpoint/component; stable capture responses | Avoid coupling changing progress to idempotent completion | Research + Plan |
| Hosting | Stay on F1; worker resumes due jobs/cleanup on wake-up; B1 only on a trigger (F1 CPU quota or S-04 cold-start target); private PostgreSQL network moved to M-2 (`database-private-network`) | B1 plus private endpoint/DNS would add about EUR 19/month for one user against a 40/month budget | Owner, 2026-10-06 (supersedes B1/private network) |
| Limits | 2,000 PDF pages, 10,000,000 text characters; bounded parser child | Explicit failed reading is safer than truncated success or a blocked API | Plan |
| Migration order | S-06 animal migration first, then S-03 processing migration | Keep one coherent EF snapshot while domain work proceeds in parallel | Plan |

## Scope

**In scope:**

- Provider feasibility check and OCR resource/access.
- Durable scheduling/backfill, page provenance, fenced worker recovery and provider cleanup.
- Owner-isolated status/Retry, PL/EN detail UI and operational documentation.

**Out of scope:**

- Embeddings/search, generated medical output, custom model training, animal management and original changes/deletion.

## Architecture / Approach

First completion schedules a unique job in the existing transaction. A leased worker reads immutable sources through local PDF extraction or authenticated Azure OCR, checkpoints remote IDs/results, publishes a complete revision and tracks cleanup independently. An owner-scoped endpoint powers detail status without changing original DTOs.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. OCR feasibility and infrastructure readiness | Quality evidence and reviewed S0/RBAC changes (F1 kept) | Real-photo readability |
| 2. Durable jobs and text model | Transactional scheduling, backfill and fenced claims | Commit gaps or stale publication |
| 3. Extraction worker and recovery | Local/OCR routing, resumable operations and cleanup | Ambiguous provider calls and parser limits |
| 4. Reading status and Retry | Isolated status/Retry plus PL/EN component | Tenant leakage or unbounded retries |
| 5. Deployment verification and documentation | Deployed acceptance and recovery notes | Unproven provider/idle/restart behavior |

**Prerequisites:** S-02 verification before release; synthetic fixtures and five owner-selected photos for manual quality evidence; reviewed OCR runtime authorization and staged Azure deployment. Separate approval is required for production mutations.

**Estimated effort:** Five implementation phases, roughly 5–8 focused sessions; an estimate, not a measured commitment. Domain work can overlap S-06; shared edits and migrations are integrated sequentially.

## Open Risks & Assumptions

- OCR can return plausible noise; the five-photo check is a real acceptance gate.
- S0 introduces usage spend; verify actual pricing/spend before applying. On F1, reading waits while the app sleeps, and parsing must stay within the daily CPU quota.
- Lost submission responses may leave unknown remote operation IDs; early deletion cannot be promised for those operations.
- Bounded retention is an accepted exception, not an immediate-deletion guarantee.
- Other S-02 work is changing concurrently; rebase onto its verified integration commit before implementation.

## Success Criteria (Summary)

- Stored documents acquire complete ordered text or an honest reading failure without altering originals.
- Owner isolation, restart/lease recovery and cleanup/retry behavior pass deterministic integration checks.
- Real-photo and deployed PL/EN evidence confirms useful reading and original access during outages.
