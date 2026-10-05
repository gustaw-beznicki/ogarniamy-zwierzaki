# Read document content Implementation Plan

## Overview

Deliver S-03: read stored veterinary PDFs/photos in the background without transcription, retain page/file provenance for S-04, and show honest reading status while originals stay openable.

Planning complexity HIGH and a shared five-question budget with S-06 were confirmed on 2026-10-04. The owner selected Azure with a bounded retention exception, local PDF text with OCR fallback, and automatic retries plus owner Retry. The five-phase structure was approved. Planning does not authorize implementation, commits or production mutations.

## Current State Analysis

Local evidence at commit `8a4887c081d510295b9c50b60a25d0eb6b54d03c` includes S-02 document metadata, immutable Blob originals, owner-scoped retrieval, capture UI and PostgreSQL/Azurite integration fixtures. S-02 still has verification pending; this is not evidence of a completed production rollout. There is no OCR adapter, durable work queue or hosted processing service in the inspected host.

Completion already holds a document lock and commits Stored/UploadedAt plus the capture preference atomically. Attach durable jobs to that transaction. Stable completion responses must remain independent of asynchronous status. Documents belong to animals; owner-facing reads must use owner memberships, including for inactive animals.

The configured App Service is F1. Existing infrastructure decisions assign B1/Always On and private database connectivity to S-03. CI Contributor cannot create role assignments; use separate owner-run Bicep for OCR runtime access without widening CI. Production state and real-photo quality were not queried during planning.

## Desired End State

- Completed captures and previously Stored documents acquire durable reading work; Uploading documents do not.
- PDFs with usable page text are extracted locally; pages requiring OCR and JPEG/PNG originals use Azure Document Intelligence `prebuilt-read`, API `2024-11-30`.
- Reading survives restarts and competing worker instances, publishes one complete text revision with source-file/page provenance, and does not overwrite original bytes.
- The owner sees queued, reading, content-read or failed status on document detail, with Retry after failure and a persistent route to every original. Content read means extraction completed, not that semantic search is implemented.
- Provider input/results are deleted promptly after durable retrieval, with durable cleanup retry and the owner-approved provider retention exception. No custom model training or content-bearing diagnostics are enabled.

### Key Discoveries:

- `services/api/Documents/OwnedDocuments.cs:181` — transactional completion and existing lock order.
- `services/api/Documents/OwnedDocuments.cs:279` — membership-based owner isolation.
- `services/api/Storage/IOriginalStorage.cs:3` — immutable writes/read-only source streams.
- `services/api/Documents/DocumentPage.cs:3` — this is a pagination DTO, not a PDF page entity.
- `services/api.Tests/ApiFactory.cs:9` — real PostgreSQL/Azurite fixtures.
- `infra/modules/app-service.bicep:52` — Always On depends on SKU.
- Azure's free OCR tier processes fewer pages and smaller files than S-02 accepts; choose S0, with no silently truncated PDF analysis.

## What We're NOT Doing

- Embeddings, chunk ranking, pgvector, semantic search or search filters; these belong to S-04/S-05.
- Generated summaries, medical advice, date/type inference, transcription requirements or extraction-review/confidence queues.
- Self-hosted OCR, custom OCR training, format expansion, editing/deleting originals, or replacing originals with searchable PDFs.
- A separate broker/Queue Storage/WebJob deployment. Durable PostgreSQL work and an API-hosted worker are sufficient for this slice.
- Animal lifecycle UI or capture eligibility changes, owned by S-06.
- Claims of immediate physical provider deletion, guaranteed OCR correctness, or verified phone/deployed behavior without evidence.

## Implementation Approach

Use PdfPig for local PDF text and a narrow Azure REST adapter over authenticated HTTP for OCR. Pin the current compatible PdfPig version during implementation in `Directory.Packages.props`; keep package references versionless. Azure calls use the existing API managed identity in production and a development-only credential configured outside tracked files. Submit original bytes directly rather than a public URL or SAS. Automatic HTTP retries must not blindly replay an Analyze submission whose outcome is ambiguous.

