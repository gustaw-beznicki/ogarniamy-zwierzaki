---
date: 2026-10-19T10:51:37+00:00
researcher: Claude
git_commit: 0466c923a0ded39dfe28230cc7118897dff7e69d
branch: ccr-193eec96-t639lr
repository: ogarniamy-zwierzaki
topic: "What the codebase already provides for S-04 semantic search with fragments, and where it must change"
tags: [research, codebase, semantic-search, pgvector, embeddings, document-reading, owner-isolation, apps-web]
status: complete
last_updated: 2026-10-19
last_updated_by: Claude
---

# Research: S-04 semantic search with fragments — internal codebase evidence

**Date**: 2026-10-19T10:51:37+00:00
**Researcher**: Claude
**Git Commit**: 0466c923a0ded39dfe28230cc7118897dff7e69d (`main` at 6d1a44f plus the change-folder commit)
**Branch**: ccr-193eec96-t639lr
**Repository**: ogarniamy-zwierzaki

## Research Question

For roadmap slice S-04 (`context/foundation/roadmap.md:150-157`): what does the codebase already do that
semantic search must build on — the read text from S-03, the background worker, owner isolation, the
original-file route, infrastructure and the frontend — and which prior decisions or gaps constrain the plan?
This is internal research only; library and provider capabilities (pgvector client, embedding model,
Azure availability) are left for external research before `/10x-plan`.

## Summary

- **Text source exists, at page granularity.** S-03 publishes one `document_text_pages` row per page of every
  original (blank pages included), keyed `(document_id, run_generation, document_file_id, source_page_number)`
  with `text` only — no line, offset or bounding-box data (`services/api/DocumentReading/DocumentTextPage.cs:3-23`,
  `services/api/Data/AppDbContext.cs:244-268`). Fragments must be cut by S-04 itself from page text; a fragment can
  cite its file and page.
- **The natural embedding hook is the publish transaction.** `DocumentReadingJobs.PublishAsync` deletes the
  document's pages, inserts the new revision, sets the job to `read` with `published_run_generation`, and commits
  in one transaction (`services/api/DocumentReading/DocumentReadingJobs.cs:246-300`). Capture enqueues reading the
  same way (raw INSERT in the capture transaction, `services/api/Documents/OwnedDocuments.cs:256`), so enqueuing an
  embedding job in the publish transaction follows an established pattern.
- **Job machinery is reusable.** Reading uses a DB-table queue claimed with `FOR UPDATE SKIP LOCKED`, leases,
  heartbeats, 5 attempts with backoff and a wake-up semaphore (`DocumentReadingJobs.cs:28-58`,
  `DocumentReadingWorker.cs:31-55`). There is no external queue.
- **Owner isolation is done in code, not by a global filter.** Every owner query starts from `animal_members`
  rows with `role = owner` (`services/api/Documents/OwnedDocuments.cs:304-315`); a raw-SQL `EXISTS` twin exists for
  hand-written queries (`services/api/DocumentReading/OwnedDocumentReading.cs:145-152`). There is no
  `HasQueryFilter`. Read paths do not filter on `IsActive`, so inactive animals stay findable
  (`OwnedDocumentReading.cs:5-6`). A vector query will be raw SQL, so it must carry this predicate explicitly.
- **Nothing for vectors exists yet.** No pgvector package, extension, migration or `UseVector` call (grep over
  `.cs`, `.csproj`, `.props`, `.yaml`, `.bicep`, `.json`); local and test PostgreSQL use plain `postgres:17`
  (`compose.yaml:5`, `services/api.Tests/ApiFactory.cs:13`); Azure Flexible Server has no `azure.extensions`
  allowlist (`infra/modules/postgres.bicep`, no `configurations` resource).
- **No embedding provider is chosen or provisioned.** `infra/` has Document Intelligence only. The PRD privacy
  rule allows content to leave the product "only for text-reading processing" (`context/foundation/prd.md:314-321`),
  so sending text to an embedding service needs an owner decision and a PRD amendment.
- **Frontend has a placeholder and a full state design.** `/search/` and `/en/search/` render `Placeholder`
  (`apps/web/src/pages/search.astro:1-10`, `apps/web/src/components/Placeholder.tsx:4-6`). The ui-redesign
  references `context/changes/ui-redesign/design/Mobile-Search.dc.html` and `Desktop-Search.dc.html` define the
  idle / loading / results / empty / unavailable states. The frontend has no test framework (`docs/testing.md:30`).

