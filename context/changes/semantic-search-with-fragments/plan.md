# Semantic Search with Fragments (S-04) Implementation Plan

## Overview

Owners can type a question in ordinary words and get back their own documents that are related in meaning, across
all their animals. Each result names its animal, shows the single fragment that best matched, and opens the stored
original (PRD US-02, FR-010, FR-013, FR-002, §Business Logic). Read page text from S-03 is cut into chunks, embedded
with Azure OpenAI `text-embedding-3-large` deployed as regional Standard in `swedencentral` (processing stays in
Sweden), stored in pgvector, and searched with an exact, owner-scoped cosine query behind a configurable threshold.
The PRD privacy requirement is amended to allow this EU-only processing.

## Current State Analysis

Evidence is in `context/changes/semantic-search-with-fragments/research.md`; the essentials:

- S-03 publishes one `document_text_pages` row per page (blank pages included), keyed
  `(document_id, run_generation, document_file_id, source_page_number)`, text only, no offsets
  (`services/api/DocumentReading/DocumentTextPage.cs:3-23`, `services/api/Data/AppDbContext.cs:244-268`). A document
  whose pages are all blank is never published (`services/api/DocumentReading/DocumentReadingJobs.cs:265-268`).
- Publishing replaces the document's pages and sets the job to `read` in one transaction
  (`DocumentReadingJobs.cs:246-300`). Background work uses a DB-table queue with `FOR UPDATE SKIP LOCKED`, leases,
  heartbeats and bounded retries (`DocumentReadingJobs.cs:28-58`, `DocumentReadingWorker.cs:31-79`).
- Cognitive services are reached by raw REST with Entra tokens; availability degrades instead of stopping the API
  (`DocumentReadingSetup.cs:14-53`, `DocumentReadingAvailability.cs:34-96`). RBAC is granted by owner-run bootstrap
  templates (`infra/bootstrap/document-reading-access.bicep`).
- Owner isolation is a code convention through `animal_members` with role `owner`
  (`services/api/Documents/OwnedDocuments.cs:304-315`); a raw-SQL predicate exists
  (`services/api/DocumentReading/OwnedDocumentReading.cs:145-152`). Read paths ignore `IsActive`.
- No pgvector anywhere: plain `postgres:17` in `compose.yaml:5` and `services/api.Tests/ApiFactory.cs:13`, no
  `azure.extensions` in `infra/modules/postgres.bicep`, no embedding resource in `infra/`.
- `/search/` renders a placeholder (`apps/web/src/components/Placeholder.tsx`); the state design lives in
  `context/changes/ui-redesign/design/Mobile-Search.dc.html` and `Desktop-Search.dc.html`.
- Animal rename is `OwnedAnimals.RenameAsync` (`services/api/Animals/OwnedAnimals.cs:44-56`).
- The PRD allows content to leave the product "only for text-reading processing" (`context/foundation/prd.md:314-321`).

## Desired End State

- A signed-in owner on `/search/` (or `/en/search/`) types a question, sees continuous progress while it runs, and
  gets one card per matching document: animal avatar and name, event date, source page label, the best fragment,
  "Open original" (the matching file) and a link to the document details. Nothing above the threshold → an explicit
  empty state. A note says how many of the owner's stored documents are not searchable yet.
- No result ever belongs to another account; inactive animals' documents are found.
- Every newly read document, every re-read after Retry, every document of a renamed animal, and every document
  already read before deployment is (re-)embedded by a background worker without user action.
- Embedding happens only in the `swedencentral` regional Standard deployment; the PRD records this and the
  abuse-monitoring exception.
- Verify: the API test suite (including new isolation and pipeline tests) passes; on Azure the backfill completes,
  calibrated threshold is set, and a query such as "serce Czarka" finds a cardiomyopathy document.

### Key Discoveries:

- Enqueue embedding work inside the reading publish transaction, mirroring how capture enqueues reading
  (`services/api/Documents/OwnedDocuments.cs:256`).
- The API managed identity is the PostgreSQL Entra admin (`infra/modules/postgres.bicep:52-60`), so an EF migration
  can run `CREATE EXTENSION vector` once `azure.extensions` allowlists `VECTOR` (pgvector 0.8.2 on Azure PG17).