Use PostgreSQL processing jobs and per-file run records to persist stages, operation IDs, retrieved text and cleanup obligations. A BackgroundService uses short database claims, scoped DbContexts and lease fencing; it releases transactions before network work. The initial worker concurrency is one. Content status and original storage state are separate.

The owner's Azure retention exception has been recorded in `context/foundation/prd.md`. Persist retrieved output before requesting provider deletion; preserve cleanup work independently of reading success/failure. A lost submission response may leave an unknown provider operation that cannot be deleted early; document that bounded-retention limitation explicitly.

## Critical Implementation Details

### Parallel implementation and migrations

Both changes release only after S-02 verification. Use separate worktrees based on the same verified S-02 integration commit. S-06 generates/merges its animal migration first; S-03 then rebases and generates its processing migration from the integrated model. Domain implementation and tests may proceed in parallel before that integration checkpoint. Never resolve the snapshot by discarding either model.

S-03 owns new processing files, completion job scheduling, document-status endpoints/component and OCR/network infrastructure. S-06 owns Animals, active capture defaults, fresh upload eligibility and the profile/capture form. Compose their disjoint OwnedDocuments edits explicitly. Integrate shared API client, locale files, Program, DbContext, test fixture and docs sequentially. Keep original detail/list/completion DTOs stable to reduce conflicts; reading status is fetched separately.

### Provider and local parsing limits

Maximum reading scope is 2,000 PDF pages and 10,000,000 extracted characters per document, preserving S-02 storage acceptance independently. Exceeding a processing limit produces an explicit reading failure without truncating a success result. Azure image dimensions/password restrictions also affect reading rather than stored-original acceptance. Local PDF parsing must execute in a bounded child process so cancellation/time limits can terminate CPU-bound parsing without taking down the API; keep the helper in the API application/deployment, not a new hosted service.

### Publication and cleanup

A worker may publish only with its current unexpired lease token and run generation. Retries cannot publish partial text as a complete revision or discard pending provider cleanup. No activity predicate belongs in worker candidates, status authorization or original reads.


## Phase 1: OCR feasibility and infrastructure readiness

### Overview

Establish the selected provider contract and a deployable, continuously running environment before relying on reading jobs.

### Changes Required:

#### 1. Provider quality harness and privacy evidence

**Files**: services/api/DocumentReading/OcrProbe.cs (new), context/changes/read-document-content/notes/ocr-feasibility.md (created during implementation)

**Intent**: Test the product's largest assumption before completing the pipeline.

**Contract**: Provide a development-only probe that exercises prebuilt-read and cleanup with synthetic fixtures. Manually check five owner-selected real photos containing representative print, stamps, handwriting and abbreviations only after the owner provides those inputs for this purpose. For each, record whether topic-bearing terms are faithfully recoverable and omissions could mislead search; do not commit records or extracted content. Failure to read materially useful content blocks acceptance, without redefining success as a 200 response. Record a sanitized verdict and provider/no-training assurance references; no training/feedback uploads.

#### 2. Infrastructure and runtime authorization

**Files**: infra/modules/document-intelligence.bicep, network.bicep, postgres-private-endpoint.bicep (new), infra/main.bicep, infra/modules/app-service.bicep, postgres.bicep, infra/environments/mvp.bicepparam, infra/deploy.sh, infra/bootstrap/document-reading-access.bicep (new)

**Intent**: Support unattended processing with least-privilege runtime access and the previously planned private database transition.

**Contract**: Provision S0 Document Intelligence in swedencentral with a custom subdomain, disable local-key auth, pass its non-secret endpoint/model/version settings, and switch App Service to B1/Always On. Add VNet integration with separate delegated API and private-endpoint subnets, PostgreSQL private endpoint and linked private DNS. Stage publicNetworkAccess as an explicit parameter: private connectivity first, confirm DNS/TLS/database health from the API, then disable public access in a later approved apply. Preserve linked SWA inbound routing; do not make the API inbound endpoint private. Adjust firewall reconciliation to skip public rules in private mode; incremental omission does not remove old rules, so disable public access explicitly. Owner-run Bicep grants API identity Cognitive Services User scoped to the OCR resource; CI creates no RBAC. Provider registrations and any read-only what-if role additions remain owner bootstrap work. No apply is authorized by this plan.