## Detailed Findings

### 1. Read text from S-03 (the corpus to embed)

- `document_text_pages` holds the single published revision: publishing runs
  `DELETE FROM document_text_pages WHERE document_id = …` and re-inserts all pages
  (`DocumentReadingJobs.cs:272-283`). A revision is complete — every page of every original, blank pages with
  empty `Text` (`DocumentTextPage.cs:3-5`).
- `FilePosition` (zero-based original position) and `SourcePageNumber` (one-based; 1 for a photo) give
  citation coordinates (`DocumentTextPage.cs:15-19`). `ExtractionMethod` is `text_layer` or `ocr`.
- OCR lines within a page are joined with `\n` (`services/api/DocumentReading/AzureDocumentTextReader.cs:278-302`);
  provider polygons and spans are not kept (same inspected path).
- If every page is blank, `PublishAsync` returns `EmptyDocument` before writing pages
  (`DocumentReadingJobs.cs:265-268`), so such a document has no text rows and cannot be found by search.
- No endpoint exposes text pages to owners yet; `OwnedDocumentReading` returns status only. Worker-side
  `DocumentReadingJobs` is marked as never serving HTTP (`DocumentReadingJobs.cs:8-12`). A search read path is new.
- Re-reading after an owner Retry bumps `run_generation` (`services/api/DocumentReading/OwnedDocumentReading.cs:70`),
  so derived embeddings must be tied to `(document_id, run_generation)` or replaced at publish.
- OCR noise risk for search: printed form labels become text and "could match unrelated queries"
  (`context/archive/2026-10-04-read-document-content/notes/ocr-feasibility.md:80-81`).

### 2. Background worker and job pattern

- `DocumentReadingWorker` is a `BackgroundService` registered in `DocumentReadingSetup.cs:53`, called from
  `Program.cs:43`. It exits unless reading is available and `DocumentReading:WorkerEnabled` is true
  (`DocumentReadingWorker.cs:73-79`); the Azure app setting turns it on (`infra/modules/app-service.bicep:108-113`).
- Claim: one `UPDATE … WHERE document_id = (SELECT … FOR UPDATE SKIP LOCKED) RETURNING` over due queued jobs
  or expired leases (`DocumentReadingJobs.cs:28-58`). Lease 2 min, heartbeat 30 s, start delay 60 s, idle
  back-off 5–60 s (`DocumentReadingWorker.cs:49-66`). Up to 5 attempts with 30 s / 2 m / 10 m / 30 m delays
  (`DocumentReadingWorker.cs:31-39`).
- Wake-up: `DocumentReadingWakeUp` is signalled on capture completion (`Documents/DocumentEndpoints.cs:90`) and
  owner Retry (`DocumentReadingEndpoints.cs:50`).
- Two hook options identified for embedding (inference, not decided): (a) insert an embedding job inside the
  `PublishAsync` transaction — crash-safe and mirrors capture; (b) a separate worker polling `read` jobs whose
  `published_run_generation` has no embedded revision.
- Infrastructure risk register already demands idempotent jobs, persisted job state and versioned embedding
  formats (`context/foundation/infrastructure.md:163-164`).

### 3. External-service client template

- OCR is raw REST over a named `HttpClient` (`"DocumentIntelligence"`), no Azure SDK and no Polly
  (`DocumentReadingSetup.cs:22-34`); hand-written retry honours `Retry-After` (`DocumentReadingWorker.cs:477`,
  `:629-632`). Timeout from options, default 30 s (`DocumentReadingOptions.cs:24`).
- Auth is Entra-only: `ManagedIdentityCredential(SystemAssigned)` in Azure, `AzureCliCredential` for
  `DeveloperCredential` in Development, scope `https://cognitiveservices.azure.com/.default`
  (`DocumentReadingAvailability.cs:34-96`). Misconfiguration never stops the API; the feature reports
  "unavailable" (same file).
- Failures are thrown as `DocumentReadFailure` with sanitized codes; text and payloads are not logged
  (`AzureDocumentTextReader.cs`, inspected path).
- Tests fake the provider with an `HttpMessageHandler` (`services/api.Tests/FakeOcrProvider.cs:12`) and
  `FakeTokenCredential`, constructing the adapter directly (`DocumentReadingWorkerTests.cs:611-613`).