- `Pgvector.EntityFrameworkCore` 0.3.0 with `Pgvector` 0.3.2 supports Npgsql EF Core 9 and 10; registration is
  needed on both the `NpgsqlDataSourceBuilder` and `UseNpgsql` options (`services/api/Data/DatabaseSetup.cs:30-57`).
- `text-embedding-3-large` regional Standard exists in `swedencentral`; `dimensions=1536` keeps the vector indexable
  later (pgvector `vector` index limit 2,000). v1 endpoint: `POST https://{resource}.openai.azure.com/openai/v1/embeddings`,
  role `Cognitive Services OpenAI User`, batches up to 2,048 inputs and 300,000 tokens per request.
- Exact search (no HNSW) gives perfect recall at thousands of chunks and avoids the post-filtering pitfall of
  approximate indexes with an owner predicate.
- S-03's rollout order (deploy with worker off → owner grants role → enable worker) is documented in
  `docs/deployment.md:92` and is reused.

## What We're NOT Doing

- Per-animal filter and the dedicated "search unavailable" notice (S-05). S-04 shows a generic error on failure and
  never shows substitute results.
- Keyword or hybrid search, reranking, query rewriting, generated answers (PRD FR-010/FR-012, §Business Logic).
- An ANN index (HNSW/IVFFlat); revisit only if measured search latency requires it.
- A document title field (PRD: capture needs no typing; the product does not guess).
- Applying for Azure "modified abuse monitoring" (parked in the roadmap).
- Removing OCR boilerplate lines before embedding; revisit after calibration if label lines cause false matches.
- A relative score cut; one absolute threshold only.
- Moving the App Service off F1; the existing B1 trigger in `context/foundation/infrastructure.md:41` stays the rule.

## Implementation Approach

Bottom-up in the established patterns: allow the data to leave (PRD) and provision the resource first, then storage
and a crash-safe embedding pipeline modelled on document reading, then the owner-scoped search endpoint with
isolation tests, then the UI from the redesign, and finally deploy, backfill, calibrate and measure. Embedding input
is the animal's current name followed by the chunk text, so the embedding format version includes the chunker
version, model, dimensions and input scheme, and a rename re-embeds that animal's documents.

## Critical Implementation Details

- **Ordering on Azure.** `azure.extensions` must be applied before the API starts with the new migration, or startup
  migration fails. The Bicep deployment precedes `az webapp deploy` in `deploy.yml`, which satisfies this; keep it.
  The embedding worker starts disabled; the owner grants the role, then enables it (as in S-03).
- **Stale-work fencing.** A chunk set is published only if the job's reading `published_run_generation` and the
  animal-name version it was computed for are still current, checked under the job row lock; otherwise the result is
  discarded and the job re-queued. Rename enqueues in the same transaction that changes the name.
- **Privacy in logs.** Never log query text, chunk text, fragments or provider payloads; log IDs, counts, states,
  failure codes and timings only (matches `docs/architecture.md:78`).

## Phase 1: PRD amendment and infrastructure

### Overview

Record the owner's EU-only decision and provision the embedding resource, the pgvector allowlist and configuration,
without enabling any worker.

### Changes Required:

#### 1. PRD privacy requirement

**File**: `context/foundation/prd.md`

**Intent**: Amend the Non-Functional Requirement so content may leave the product for text reading and for semantic
indexing (embeddings of document text and of search queries), processed only in the EU — the Azure OpenAI
regional deployment in Sweden — never used for training. Record that Azure abuse monitoring may store prompts in
the resource's geography with EEA reviewers, accepted by the owner on 2026-10-19. Close the embedding half of the
roadmap unknown.

**Contract**: Edit the bullet at `prd.md:314-321` and bump `version`/`updated` in the frontmatter if present; cite
Microsoft's "Data, privacy, and security for Azure OpenAI" page.

#### 2. Embedding resource

**File**: `infra/modules/embeddings.bicep` (new), `infra/main.bicep`