#### 3. Configuration and non-regressing verification

**Files**: services/api/DocumentReading/DocumentReadingOptions.cs, DocumentReadingSetup.cs (new), services/api/appsettings.json, Program.cs, docs/local-development.md

**Intent**: Make later processing setup explicit while keeping the current original path usable.

**Contract**: Use managed identity and cognitive-services token scope in production; development credentials follow existing user-secrets conventions. Worker activation is separately configurable. Misconfigured OCR disables reading with actionable diagnostics rather than failing API startup or changing database-only health semantics. The local probe is opt-in; ordinary tests never call Azure.

### Success Criteria:

#### Automated Verification:

- API restore/build pass: `dotnet restore services/api/ogarniamy-zwierzaki-api.csproj` and `dotnet build services/api/ogarniamy-zwierzaki-api.csproj --no-restore`.
- Bicep lint and read-only what-if pass: `infra/deploy.sh lint` and `infra/deploy.sh what-if`; owner access template validates without application.
- Synthetic probe verifies analysis, result retrieval and deletion request without altering originals; no live-provider call runs in ordinary CI.

#### Manual Verification:

- Owner confirms the five-photo quality verdict and reviews S0/B1 cost implications, privacy assurance and staged network/RBAC diff before any production mutation.

**Implementation Note**: Keep manual items pending until their evidence is confirmed; follow the implementation workflow before proceeding past a manual gate. Production mutations require separate approval.

## Phase 2: Durable jobs and text model

### Overview

Represent restart-safe reading independently of capture storage and schedule it without a commit/enqueue gap.

### Changes Required:

#### 1. Processing schema and provenance

**Files**: services/api/DocumentReading/DocumentReadingJob.cs, DocumentReadingState.cs, DocumentReadingRunFile.cs, DocumentTextPage.cs (new), services/api/Data/AppDbContext.cs, Data/Migrations/<timestamp>_DocumentReading.cs and generated companions

**Intent**: Keep derived text/version history separate from original persistence.

**Contract**: One job per DocumentId, with state queued/reading/read/failed, run generation, due time, attempt count, lease token/expiry, sanitized failure code, timestamps and owner-retry counters. Per-file run records retain provider operation ID, selected page ranges, stage, fetched text and cleanup state. Published pages carry DocumentId, run generation, DocumentFileId, source page number, file position, extraction method and text, unique within the revision/source page. Constrain valid references/states and due-job indexes. Use one C# type per file. Published content is complete for the revision; empty pages are represented, but an entirely empty document is not content-read.

#### 2. Transactional scheduling and historical backfill

**Files**: services/api/Documents/OwnedDocuments.cs (completion transaction), services/api/DocumentReading/DocumentReadingJobs.cs (new), processing migration

**Intent**: Ensure committed originals have durable reading work even if the process stops immediately.

**Contract**: Insert a unique pending job inside the existing first-completion transaction. Repeated completion leaves job/run state unchanged and keeps its stable original DTO. Migration inserts pending work for pre-existing Stored documents only, with conflict-safe uniqueness; do not depend on requests to discover historical jobs. System job access is an explicit trusted-worker boundary, documented separately from user-scoped document access.

#### 3. Claims and publication boundary

**Files**: services/api/DocumentReading/DocumentReadingJobs.cs, services/api.Tests/DocumentReadingRepositoryTests.cs (new)

**Intent**: Recover abandoned work and prevent competing workers from publishing stale results.

**Contract**: Claim due rows using short transactions and SKIP LOCKED or equivalent atomic conditional updates. Use database time for lease comparison; claims get unique tokens. Lease heartbeat, result writes, completion/failure and retry transitions require current token/generation. Commit text pages and read state together. Recover expired work without discarding known remote operation IDs or cleanup obligations. Keep current published revisions until replacement publication, though successful reprocessing is outside owner Retry scope.