- RBAC for the cognitive resource is granted by an owner-run bootstrap template, not CI
  (`infra/bootstrap/document-reading-access.bicep:20-55`; CI identity is Contributor only,
  `context/foundation/infrastructure.md:152`). An embedding resource would need the same pattern.
- No Application Insights or OpenTelemetry is wired; health check is `AddDbContextCheck` at `/api/health`
  (`services/api/Data/DatabaseSetup.cs:18`, `Program.cs:139`).

### 4. Data access and migrations

- Packages (central management, `Directory.Packages.props`): `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3`,
  `EFCore.NamingConventions 10.0.1` (snake_case), `Testcontainers.PostgreSql 4.15.0`. No pgvector package.
- `NpgsqlDataSource` is built in `services/api/Data/DatabaseSetup.cs:33-57` with managed-identity tokens
  (`:59-75`); context config is `UseNpgsql(dataSource).UseSnakeCaseNamingConvention()` (`:30-31`). Vector type
  mapping would plug in at both points (inference; library API to be confirmed externally).
- Migrations auto-apply at startup (`Program.cs:123` → `DatabaseSetup.cs:23-27`); CI has no migration step
  (`.github/workflows/deploy.yml`). Conventions: explicit `pk_/fk_/ix_/ck_/ak_` names, enums as text with
  check constraints (`AppDbContext.cs:297-361`), idempotent `migrationBuilder.Sql` backfills
  (`Migrations/20261005194436_DocumentReading.cs:166-175`), forward-only expand/contract
  (`docs/local-development.md:111-118`). Migration tests use `IMigrator`
  (`services/api.Tests/AnimalActivityMigrationTests.cs:30-60`).
- A backfill will be needed for documents already in `read` state when S-04 deploys (inference from the
  DocumentReading migration's own backfill at `:166-175`).
- `AGENTS.md` forbids `SELECT *` in hand-written SQL and prefers LINQ; a vector `ORDER BY embedding <=> …`
  query is the expected raw-SQL exception and must list columns.

### 5. Owner isolation, API shape and errors

- Minimal APIs per feature folder: `Map*Endpoints` + scoped `Owned*` service, mapped at `Program.cs:151-156`,
  services at `Program.cs:112-115`. Global fallback policy requires authentication (`Program.cs:93-95`); GETs
  need no antiforgery filter; every `/api/*` response is `no-store` (`services/api/NoStoreApiResponses.cs:5-17`).
- Current user: `CurrentUser.Id(principal, users)` (`services/api/Auth/CurrentUser.cs:6-11`).
- Ownership: `animal_members(animal_id, user_id, role)` with `AnimalRole { Owner }`
  (`services/api/Animals/AnimalRole.cs`, `AppDbContext.cs:78-85`). `Document` has `AnimalId` and no owner column
  (`services/api/Documents/Document.cs`).
- Original route: `GET /api/documents/{documentId}/files/{fileId}/original` re-checks ownership and streams the
  file (`services/api/Documents/DocumentEndpoints.cs:39-40`, `:124-152`). DTO `StoredDocumentFileResponse` builds
  `OriginalUrl` (`services/api/Documents/StoredDocumentFileResponse.cs:19`). `StoredDocumentResponse` carries
  `AnimalName` (projected at `OwnedDocuments.cs:72`).
- Errors: `ApiProblem.Create(status, code)` → ProblemDetails with `code` (`services/api/ApiProblem.cs`); the
  frontend maps missing codes to `unauthorized` / `not_found` / `file_too_large` / `request_failed`
  (`apps/web/src/lib/api.ts:136-143`). Transient upstream failures map to 503 (`Documents/TransientFailures.cs:23`)
  — relevant to the S-05 "search unavailable" notice.
- Isolation test template: `services/api.Tests/DocumentIsolationTests.cs:10-100` (401 for anonymous; two
  accounts, other gets 404), helpers in `DocumentApi.cs` and `TestAccounts.cs`, real HTTP over
  `WebApplicationFactory` with Testcontainers PostgreSQL and Azurite (`ApiFactory.cs`).
- No search code exists: case-insensitive grep for "search" in `.cs` files finds one comment
  (`services/api.Tests/DocumentCaptureTests.cs:90`).

### 6. Infrastructure

- PostgreSQL Flexible Server: `Standard_B1ms` Burstable, version 17, Entra-only auth, API identity is the Entra
  admin (`infra/modules/postgres.bicep:26-60`). Child operations must be chained with `dependsOn` (`:62`).
  Enabling pgvector needs a `configurations` child `azure.extensions = VECTOR` (absent today).
- App Service F1 (`infra/environments/mvp.bicepparam:5`), region `swedencentral` (`:3`), system identity, plain
  app settings with no secrets (`infra/modules/app-service.bicep:51-114`). F1 has a 60 CPU-min/day quota; a
  switch to B1 is triggered if cold starts break the ~2 s search target (`context/foundation/infrastructure.md:40-41`).
- No Azure OpenAI / AI Foundry resource exists. The region was chosen partly because it offers "regional
  embeddings" (`context/archive/2026-09-27-azure-walking-skeleton/plan-brief.md:24`; `plan.md:19` names
  `text-embedding-3-large`). Availability and quota were not re-checked after 2026-09-27.