**Intent**: Create an Azure OpenAI account (custom subdomain, `disableLocalAuth: true`, public network like Document
Intelligence) and a `text-embedding-3-large` version `1` deployment with sku `Standard` (regional) in the backend
region, with a small TPM capacity.

**Contract**: Name computed in `main.bicep` as `oai-ogarniamy-${uniqueString(resourceGroup().id)}` (bootstrap must
match). Outputs: endpoint. Deployment name `text-embedding-3-large`. Region param stays `swedencentral`.

#### 3. pgvector allowlist

**File**: `infra/modules/postgres.bicep`

**Intent**: Allowlist the `vector` extension so the startup migration can create it.

**Contract**: `Microsoft.DBforPostgreSQL/flexibleServers/configurations@2025-08-01` named `azure.extensions`,
`value: 'VECTOR'`, `source: 'user-override'`, chained with `dependsOn` after the other server child resources (the
module comment at `:62` requires serialization).

#### 4. App settings

**File**: `infra/modules/app-service.bicep`, `infra/main.bicep`, `services/api/appsettings.json`

**Intent**: Pass embedding and search configuration as plain settings (no secrets).

**Contract**: `Embedding__Auth=AzureManagedIdentity`, `Embedding__Endpoint`, `Embedding__Deployment`,
`Embedding__Dimensions=1536`, `Embedding__WorkerEnabled=false` (first deploy), `Search__MinSimilarity` (provisional
`0.30`). `appsettings.json` gets the same section with `Auth=Disabled`, `WorkerEnabled=false` defaults.

#### 5. Owner-run role assignment

**File**: `infra/bootstrap/embedding-access.bicep` (new), `docs/deployment.md`

**Intent**: Grant `Cognitive Services OpenAI User` on the account to the API managed identity and developer
principals, run once by the owner (CI cannot grant roles), and document the S-04 activation steps.

**Contract**: Same structure and parameters as `infra/bootstrap/document-reading-access.bicep`; new activation
section in `docs/deployment.md` mirroring S-03's (deploy with worker off → grant → enable).

### Success Criteria:

#### Automated Verification:

- Bicep lints: `infra/deploy.sh lint`
- API builds: `dotnet build services/api/ogarniamy-zwierzaki-api.csproj`

#### Manual Verification:

- `infra/deploy.sh what-if` shows only the new OpenAI account and deployment, the `azure.extensions` configuration and new app settings
- PRD amendment reads correctly and states the EU-only scope and the abuse-monitoring exception

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual
confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Vector storage and embedding pipeline

### Overview

Store chunks with embeddings and keep them current through a crash-safe background worker.

### Changes Required:

#### 1. Packages, images and registration

**File**: `Directory.Packages.props`, `services/api/ogarniamy-zwierzaki-api.csproj`, `services/api/Data/DatabaseSetup.cs`,
`compose.yaml`, `services/api.Tests/ApiFactory.cs`, `docs/local-development.md`, `docs/testing.md`

**Intent**: Add pgvector support and use a pgvector-enabled PostgreSQL 17 image locally and in tests.

**Contract**: `Pgvector` 0.3.2 and `Pgvector.EntityFrameworkCore` 0.3.0 (versions only in `Directory.Packages.props`).
`UseVector()` on the data source builder and in `UseNpgsql` options. Image `pgvector/pgvector:0.8.2-pg17` in both
`compose.yaml` and `ApiFactory`. If type mapping fails after the migration creates the extension on a pooled data
source, reload types once after migrating.

#### 2. Schema and migration

**File**: `services/api/Data/AppDbContext.cs`, `services/api/DocumentEmbedding/DocumentChunk.cs`,
`services/api/DocumentEmbedding/DocumentEmbeddingJob.cs` (+ state enum file), `services/api/Data/Migrations/*`

**Intent**: Create the extension, a chunk table and an embedding job queue; backfill jobs for documents already read.

**Contract**:
- `HasPostgresExtension("vector")`.
- `document_chunks`: `document_id` (FK documents), `chunk_index`, `run_generation`, `document_file_id`,
  `file_position`, `source_page_number`, `start_offset`, `end_offset` (character offsets within the page text),
  `text`, `embedding vector(1536)`, `embedding_version text`; PK `(document_id, chunk_index)`; B-tree index on
  `document_id`. No ANN index.