### Success Criteria:

#### Automated Verification:

- API build and integration suite pass: `dotnet build services/api/ogarniamy-zwierzaki-api.csproj` and `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`.
- Fresh and S-02 databases migrate additively; Stored backfill excludes Uploading, repeated/concurrent completion schedules one job, and scheduling failure rolls back completion.
- Real PostgreSQL tests prove competing claims, expiry recovery, stale-token fencing and atomic complete-revision publication.

## Phase 3: Extraction worker and recovery

### Overview

Read page content, resume remote operations and clean up provider data without coupling failures to archive access.

### Changes Required:

#### 1. Local PDF reader and OCR adapter

**Files**: services/api/DocumentReading/IDocumentTextReader.cs, PdfTextReader.cs, PdfTextReaderProcess.cs, AzureDocumentTextReader.cs, ReadPageResult.cs, DocumentReadFailure.cs (new), services/api/Program.cs, Directory.Packages.props, services/api/ogarniamy-zwierzaki-api.csproj

**Intent**: Read existing text locally and use the selected OCR service where needed, with reproducible source order.

**Contract**: PdfPig uses layout-aware extraction, not raw page.Text order. A page is locally usable when it yields at least one letter/digit and no invalid replacement/null/control characters other than whitespace; pages containing raster image content also go through OCR to avoid accepting a text caption while missing scanned body text. Local-only PDFs never call Azure. For mixed PDFs submit the original bytes with explicit pages ranges and merge OCR output for those original page numbers; do not rewrite originals or claim excluded-page bytes were never transmitted. Every JPEG/PNG file is read in manifest order. Parser errors trigger provider fallback where possible; password-protected/malformed inputs that neither path reads fail honestly. The child process has a 60-second parse timeout and capped output; dispose temporary artifacts with restrictive access, no persistent source cache. Verify the stable compatible PdfPig release and pin centrally.

#### 2. Hosted worker, resumable stages and cleanup

**Files**: services/api/DocumentReading/DocumentReadingWorker.cs, DocumentReadingCleanup.cs (new), DocumentReadingJobs.cs, DocumentReadingSetup.cs, services/api/Program.cs

**Intent**: Deliver restart-safe progress without holding requests or database locks during OCR.

**Contract**: Register a BackgroundService using scoped services after normal database startup. Defaults: one job at a time, poll every five seconds, two-minute renewable leases with 30-second heartbeats, 30-second HTTP calls, and a 30-minute processing-attempt deadline. Persist remote operation IDs as soon as available, poll saved IDs on restart, checkpoint fetched output before cleanup and publish only after all source files/page ranges succeed with some readable document text. Use bounded streaming and the stated page/character caps. Classify throttling/network/5xx/storage outage as transient; retry at 30 seconds, two minutes, ten minutes and 30 minutes (five attempts including the initial attempt), honoring longer Retry-After. Encrypted/unsupported, empty output and processing-limit errors are terminal. Remote auth/config errors become explicit failed status and diagnostics rather than endless retries. Lease loss stops reading/publication.

#### 3. Provider cleanup and fault coverage

**Files**: services/api/DocumentReading/DocumentReadingCleanup.cs, services/api.Tests/DocumentReadingWorkerTests.cs, DocumentReadingCleanupTests.cs, PdfTextReaderTests.cs (new), services/api.Tests/ApiFactory.cs, synthetic fixtures

**Intent**: Prove recovery at provider/database boundaries and avoid nondeterministic production calls in tests.

**Contract**: Known Analyze IDs retain durable deletion work after output is checkpointed, including terminal analysis failure; try deletion promptly and retry independently after restart. 204 or authenticated result-not-found marks cleanup acknowledged, not verified physical erasure. Unacknowledged operations remain observable until deletion/expiry; do not erase their records on owner Retry. Ambiguous submissions with no result ID are logged without contents and rely on the accepted bounded provider retention; do not promise exactly-once billing. Tests disable autonomous workers by default and use a fake HTTP provider/deterministic clock while exercising real job storage. Synthetic fixtures cover text, scanned and mixed PDFs, ordered photos, blanks and malformed/password inputs.