- Local/test images lack pgvector: `compose.yaml:5` and `ApiFactory.cs:13` both use `postgres:17`; compose notes the
  tests use the same image (`compose.yaml:16`).

### 7. Frontend

- Routes `/search/` and `/en/search/` (`apps/web/src/pages/search.astro`, `apps/web/src/pages/en/search.astro`)
  wrap `Placeholder` inside `SessionGate` + `AppShell active="search"`; nav entries at
  `apps/web/src/components/AppShell.tsx:17`, `:41-46`, `:57-65`.
- API client: interfaces and the `api` object in `apps/web/src/lib/api.ts:1-103`, `:255-284`; plain `request<T>`
  serves GETs (`:145-176`); mutations need antiforgery helpers (`:194-240`).
- Links: document page `clientLocalePath(locale,'document')?id=…` (`apps/web/src/components/AnimalDocuments.tsx:326`);
  original opens in a new tab and is checked to start with `/api/documents/`
  (`apps/web/src/components/DocumentDetails.tsx:13`, `:36`, `:104`).
- i18n: `en.ts` defines the `Messages` type, `pl.ts` must satisfy it (`apps/web/src/i18n/en.ts:187`,
  `pl.ts:187`); `countLabel` accepts `documentCount` (`apps/web/src/i18n/format.ts:4-17`). Polish only in `pl.ts`
  (`context/foundation/lessons.md`).
- Reusable pieces: `Alert`, `PageState`, `Icon` (has `search`, `document`, `pdf`, `external`), `AnimalAvatar`,
  `.loading` status, `.document-list`/`.document-link` rows, `.chip` (S-05 filter) (`apps/web/src/styles/tokens.css`).
- Designs: F-02 index row E05 with empty / searching (indicator after 2 s) / unavailable states
  (`context/foundation/ui-mockup/README.md`); newer ui-redesign mocks with results cards (animal, date, page label,
  fragment, "open original"), and a note that a document still being read is not in results
  (`context/changes/ui-redesign/design/Mobile-Search.dc.html:145-166`, `Desktop-Search.dc.html:160-208`).
  The two disagree on whether a result shows a document title (F-02 does; the redesign shows animal + page).

## Code References

- `services/api/DocumentReading/DocumentReadingJobs.cs:246-300` — publish transaction (embedding hook)
- `services/api/DocumentReading/DocumentReadingJobs.cs:28-58` — `FOR UPDATE SKIP LOCKED` job claim
- `services/api/DocumentReading/DocumentTextPage.cs:3-23` — page-level text with provenance
- `services/api/Data/AppDbContext.cs:244-268` — `document_text_pages` mapping
- `services/api/DocumentReading/DocumentReadingWorker.cs:31-79` — retry, lease and enable settings
- `services/api/DocumentReading/DocumentReadingSetup.cs:14-53` — DI and named HttpClient template
- `services/api/DocumentReading/DocumentReadingAvailability.cs:34-96` — Entra credential selection
- `services/api/Documents/OwnedDocuments.cs:304-315` — owner scoping via `animal_members`
- `services/api/DocumentReading/OwnedDocumentReading.cs:145-152` — raw-SQL owner predicate
- `services/api/Documents/DocumentEndpoints.cs:39-40`, `:124-152` — original-file route
- `services/api/Data/DatabaseSetup.cs:23-57` — migrations at startup, data source
- `services/api.Tests/DocumentIsolationTests.cs:10-100` — isolation test template
- `services/api.Tests/ApiFactory.cs:13` — Testcontainers image `postgres:17`
- `infra/modules/postgres.bicep:26-73` — Flexible Server, no extension allowlist
- `apps/web/src/components/Placeholder.tsx:4-6` — current search screen
- `apps/web/src/lib/api.ts:255-284` — API client endpoint list
- `context/changes/ui-redesign/design/Mobile-Search.dc.html` — search state design