- `document_embedding_jobs`: one row per document (PK `document_id`), `state` (`queued`/`embedding`/`embedded`/`failed`,
  text with check constraint), `due_at`, `attempt_count`, `lease_token`, `lease_expires_at`, `failure_code`,
  `requested_at`, `embedded_run_generation`, `embedded_animal_name`, timestamps; partial index on due queued jobs.
- Migration backfill: one queued job for every document whose reading job is `read`, written to be safe to re-run.
- One type per file; explicit snake_case constraint names as in existing migrations.

#### 3. Chunker

**File**: `services/api/DocumentEmbedding/PageChunker.cs`

**Intent**: Cut each non-blank page into chunks of about 1,200 characters with about 15% overlap, breaking on line
boundaries (falling back to whitespace), never crossing pages; a short page is one chunk.

**Contract**: Pure function from published pages to chunks with `(file, page, start_offset, end_offset, text)`;
deterministic ordering by file position, page, offset. The chunker version is part of `embedding_version`.

#### 4. Embedding client

**File**: `services/api/DocumentEmbedding/IEmbeddingClient.cs`, `AzureOpenAiEmbeddingClient.cs`,
`EmbeddingOptions.cs`, `EmbeddingAvailability.cs`, `EmbeddingSetup.cs`, `EmbeddingFailure.cs`

**Intent**: Raw REST client for the v1 embeddings endpoint with Entra tokens, following the Document Intelligence
adapter (named `HttpClient`, timeout from options, sanitized failure codes, transient vs terminal classification
honouring `Retry-After`, no text in logs). Misconfiguration makes embedding "unavailable" instead of stopping the API.

**Contract**: `Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken)`; body
`{ "model": <deployment>, "input": [...], "dimensions": 1536 }`; batches respect 2,048 inputs and the token cap
(split by an estimated size). Auth modes mirror `DocumentReadingAuthMode` (Disabled, AzureManagedIdentity,
DeveloperCredential); scope `https://cognitiveservices.azure.com/.default` (switch to `https://ai.azure.com/.default`
only if Azure rejects it during Phase 5). The returned vector count and dimension are validated.

#### 5. Job queue and worker

**File**: `services/api/DocumentEmbedding/DocumentEmbeddingJobs.cs`, `DocumentEmbeddingWorker.cs`,
`DocumentEmbeddingWakeUp.cs`, `Program.cs`

**Intent**: Claim due jobs with `FOR UPDATE SKIP LOCKED` and leases (as `DocumentReadingJobs.ClaimNextAsync`),
read the published pages and the animal's current name, chunk, embed `"{animal name}\n\n{chunk text}"`, then
replace the document's chunks and mark the job `embedded` in one transaction — only if the reading
`published_run_generation` and the animal name still match what was embedded; otherwise re-queue. Transient
failures retry with bounded backoff (5 attempts); terminal ones mark `failed`. Worker runs only when available and
`Embedding:WorkerEnabled` is true.

**Contract**: Worker exposes a `RunOnceAsync` test seam like `DocumentReadingWorker`. Wake-up signalled after reading
publication and after rename.

#### 6. Enqueue points

**File**: `services/api/DocumentReading/DocumentReadingJobs.cs`, `services/api/Animals/OwnedAnimals.cs`

**Intent**: Queue (or re-queue) an embedding job in the same transaction as reading publication, and for every
read document of an animal in the same transaction as a rename that changes the name.

**Contract**: Idempotent upsert setting `state='queued'`, `due_at=now()`, resetting attempts; a reading Retry that
re-publishes re-queues through the publish path. A no-op rename (same name) enqueues nothing.

#### 7. Tests

**File**: `services/api.Tests/FakeEmbeddingProvider.cs` (new), `services/api.Tests/DocumentEmbeddingTests.cs` (new),
`services/api.Tests/ApiFactory.cs`