### Success Criteria:

#### Automated Verification:

- API build and integration suite pass: `dotnet build services/api/ogarniamy-zwierzaki-api.csproj` and `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`.
- Extraction tests prove local-only PDF routing, mixed-page fallback/provenance, image order, empty/limit failures and no partial success publication.
- Fault tests prove restart after submission/retrieval, lease loss, bounded retries, independent cleanup recovery and byte-identical original retrieval during reading failure.

#### Manual Verification:

- A controlled live-provider run verifies saved operation recovery and early deletion acknowledgement using non-sensitive fixtures; sanitized evidence records any provider limitations.

**Implementation Note**: Keep manual items pending until their evidence is confirmed; follow the implementation workflow before proceeding past a manual gate. Production mutations require separate approval.

## Phase 4: Reading status and Retry

### Overview

Expose owner-isolated processing progress and safe retry without changing capture completion or original retrieval.

### Changes Required:

#### 1. Owner status and retry routes

**Files**: services/api/DocumentReading/OwnedDocumentReading.cs, DocumentReadingEndpoints.cs, DocumentReadingResponse.cs (new), services/api/Program.cs

**Intent**: Make reading state visible while preserving tenant isolation and idempotent retry behavior.

**Contract**: GET /api/documents/{id:guid}/reading returns {state, failureCode, canRetry, retryAfter, completedAt}; a Stored owned document only. Missing/foreign/Uploading resources return 404, anonymous 401. POST on the same route's /retry subpath requires the existing antiforgery header. Under the job lock, retry only failed jobs, increment generation/reset reading attempts, and keep pending cleanup. Repeated POST while queued/reading is a no-op returning current status; read state is unchanged. Enforce a durable limit of three accepted owner retries per document per hour with 429/retryAfter, surviving API restart/replicas. Do not return provider messages, extracted text, blob keys or remote IDs.

#### 2. Independent detail status component

**Files**: apps/web/src/components/DocumentReadingStatus.tsx (new), DocumentDetails.tsx, apps/web/src/lib/api.ts, apps/web/src/i18n/en.ts, pl.ts

**Intent**: Show progress/failure and recovery while leaving originals usable.

**Contract**: Mount the status component alongside stored-original information. Poll its endpoint every five seconds while queued/reading, pause when hidden, cancel on unmount/account change and stop after ten minutes with a Refresh action. Failed polling is an honest status-unavailable notice that does not replace document details or disable originals. Content-read copy does not promise searchability. Retry requires an explicit click, shows rate limits/errors, uses account-aware antiforgery without blind replay, and resumes status polling on success. No status changes to stable completion/detail DTOs or the S-06 profile list are required.

### Success Criteria:

#### Automated Verification:

- Frontend check/build and API suite pass: `npm run check --prefix apps/web`, `npm run build --prefix apps/web` and `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`.
- API tests prove status/retry owner isolation, antiforgery rejection, retry coalescing, durable cooldown, inactive-animal access and unchanged completion responses.

#### Manual Verification:

- PL/EN desktop and phone detail views show queued/reading/read/failed, recover through Retry, stop polling correctly and open originals during provider/status failure.

**Implementation Note**: Keep manual items pending until their evidence is confirmed; follow the implementation workflow before proceeding past a manual gate. Production mutations require separate approval.

## Phase 5: Deployment verification and documentation

### Overview

Close deployed quality/recovery evidence and explain implemented reading without claiming search exists.

### Changes Required:

#### 1. Integrated acceptance and operations

**Files**: services/api.Tests/DocumentReadingAcceptanceTests.cs (new), README.md, docs/architecture.md, local-development.md, testing.md, deployment.md, scripts/smoke.sh, context/changes/read-document-content/notes/reading-runbook.md (new during implementation)