## Architecture Insights

- Background work is DB-driven (table queue, leases, idempotent stages), crash-safe through enqueueing in the same
  transaction as the triggering write. Embedding should follow this rather than add a new queue service.
- Cognitive services are reached by raw REST with Entra tokens and no keys; availability degrades instead of
  failing startup. This fits FR-012 (explicit "unavailable" rather than a crash) and the "no secrets" rule.
- Isolation is a code convention, so every new raw-SQL query is a new place where FR-002 can break; tests in the
  `DocumentIsolationTests` style are the established guard.
- The original stays reachable independently of reading/search (`DocumentEndpoints.cs:124-152`), which already
  satisfies the "original always retrievable" invariant for search failures.

## Historical Context (from prior changes)

- `context/archive/2026-10-04-read-document-content/plan.md:37` — S-03 explicitly deferred embeddings, chunk
  ranking, pgvector and search to S-04/S-05 (supported; still true).
- `context/foundation/roadmap.md:141` — "Which … OCR service and embedding service meet the no-retention,
  no-training requirement?" OCR part resolved by S-03 (Azure Document Intelligence with a 24 h retention exception,
  `prd.md:314-321`); embedding part still open (supported by absence in `infra/` and archive research).
- `context/foundation/shape-notes.md:48`, `:73-76`, `:535` — document is the unit, best fragment as
  justification, chunk embeddings in pgvector (supported as direction). The `:73-76` remark that exact search works
  when embeddings are unavailable is contradicted by later PRD FR-012 (`prd.md:275-281`), which removed keyword
  search entirely.
- `context/archive/2026-09-27-azure-walking-skeleton/plan-brief.md:24` — region picked for regional embeddings
  (partial: availability recorded then, not re-verified).
- `context/foundation/infrastructure.md:41` — B1 trigger if cold starts break the S-04 latency target (supported).

## Related Research

- `context/archive/2026-10-04-read-document-content/research.md` — S-03 reading pipeline research
- `context/archive/2026-09-28-account-and-first-animal/research.md` — auth and isolation research

## Open Questions

1. **Privacy rule for embeddings (owner decision, blocking for the plan).** The PRD allows content to leave the
   product only for text reading (`prd.md:314-321`). Sending page text and queries to an embedding API needs an
   explicit owner decision and PRD amendment (provider retention, no-training terms), or a self-hosted/local model.
2. **Embedding provider and model (external research).** Azure OpenAI in `swedencentral` vs alternatives;
   multilingual (Polish + Latin abbreviations) quality, dimensions, cost, quota; plus `Pgvector.EntityFrameworkCore`
   compatibility with Npgsql EF 10.0.3 and the Azure `VECTOR` extension version. Use Context7 / exa.ai.
3. **Chunking.** Page text has no offsets; the plan must define chunk size/overlap within a page and how the shown
   fragment is cut, keeping one result per document with its single best fragment (`prd.md:339-342`).
4. **Provisional similarity threshold** and how it is configured (roadmap Open Question 1, `roadmap.md:155`).
5. **Query transport.** A GET query string may reach server logs with medical wording; POST needs antiforgery
   (`apps/web/src/lib/api.ts:194-240`). Decide in the plan.
6. **Result shape.** Document title is absent from the data model; F-02 shows one, the redesign does not. Also
   whether to report documents still being read (redesign note) — needs a count from `document_reading_jobs`.
7. **Search latency on F1** (~2 s target, `prd.md:313`): query embedding round-trip plus cold start; measure, with
   the B1 trigger in `infrastructure.md:41` as the fallback.
8. **Backfill** of documents already `read` before S-04 deploys, and embedding versioning for rollback
   (`infrastructure.md:163`).