**Intent**: A deterministic fake provider (e.g. hashed bag-of-words vectors) and tests for: chunk boundaries and
offsets; publish enqueues exactly one job (also on repeated publish); rename re-queues only that animal's read
documents; stale generation or name discards the result; transient failure retries and terminal failure stops;
backfill migration queues existing read documents; logs carry no text. `ApiFactory` pins `Embedding:Auth=Disabled`
and `WorkerEnabled=false`.

**Contract**: Synthetic text only (never real veterinary records).

### Success Criteria:

#### Automated Verification:

- API tests pass: `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`
- API builds: `dotnet build services/api/ogarniamy-zwierzaki-api.csproj`
- Migration applies on a fresh pgvector database and on a database migrated through `DocumentReading`

#### Manual Verification:

- Local run against `compose.yaml` with `DeveloperCredential` embeds a sample document and stores 1536-dimension vectors

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual
confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Owner-scoped search API

### Overview

`POST /api/search` returns the owner's matching documents with their best fragment.

### Changes Required:

#### 1. Endpoint and service

**File**: `services/api/Search/SearchEndpoints.cs`, `services/api/Search/OwnedSearch.cs`, request/response record
files, `services/api/Program.cs`

**Intent**: Authenticated POST with `AntiforgeryHeaderFilter`. Validate the query (trimmed, 1–500 characters, else
400 `invalid_query`). Embed the query text as-is (no prefix). Run one raw-SQL exact cosine query over
`document_chunks` restricted to stored documents the user owns through `animal_members` (role owner, any animal
activity) and to chunks whose `run_generation` equals the reading job's `published_run_generation`; take each
document's best chunk (max similarity), keep documents with similarity ≥ `Search:MinSimilarity`, order by
similarity, cap at 20 documents.

**Contract**:
- Request `{ "query": string }`.
- Response `{ items: SearchResult[], notSearchableCount: int }`; `SearchResult` = `documentId`, `animalId`,
  `animalName`, `eventDate`, `fileId`, `filePosition`, `sourcePageNumber`, `filePageCount`, `fragment`,
  `originalUrl` (same builder as `StoredDocumentFileResponse.cs:19`). No score in the response.
- `notSearchableCount` = the owner's stored documents whose reading is queued/reading or whose embedding job is not
  `embedded` for the published generation (failed reading excluded; it has its own status on the document page).
- Embedding unavailable or provider failure → 503 ProblemDetails `search_unavailable` (no partial or fallback
  results). Empty result is 200 with `items: []`.
- Explicit column list in SQL (no `SELECT *`); query vector as a parameter; owner predicate written like
  `OwnedDocumentReading.cs:145-152`.

#### 2. Tests

**File**: `services/api.Tests/SearchTests.cs`, `services/api.Tests/SearchIsolationTests.cs`

**Intent**: With the fake provider and documents embedded via `RunOnceAsync`: anonymous 401 without redirect;
missing antiforgery refused; two accounts with the same unique token each see only their own documents and the
other's `originalUrl` returns 404; an inactive animal's document is found; one result per document with its best
fragment; below-threshold returns empty; stale chunks of an old generation are ignored; `notSearchableCount` counts
queued and not-yet-embedded documents; provider failure yields 503 `search_unavailable`; query text never logged;
invalid query 400.

**Contract**: Follows `DocumentIsolationTests.cs:10-100` structure and helpers in `DocumentApi.cs`/`TestAccounts.cs`.

### Success Criteria:

#### Automated Verification:

- API tests pass: `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`
- Isolation tests for search pass (anonymous, other account, inactive animal)

#### Manual Verification:

- A local search against embedded sample documents returns the expected document with a readable fragment

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual
confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 4: Search screen

### Overview

Replace the placeholder with the search experience from the redesign references.

### Changes Required:

#### 1. API client

**File**: `apps/web/src/lib/api.ts`

**Intent**: Add search types and `api.search(query)` as a POST through the existing antiforgery mutation helper.

**Contract**: `SearchResult`, `SearchResponse` interfaces matching Phase 3.

#### 2. Search view

**File**: `apps/web/src/components/SearchView.tsx` (new), `apps/web/src/pages/search.astro`,
`apps/web/src/pages/en/search.astro`, `apps/web/src/styles/tokens.css` (only if a needed class is missing)