**Intent**: Document and prove the selected behavior, deployment sequence and recoverability.

**Contract**: Retain current CI checks and non-mutating smoke behavior; add anonymous status/retry-route checks without uploading owner data. Document parser helper packaging, job states, configuration, due/failed/cleanup inspection, paused-worker recovery, and rollback retaining originals/text/jobs. Record one-time provider/RBAC/network applies and live evidence under change notes. Verify B1/Always On, OCR identity, private database resolution and disabled public access after staged approved rollout. Test two synthetic owners and an inactive profile if S-06 is integrated. Monitor IDs/state/timing only; omit extracted text, source filenames, provider payloads and credentials. Reading outages do not change original health dependencies.

### Success Criteria:

#### Automated Verification:

- Full documented checks pass: `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`, `dotnet build services/api/ogarniamy-zwierzaki-api.csproj`, `npm run check --prefix apps/web`, `npm run build --prefix apps/web` and `infra/deploy.sh lint`.
- Read-only what-if matches the staged infrastructure scope and post-deployment non-mutating smoke checks pass.
- Integrated S-03/S-06 tests retain inactive archive access, stale-edit rejection and repeat-completion behavior after both migrations.

#### Manual Verification:

- After separately approved deployment, two synthetic accounts prove status/original isolation, a restart recovers jobs/cleanup, and idle-period processing works with Always On.
- Owner reviews OCR quality evidence, retention/cleanup limitations and rollback runbook; original retrieval works with reading disabled.

**Implementation Note**: Keep manual items pending until their evidence is confirmed; follow the implementation workflow before proceeding past a manual gate. Production mutations require separate approval.

## Testing Strategy

### Unit Tests:

Test extraction ordering/routing, output limits, failure classification and parser process termination with meaningful fixtures. Avoid tests that merely duplicate DTO definitions.

### Integration Tests:

Use real PostgreSQL/Azurite and deterministic fake provider responses. Exercise transaction rollback, concurrent completion/claims, lease expiry, publication fencing, crash stages, cleanup retry, owner Retry limits and two-account status/original isolation. Original bytes must match before, during and after reading failures.

### Manual Testing Steps:

1. Evaluate the five real photos without committing their content.
2. Store a text PDF, scanned/mixed PDF and reordered photos; inspect internal text/provenance in a controlled development session.
3. Stop/restart work at known provider stages and confirm recovery and deletion acknowledgement.
4. View PL/EN status and Retry through the deployed SWA origin with two synthetic owners.
5. Open an inactive animal's original while reading is unavailable, and verify database private connectivity/Always On after approved rollout.

## Performance Considerations

Process files sequentially at default concurrency one; stream source reads and avoid retaining an entire ten-file capture in memory. The parser child limits CPU-bound failure impact; OCR can take minutes and must not run inside SWA request timeouts. Keep claim transactions short and indexed. Five-second status polling is detail-only, not one request per history row. The processing caps are explicit failure boundaries, not permission to truncate content and report success.

## Migration Notes

Generate the processing migration after S-06's migration is integrated. Backfill only Stored documents, with unique jobs. Keep storage constraints and original retrieval unchanged. Deployment order: B1/private connectivity and OCR access prerequisites, schema/application with reading initially disabled, verified configuration, then worker activation; disable public database access only after private-path evidence. Rollback stops the worker and restores code while keeping originals, published text, jobs and cleanup records. If a pre-S-03 release stores new documents without jobs, a forward recovery reconciliation inserts missing Stored jobs before re-enabling processing.

## References