**Intent**: `<form role="search">` with a visually hidden label, hint text and example queries; states idle,
loading (persistent `role="status"` with continuous indicator), results (count label for the query, cards),
empty (`role="status"`, "No documents on this topic"), error (generic `Alert` with Retry; never substitute
results). Card: `AnimalAvatar` + name, event date, page label, fragment, "Open original" (`originalUrl`, new tab,
only if it starts with `/api/documents/`) and a link to the document details page. Note above results when
`notSearchableCount > 0`. 401 redirects to sign-in like other views. Reuse `Alert`, `PageState`, `Icon`, `.loading`,
document row classes.

**Contract**: Pages keep `SessionGate` + `AppShell active="search"`; the smoke marker is untouched.

#### 3. Messages

**File**: `apps/web/src/i18n/en.ts`, `apps/web/src/i18n/pl.ts`, `apps/web/src/i18n/format.ts` (if a new counted key)

**Intent**: Add every new string to both catalogues (Polish only in `pl.ts`), plural forms through `countLabel`.

### Success Criteria:

#### Automated Verification:

- Type check passes: `npm run check --prefix apps/web`
- Build passes: `npm run build --prefix apps/web`
- Every new message key exists in both catalogues
- No Polish outside `pl.ts` in changed files

#### Manual Verification:

- Search screen states (idle, loading, results, empty, error) at 390 px and 1280 px, keyboard and screen-reader announcements
- "Open original" opens the right file; details link opens the document page

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual
confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 5: Deployment, backfill, calibration and documentation

### Overview

Ship to Azure, activate embedding, calibrate the threshold on real documents and record the results.

### Changes Required:

#### 1. Activation

**File**: `infra/modules/app-service.bicep` (or `main.bicep`), `docs/deployment.md`

**Intent**: After the owner grants the role, set `Embedding__WorkerEnabled=true`; the backfill drains.

#### 2. Calibration

**File**: `context/changes/semantic-search-with-fragments/notes/threshold-calibration.md` (new),
`infra/main.bicep`/`app-service.bicep`

**Intent**: The owner runs about 20 Polish queries against their real archive (including animal names and
vocabulary mismatches like "serce" vs "kardiomiopatia"), recording expected and returned documents and scores
observed in a developer-only run. Pick the lowest threshold that removes false matches; commit it to Bicep.
Record no medical content in the note beyond query wording the owner approves.

#### 3. Documentation and roadmap

**File**: `docs/architecture.md`, `docs/deployment.md`, `docs/local-development.md`, `docs/testing.md`,
`context/foundation/roadmap.md`, `context/foundation/infrastructure.md`

**Intent**: Describe the embedding pipeline, search endpoint, configuration, privacy scope and tests; replace the
"planned pgvector" line; record the Azure OpenAI resource and cost; add "modified abuse monitoring" to Parked;
resolve the embedding half of the S-03 unknown.

### Success Criteria:

#### Automated Verification:

- Deploy workflow and smoke checks pass on main
- All tests and frontend checks pass: `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`, `npm run check --prefix apps/web`, `npm run build --prefix apps/web`

#### Manual Verification:

- Backfill completes: every read document has an `embedded` job for its published generation
- Calibration note recorded and threshold committed; "serce Czarka" finds the cardiomyopathy document
- Search answers within about 2 seconds on a warm app (cold-start time noted against the B1 trigger)
- A second test account sees none of the owner's results on Azure

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual
confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Testing Strategy

### Unit Tests:

- Chunker boundaries, overlap, offsets, blank pages, very long lines.
- Embedding client request shape, batching limits, failure classification, dimension validation.

### Integration Tests:

- Pipeline over real PostgreSQL with pgvector (Testcontainers) and the fake provider: enqueue, claim, fencing,
  retries, rename, backfill.
- Search endpoint: isolation (401, other account, originals 404), inactive animal, threshold, best-fragment,
  stale generation, `notSearchableCount`, 503, logging.

### Manual Testing Steps:

1. Locally with `DeveloperCredential`: capture a synthetic document, wait for reading and embedding, search by a
   synonym, open the original.
2. On Azure: run the calibration queries, confirm the empty state on an unrelated query.
3. Rename an animal and confirm its documents are re-embedded (job states) and still found.
4. Sign in as a second account and confirm no results from the owner's archive.

## Performance Considerations

Query latency is one embedding call (~hundreds of ms) plus an exact scan over the owner's chunks (thousands of
1536-float rows, a few MB). Embedding work shares the F1 CPU quota with reading; batching keeps it network-bound.
If latency exceeds ~2 s warm, measure first; HNSW with iterative scan or B1 are the follow-ups.

## Migration Notes

Forward-only expand migration: adds the extension, two tables and a backfill; nothing existing changes. Rollback of
the app package leaves unused tables, which older builds ignore. `embedding_version` lets a later model or chunker
change re-queue documents.

## References

- Research: `context/changes/semantic-search-with-fragments/research.md`
- Reading pipeline pattern: `services/api/DocumentReading/DocumentReadingJobs.cs:28-58`, `:246-300`
- Isolation test template: `services/api.Tests/DocumentIsolationTests.cs:10-100`
- S-03 rollout: `docs/deployment.md:92`
- Azure OpenAI data privacy: https://learn.microsoft.com/azure/foundry/responsible-ai/openai/data-privacy
- Model/region availability: https://learn.microsoft.com/azure/foundry/foundry-models/concepts/models-sold-directly-by-azure-region-availability
- pgvector on Azure: https://learn.microsoft.com/azure/postgresql/extensions/how-to-use-pgvector
- pgvector-dotnet: https://github.com/pgvector/pgvector-dotnet

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: PRD amendment and infrastructure

#### Automated

- [x] 1.1 Bicep lints: `infra/deploy.sh lint`
- [x] 1.2 API builds: `dotnet build services/api/ogarniamy-zwierzaki-api.csproj`

#### Manual

- [ ] 1.3 `infra/deploy.sh what-if` shows only the new OpenAI account and deployment, the `azure.extensions` configuration and new app settings
- [ ] 1.4 PRD amendment reads correctly and states the EU-only scope and the abuse-monitoring exception

### Phase 2: Vector storage and embedding pipeline

#### Automated

- [ ] 2.1 API tests pass: `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`
- [ ] 2.2 API builds: `dotnet build services/api/ogarniamy-zwierzaki-api.csproj`
- [ ] 2.3 Migration applies on a fresh pgvector database and on a database migrated through `DocumentReading`

#### Manual

- [ ] 2.4 Local run against `compose.yaml` with `DeveloperCredential` embeds a sample document and stores 1536-dimension vectors

### Phase 3: Owner-scoped search API

#### Automated

- [ ] 3.1 API tests pass: `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`
- [ ] 3.2 Isolation tests for search pass (anonymous, other account, inactive animal)

#### Manual

- [ ] 3.3 A local search against embedded sample documents returns the expected document with a readable fragment

### Phase 4: Search screen

#### Automated

- [ ] 4.1 Type check passes: `npm run check --prefix apps/web`
- [ ] 4.2 Build passes: `npm run build --prefix apps/web`
- [ ] 4.3 Every new message key exists in both catalogues
- [ ] 4.4 No Polish outside `pl.ts` in changed files

#### Manual

- [ ] 4.5 Search screen states (idle, loading, results, empty, error) at 390 px and 1280 px, keyboard and screen-reader announcements
- [ ] 4.6 "Open original" opens the right file; details link opens the document page

### Phase 5: Deployment, backfill, calibration and documentation

#### Automated

- [ ] 5.1 Deploy workflow and smoke checks pass on main
- [ ] 5.2 All tests and frontend checks pass

#### Manual

- [ ] 5.3 Backfill completes: every read document has an `embedded` job for its published generation
- [ ] 5.4 Calibration note recorded and threshold committed; "serce Czarka" finds the cardiomyopathy document
- [ ] 5.5 Search answers within about 2 seconds on a warm app (cold-start time noted against the B1 trigger)
- [ ] 5.6 A second test account sees none of the owner's results on Azure