- `context/changes/read-document-content/research.md`; companion `context/changes/animal-profiles-and-history/plan.md`.
- `context/foundation/prd.md`, `roadmap.md`, `infrastructure.md`, `lessons.md`, `shape-notes.md` (original discovery).
- [Azure Read model](https://learn.microsoft.com/en-us/azure/ai-services/document-intelligence/prebuilt/read?view=doc-intel-4.0.0).
- [Provider privacy](https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/document-intelligence/data-privacy-security) and [Delete Analyze Result](https://learn.microsoft.com/en-us/rest/api/aiservices/document-models/delete-analyze-result?view=rest-aiservices-v4.0+(2024-11-30)).
- [PdfPig upstream](https://github.com/UglyToad/PdfPig).
- [App Service VNet integration](https://learn.microsoft.com/en-gb/azure/app-service/overview-vnet-integration); [PostgreSQL Private Link](https://learn.microsoft.com/en-us/azure/postgresql/network/concepts-networking-private-link).

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: OCR feasibility and infrastructure readiness

#### Automated

- [ ] 1.1 API restore/build pass: `dotnet restore services/api/ogarniamy-zwierzaki-api.csproj` and `dotnet build services/api/ogarniamy-zwierzaki-api.csproj --no-restore`.
- [ ] 1.2 Bicep lint and read-only what-if pass: `infra/deploy.sh lint` and `infra/deploy.sh what-if`; owner access template validates without application.
- [ ] 1.3 Synthetic probe verifies analysis, result retrieval and deletion request without altering originals; no live-provider call runs in ordinary CI.

#### Manual

- [ ] 1.4 Owner confirms the five-photo quality verdict and reviews S0/B1 cost implications, privacy assurance and staged network/RBAC diff before any production mutation.

### Phase 2: Durable jobs and text model

#### Automated

- [ ] 2.1 API build and integration suite pass: `dotnet build services/api/ogarniamy-zwierzaki-api.csproj` and `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`.
- [ ] 2.2 Fresh and S-02 databases migrate additively; Stored backfill excludes Uploading, repeated/concurrent completion schedules one job, and scheduling failure rolls back completion.
- [ ] 2.3 Real PostgreSQL tests prove competing claims, expiry recovery, stale-token fencing and atomic complete-revision publication.

### Phase 3: Extraction worker and recovery

#### Automated

- [ ] 3.1 API build and integration suite pass: `dotnet build services/api/ogarniamy-zwierzaki-api.csproj` and `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`.
- [ ] 3.2 Extraction tests prove local-only PDF routing, mixed-page fallback/provenance, image order, empty/limit failures and no partial success publication.
- [ ] 3.3 Fault tests prove restart after submission/retrieval, lease loss, bounded retries, independent cleanup recovery and byte-identical original retrieval during reading failure.

#### Manual

- [ ] 3.4 A controlled live-provider run verifies saved operation recovery and early deletion acknowledgement using non-sensitive fixtures; sanitized evidence records any provider limitations.

### Phase 4: Reading status and Retry

#### Automated

- [ ] 4.1 Frontend check/build and API suite pass: `npm run check --prefix apps/web`, `npm run build --prefix apps/web` and `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`.
- [ ] 4.2 API tests prove status/retry owner isolation, antiforgery rejection, retry coalescing, durable cooldown, inactive-animal access and unchanged completion responses.

#### Manual

- [ ] 4.3 PL/EN desktop and phone detail views show queued/reading/read/failed, recover through Retry, stop polling correctly and open originals during provider/status failure.

### Phase 5: Deployment verification and documentation

#### Automated

- [ ] 5.1 Full documented checks pass: `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`, `dotnet build services/api/ogarniamy-zwierzaki-api.csproj`, `npm run check --prefix apps/web`, `npm run build --prefix apps/web` and `infra/deploy.sh lint`.
- [ ] 5.2 Read-only what-if matches the staged infrastructure scope and post-deployment non-mutating smoke checks pass.
- [ ] 5.3 Integrated S-03/S-06 tests retain inactive archive access, stale-edit rejection and repeat-completion behavior after both migrations.

#### Manual

- [ ] 5.4 After separately approved deployment, two synthetic accounts prove status/original isolation, a restart recovers jobs/cleanup, and idle-period processing works with Always On.
- [ ] 5.5 Owner reviews OCR quality evidence, retention/cleanup limitations and rollback runbook; original retrieval works with reading disabled.
