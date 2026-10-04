# Capture document original Implementation Plan

## Overview

Deliver roadmap S-02: an authenticated owner can photograph or upload veterinary records, assign an animal and event date, and reopen the private originals later. A document contains either one PDF or 1–10 ordered JPEG/PNG images. Each file is at most 10 MiB (10,485,760 bytes), inclusive. Originals remain unchanged and retrievable independently of future OCR and search.

Planning complexity: MEDIUM. The owner answered eight scope questions and approved the five phases below on 2026-10-04. This plan authorizes no implementation, commit, or production deployment by itself.

## Current State Analysis

The repository has deployed Astro/React pages, ASP.NET Core Identity cookie authentication, PostgreSQL/EF Core, owner-isolated animals, and integration tests against PostgreSQL. Add and Search are placeholders. Documents, file storage, upload state and last-capture preferences do not exist.

The API uses an authenticated fallback policy and derives the account from claims. Animal reads start at the current user's memberships; a foreign ID and a missing ID both return 404. Document ownership must follow this relationship rather than a client-supplied owner field. Existing JSON mutations do not provide an antiforgery mechanism; multipart upload needs explicit protection.

Infrastructure uses Static Web Apps Standard as a same-origin API proxy and App Service with a managed identity. The deployment identity has Contributor permissions and cannot create role assignments. Blob resources and Azurite are absent. Production state was not queried during planning; provider registration and runtime storage authorization must be verified during implementation.

## Desired End State

- A signed-in owner chooses one PDF or adds, removes and reorders 1–10 photos before saving. Camera capture and JPEG/PNG gallery selection are available. Unsupported formats explain the supported alternatives.
- Saving requires no typed title or description. The animal selector uses one-tap choices; it defaults to the animal from the account's most recently completed capture. Before any capture, it uses the earliest-created owned animal, with ID as the existing deterministic tie-breaker.
- The event date defaults to today on the user's device. It accepts today and past calendar dates, without an extra historical cutoff. Uploaded time is a separate UTC instant.
- The owner can navigate from Animals to a minimal document list and open a document with all originals in the confirmed order, including after a fresh sign-in.
- Files are uploaded separately and a document becomes visible only after all files and its metadata are durable. Retrying the same operation in the still-open form returns the same document and never duplicates it.
- Original URLs require the owner's current session. Anonymous requests return 401; foreign resources return 404. No public blob URLs or bearer download links are returned.
- PL and EN flows distinguish a stored original from content processing, which is not implemented in this slice.

### Key Discoveries:

- `services/api/Program.cs:62`: fallback authorization already gates new routes; add document services and route mapping alongside existing registrations.
- `services/api/Animals/OwnedAnimals.cs:44`: reuse membership-scoped queries and the existing 404 convention from `AnimalEndpoints.cs:26`.
- `services/api/Data/AppDbContext.cs:13`: add document entities and a forward-compatible migration to the existing context.
- `apps/web/src/lib/api.ts:21`: the transport sets JSON Content-Type for every body and assumes JSON responses; multipart upload and binary navigation require explicit handling.
- `apps/web/src/pages/add.astro:12` and `apps/web/src/pages/en/add.astro:12`: replace protected placeholder content while retaining the shell and session gate.
- `apps/web/src/components/AnimalsList.tsx:21`: add document navigation to existing animal rows.
- `services/api.Tests/ApiFactory.cs:8`: extend real integration fixtures with Azurite and keep production storage code under test.
- `infra/modules/app-service.bicep:36`: API managed identity is available; `infra/bootstrap/app-rg-roles.bicep:10` grants CI only Contributor.
- Microsoft documents a 30 MB SWA request limit and a 45-second API request duration. Sending a 10-photo document in one request could violate the size limit even though each file is valid.

## What We're NOT Doing

- OCR, text extraction, embeddings, processing queues or semantic search (S-03 onward).
- Animal creation beyond existing onboarding, editing, inactive status, sharing, or a full animal profile (S-06). Only the minimal document list needed for retrieval moves into S-02.
- HEIC/HEIF support, image conversion, compression, cropping, merging photographs into a PDF, or changing original bytes.
- Mixing PDF and images in one document, multiple PDFs in one document, or bulk creation of multiple documents from one submission.
- Required titles, document types, automatic date extraction, metadata editing after successful storage, future event dates, or a reliable clinical timeline claim.
- Document/file deletion, draft management screens, background upload, offline capture, or resuming a form after navigation or browser closure.
- Content-based deduplication across separate submissions. The owner may intentionally store identical files in different documents.
- Public originals, SAS download links, a hosting migration, broad CI role-granting permissions, or unrelated account-hardening work.

## Implementation Approach

Use one Blob implementation in Azure, local development and integration tests. Production uses the API managed identity; development and tests use Azurite. Configure a private Standard_LRS StorageV2 account in the backend region, an `originals` container, HTTPS/TLS 1.2, disabled anonymous blob access and disabled production Shared Key access. Enable blob and container soft delete for 30 days and blob versioning. Do not introduce storage lifecycle rules that expire originals. This provides recovery features, not a promise of continuous availability or unlimited retention of recovery versions.

A capture is a durable operation with a client-generated UUID held in the open form. Before uploading, the browser computes each file's SHA-256 and creates an immutable manifest containing animal, event date, browser IANA time zone, ordered file names, media types, lengths and hashes. The server validates and stores this manifest as an Uploading document; it then accepts one multipart file per slot. Only a final commit switches the document to Stored and updates the account preference in the same PostgreSQL transaction. Lists expose Stored documents only. Originals are never overwritten.

There is no distributed transaction between PostgreSQL and Blob Storage. Durable manifest rows precede blob writes; deterministic blob keys and hash receipts make a blob written before a database failure recoverable on retry. There is no automated deletion of incomplete uploads in this slice. They remain private, invisible to ordinary document lists, and measurable in logs; a future cleanup policy must explicitly distinguish them from stored originals.

Read original bytes through the authenticated API after a fresh membership check. The frontend uses ordinary same-origin links rather than loading an entire PDF into JavaScript. The detail view previews image pages in order, opens the PDF in the browser where supported, and offers authenticated download links as a fallback.

## Critical Implementation Details

### State sequencing

Freeze a manifest once created. Changing the animal, date, file order or bytes after upload begins requires a new operation; retrying an unchanged manifest reuses its ID. A lost response after final commit must be recoverable without changing UploadedAt or the last-used preference again. Concurrent completion must serialize the preference update on the account row so the last completed transaction determines the default.

### Infrastructure authorization

Normal CI cannot grant Blob roles. Provide a separate owner-run Bicep entry point that provisions the same shared storage module and grants the existing API identity container-scoped Storage Blob Data Contributor. Run it before the first production release that requires storage; ordinary CI subsequently maintains storage settings without managing this role assignment. Keep PR permissions and the main deploy identity unchanged.

### Date and transport boundaries

Validate the browser's named time zone on the server and compute today from the server clock in that zone. A named zone describes user context; it is not an authorization claim. Compare event dates when creating the operation, not again against a changing clock during retries. Accept ISO dates from 0001-01-01 through the computed local today; reject malformed dates and unsupported zones explicitly. Upload one file per request, cap request bodies at 11 MiB to allow multipart overhead, and keep SDK retries bounded within the proxy's time budget.

## Phase 1: Private storage

### Overview

Introduce storage configuration and a private Blob adapter, with local/test emulation and an explicit production access prerequisite.

### Changes Required:

#### 1. Azure resources and authorization

**Files**: `infra/modules/storage.bicep` (new), `infra/main.bicep`, `infra/modules/app-service.bicep`, `infra/bootstrap/storage-access.bicep` (new), `infra/environments/mvp.bicepparam` if needed.

**Intent**: Keep originals private and durable while retaining infrastructure ownership in Bicep. Make the one-time runtime access grant executable without expanding CI privileges.

**Contract**: The shared storage module creates the account, private container and recovery settings described above. Compute storage naming without a dependency on App Service outputs; pass the endpoint/container into App Service settings. Main deployment creates no role assignments. The owner entry point takes the existing API principal ID, uses the shared module and creates only the scoped Blob role. Document Microsoft.Storage registration as an owner prerequisite. Neither template contains credentials.

#### 2. Storage adapter and local emulator

**Files**: `services/api/Storage/StorageOptions.cs`, `StorageAuthMode.cs`, `OriginalFileReceipt.cs`, `IOriginalStorage.cs`, `BlobOriginalStorage.cs`, `StorageSetup.cs` (all new), `services/api/Program.cs`, `services/api/appsettings.json`, `Directory.Packages.props`, `services/api/ogarniamy-zwierzaki-api.csproj`, `compose.yaml`, `docs/local-development.md`.

**Intent**: Exercise the same storage path in Azure and locally; keep secrets in the existing convention.

**Contract**: Adapter supports create-if-absent upload, receipt lookup and streaming original reads with cancellation. Receipts contain length, SHA-256 and ETag; existing blobs are never overwritten. Azure uses ManagedIdentityCredential, not storage keys. Local connection strings live in user secrets or the gitignored environment, including emulator configuration. Pin a current compatible Azure.Storage.Blobs version in central package management and pin the Azurite image; verify these against official release documentation during implementation. Production never silently falls back to Azurite. Provision containers through Bicep in Azure and explicitly during local/test setup.

#### 3. Storage test foundation

**Files**: `services/api.Tests/ApiFactory.cs`, `services/api.Tests/OriginalStorageTests.cs` (new), `services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`, `Directory.Packages.props`.

**Intent**: Verify the adapter in this phase rather than deferring its first checks to Phase 5.

**Contract**: Add a pinned Azurite test container alongside PostgreSQL, isolate fixture storage, and exercise create/receipt/read/overwrite/anonymous-access behavior. Any new direct Testcontainers package reference gets its version only in Directory.Packages.props. Later phases extend this foundation.

### Success Criteria:

#### Automated Verification:

- API restore and build pass: `dotnet restore services/api/ogarniamy-zwierzaki-api.csproj` and `dotnet build services/api/ogarniamy-zwierzaki-api.csproj --no-restore`.
- Bicep lint passes and read-only what-if shows the expected storage/configuration changes: `infra/deploy.sh lint` and `infra/deploy.sh what-if`; validate the owner entry point separately without applying it.
- Storage integration checks against Azurite preserve bytes, reject overwrite, support receipt lookup and reject anonymous reads.

#### Manual Verification:

- Owner reviews the storage diff and verifies the provider registration and scoped API role prerequisite; production apply remains a separate approved action.

**Implementation Note**: Record checks in Progress. If manual confirmation is still pending, leave it unchecked and report it before proceeding under the implementation workflow.

## Phase 2: Document model

### Overview

Persist immutable capture manifests, ordered original files and the account's last completed animal preference.

### Changes Required:

#### 1. Entities and migration

**Files**: `services/api/Documents/Document.cs`, `DocumentFile.cs`, `DocumentStorageState.cs` (new), `services/api/Auth/AppUser.cs`, `services/api/Data/AppDbContext.cs`, `services/api/Data/Migrations/<timestamp>_DocumentOriginals.cs` and generated companions.

**Intent**: Represent one logical document with ordered originals without assigning direct user ownership to the document.

**Contract**: Document fields include UUID ID/operation ID, AnimalId, DateOnly EventDate, capture time-zone identifier, storage state Uploading/Stored, CreatedAt and nullable UploadedAt. DocumentFile has its own UUID, DocumentId, zero-based Position, sanitized OriginalName, validated ContentType, ByteLength, SHA-256, immutable BlobKey and nullable storage receipt. Position and BlobKey are unique in their applicable scopes; lengths are 1–10,485,760 bytes and hashes are 64 hex characters. A document has one PDF or 1–10 JPEG/PNG files, enforced by service validation and applicable database constraints. Restrict document/animal deletion relationships. AppUser gains nullable LastCaptureAnimalId; existing accounts and animals remain valid. One type per file; generated migrations are the repository's explicit exception.

#### 2. Owner-scoped repository and capture defaults

**Files**: `services/api/Documents/OwnedDocuments.cs`, `DocumentSummary.cs`, `DocumentDetails.cs`, `DocumentFileDetails.cs`, `CaptureDefaults.cs` (new), `services/api/Program.cs`.

**Intent**: Centralize membership checks for every document operation and make the default consistent across devices.

**Contract**: Queries begin at the current user's owner memberships. Uploading documents are available only through the upload-operation contract, not lists or original-read routes. Stored documents list by EventDate descending, UploadedAt descending, then ID descending, with offset/limit pagination (default 50, maximum 100). Defaults validate the saved animal is still eligible, otherwise use the existing earliest-created/ID ordering; no animals yields a null default and onboarding behavior. Completion updates Stored/UploadedAt and LastCaptureAnimalId atomically; a repeated completion has no additional preference side effect.

#### 3. Model and repository checks

**Files**: `services/api.Tests/DocumentModelTests.cs`, `DocumentRepositoryTests.cs` (new).

**Intent**: Prove relational constraints and owner scoping before exposing document routes.

**Contract**: Use the real PostgreSQL fixture to check additive migration, pending exclusion, ordering, foreign membership denial and default-preference transitions. These tests are introduced in this phase.

### Success Criteria:

#### Automated Verification:

- API build and integration suite pass: `dotnet build services/api/ogarniamy-zwierzaki-api.csproj` and `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`.
- New and existing databases migrate without losing accounts, animals or session keys; relational constraints preserve file order, blob-key uniqueness and valid references.
- Repository tests prove owner isolation, pending-document exclusion, deterministic pagination and account-scoped capture defaults.

## Phase 3: Document API

### Overview

Implement bounded file upload, recovery from ambiguous outcomes, final commit and private retrieval.

### Changes Required:

#### 1. Capture service and validation

**Files**: `services/api/Documents/DocumentCaptureService.cs`, `DocumentFileValidator.cs`, `CreateDocumentUploadRequest.cs`, `DocumentUploadFileRequest.cs`, `DocumentUploadResponse.cs`, `DocumentUploadFileResponse.cs` (new), and narrowly scoped DTO/result types as required, each in its own file.

**Intent**: Ensure a success means the complete original set can be retrieved, including after failure between Blob and PostgreSQL writes.

**Contract**: Manifest creation checks owned animal, valid local event date, supported time zone, mode/count, names (sanitized, maximum 255 characters), positive lengths and hashes. Repeated operation ID with an identical manifest returns the same operation; a changed manifest returns 409. Foreign IDs return 404. Each file upload checks actual bytes, length and SHA-256 against the manifest and validates PDF/JPEG/PNG signatures, rather than trusting extension or submitted Content-Type. Empty or mismatched files are rejected. This is format screening, not a promise to detect every malformed or hostile PDF; no document parser or malware service is introduced here.

**Contract**: Use deterministic opaque blob keys based on document/file IDs, never user filenames. An existing matching blob receipt repairs a missing database receipt on retry; a different receipt is a conflict and never an overwrite. If cancellation or a database failure follows a blob write, the next retry reconciles it. Commit verifies every manifest slot has a matching durable storage receipt and atomically transitions once. An incomplete commit returns 409, never a partial Stored document. Repeated successful create/file/commit calls remain safe, including concurrent retries. Completed documents reject changed file bodies. Retain pending operations and blobs; there is no expiry timer or cleanup job in this slice.

#### 2. Routes, antiforgery and errors

**Files**: `services/api/Documents/DocumentEndpoints.cs`, `services/api/Auth/AntiforgeryEndpoints.cs` (new), `services/api/Program.cs`, `services/api/ApiProblem.cs` only if needed.

**Intent**: Expose explicit upload boundaries and protect cookie-authenticated writes.

**Contract**:

- `GET /api/capture-defaults` returns eligible owned animals and defaultAnimalId.
- `GET /api/antiforgery` returns a request token and sets the associated secure/HttpOnly antiforgery cookie; response is not cached.
- `PUT /api/document-uploads/{operationId:guid}` accepts the immutable JSON manifest; returns 201 on first creation, 200 on an identical retry.
- `PUT /api/document-uploads/{operationId:guid}/files/{position:int}` accepts exactly one multipart file, at most 10 MiB; returns its durable receipt after successful reconciliation.
- `POST /api/document-uploads/{operationId:guid}/complete` returns the same Stored document representation on first completion and retries.
- `GET /api/animals/{animalId:guid}/documents?offset=0&limit=50` lists only owned Stored documents, with hasMore for navigation.
- `GET /api/documents/{documentId:guid}` returns animal, EventDate, UploadedAt and ordered file links for a Stored document.
- `GET /api/documents/{documentId:guid}/files/{fileId:guid}/original` streams an owned Stored original; `?download=true` supplies attachment disposition. No route exposes blob endpoints or keys.

**Contract**: Require valid antiforgery tokens in an `X-CSRF-TOKEN` header on all new capture mutations, including JSON create/complete. Configure middleware after authentication/authorization, and ensure multipart validation is not disabled to bypass framework requirements. Anonymous reads/writes return 401, authenticated foreign IDs 404, validation 400, conflicts 409, unsupported media 415, oversize 413, and transient storage failure 503 with a retryable machine code. Use existing ProblemDetails `code` conventions, including `invalid_event_date`, `future_event_date`, `invalid_time_zone`, `unsupported_file_type`, `file_too_large`, `file_mismatch`, `upload_conflict`, `upload_incomplete`, `invalid_antiforgery_token` and `storage_unavailable`. Proxy-generated errors may lack JSON; frontend needs status-based fallback.

**Contract**: Original responses use validated Content-Type, safe Content-Disposition, `Cache-Control: private, no-store` and `X-Content-Type-Options: nosniff`. Do not cache private original responses or return redirects to storage. Stream with response-lifetime disposal and cancellation; support HTTP range handling where the storage stream permits it, without unbounded buffering. A missing original for a Stored row returns an explicit unavailable error and logs a document/file correlation ID; do not remove the record or fall back to derived content.

#### 3. API and recovery checks

**Files**: `services/api.Tests/DocumentCaptureTests.cs`, `DocumentIsolationTests.cs`, `DocumentRecoveryTests.cs` (new), `services/api.Tests/TestAccounts.cs` and narrowly scoped test failure helpers.

**Intent**: Verify the new endpoint guarantees as they are introduced.

**Contract**: Acquire actual antiforgery tokens with session cookies, use real Blob/PostgreSQL storage for successful paths and inject deterministic failures at persistence boundaries. Implement this phase's boundary, authorization, recovery and original-byte checks now; Phase 5 closes remaining regression and deployed acceptance coverage.

### Success Criteria:

#### Automated Verification:

- API build and integration suite pass with PostgreSQL and Azurite: `dotnet build services/api/ogarniamy-zwierzaki-api.csproj` and `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`.
- Upload tests prove 10 MiB inclusive, oversize rejection, one PDF or 1–10 images, signature/hash validation and today/past-date acceptance with future-date rejection in the supplied valid time zone.
- Authorization tests deny anonymous and foreign upload/list/detail/original requests and reject missing or invalid antiforgery tokens before mutation.
- Failure tests prove no partial visibility and one document after repeated/concurrent create, upload and commit, including blob-success/database-failure and lost completion response.
- Original retrieval tests prove byte-for-byte equality and order, correct private headers, and retrieval without any OCR/search services.

## Phase 4: Capture and archive interface

### Overview

Deliver the complete mobile and desktop flow in both locales using the existing authenticated shell.

### Changes Required:

#### 1. API transport and form

**Files**: `apps/web/src/lib/api.ts`, `apps/web/src/components/DocumentCaptureForm.tsx` (new), `apps/web/src/pages/add.astro`, `apps/web/src/pages/en/add.astro`, `apps/web/src/styles/tokens.css`.

**Intent**: Make capture possible without typing and preserve the user's work during retry in the open form.

**Contract**: Request helpers distinguish JSON and FormData; browser generates multipart boundaries. Fetch the antiforgery token for capture mutations and refresh after a token/session change, without blindly replaying non-idempotent legacy calls. Preserve HTTP status in ApiError for non-JSON proxy responses. Keep originals as ordinary same-origin links.

**Contract**: Provide Take photo, Add photos and Choose PDF controls. Images may be added one at a time or selected together, with preview, remove and accessible move-up/move-down controls before upload. A PDF is exclusive of image selection; replacing an existing selection requires an explicit UI action. Enforce 1–10 photos and 1–10,485,760 bytes per file locally and again on the API. Camera format is checked after selection; HEIC/HEIF errors explain conversion to JPEG/PNG or PDF without silently altering bytes. Display current animal and date visibly, with one-tap animal chips and device-local today; date input prevents future dates.

**Contract**: On Save, freeze controls, compute file hashes, create the operation, upload slots sequentially, then complete. Show visible phase/file progress and a retry action that retains files, manifest and operation ID in memory. No success message appears before server completion. A session-expiry message explains reauthentication and file reselection; do not transfer pending files between accounts. On success navigate to the document detail page. Revoking object URLs and disposal on unmount must not discard state while retrying in the mounted form. After navigation/closure a new submission gets a new operation ID; previously completed documents remain discoverable on the list.

#### 2. Document navigation and originals

**Files**: `apps/web/src/components/AnimalsList.tsx`, `AnimalDocuments.tsx`, `DocumentDetails.tsx` (new components), `apps/web/src/pages/animal-documents.astro`, `document.astro`, `apps/web/src/pages/en/animal-documents.astro`, `document.astro` (new static pages), `apps/web/src/i18n/en.ts`, `apps/web/src/i18n/pl.ts`.

**Intent**: Provide durable access to originals before the broader animal-profile slice exists.

**Contract**: Use static routes `/animal-documents/?animalId=<uuid>` and `/document/?id=<uuid>` with locale equivalents under `/en/`; resolve data client-side through authenticated owner-scoped API calls. Animal rows link to their document list; show explicit empty/error/loading states and pagination. Summaries use the event date and file count, requiring no title. Details label Event date and Uploaded on separately, render image files in confirmed order, and offer PDF open/download and per-image original download. Switching locale preserves the selected resource query. Unknown/foreign IDs show the same unavailable view. Never display Reading/Content read or imply searchability; describe only successful original storage. Use translated messages, accessible labels and existing shell/focus styling.

### Success Criteria:

#### Automated Verification:

- Frontend checks pass: `npm ci --prefix apps/web`, `npm run check --prefix apps/web` and `npm run build --prefix apps/web`.
- Existing API integration checks remain green after transport/antiforgery integration: `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`.

#### Manual Verification:

- PL and EN desktop/phone flows capture a PDF and a reordered multi-photo document without typed titles, show progress/errors, and open every original after a fresh sign-in.
- Switching accounts/devices preserves only the correct account's last completed animal; changing locale preserves the document being viewed.
- Interrupted uploads retry in the open form without duplicates; future dates, HEIC/HEIF, an eleventh image and an oversized file produce understandable messages.

**Implementation Note**: Report manual checks separately and leave them pending until the owner confirms them. Do not mark phone-camera behavior verified from desktop emulation alone.

## Phase 5: Verification and documentation

### Overview

Close failure/security coverage and document the operational steps needed to keep originals retrievable.

### Changes Required:

#### 1. Integration fixtures and regression coverage

**Files**: `services/api.Tests/ApiFactory.cs`, `DocumentCaptureTests.cs`, `DocumentIsolationTests.cs`, `DocumentRecoveryTests.cs`, `OriginalStorageTests.cs`, test helpers (new as needed), `services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`, `Directory.Packages.props`, `.github/workflows/ci.yml`, `.github/workflows/deploy.yml` only if required.

**Intent**: Prove complete original persistence through the real API/database/storage path and make failures repeatable.

**Contract**: Extend the fixtures and tests introduced in Phases 1–3; do not defer earlier phase checks here. Use pinned disposable PostgreSQL and Azurite containers with isolated data, normal migrations, the production Blob adapter and real auth/antiforgery cookies. Close any missing failure-injection coverage around storage/database boundaries. Use small valid non-sensitive PDF/JPEG/PNG fixtures plus generated size-boundary bodies. Run these tests in current CI jobs; never use production credentials or owner veterinary records as fixtures. No new frontend testing framework is required by this plan.

#### 2. Operations and current product documentation

**Files**: `README.md`, `docs/architecture.md`, `docs/local-development.md`, `docs/testing.md`, `docs/deployment.md`, `scripts/smoke.sh`, `context/changes/capture-document-original/notes/storage-runbook.md` (new during implementation).

**Intent**: Explain what is implemented and how to verify/recover it, without presenting future OCR as available.

**Contract**: Update supported formats/limits and local Azurite setup; describe storage configuration and the separate owner-run Bicep access prerequisite. Add anonymous document/original route rejection to the non-mutating smoke test. Put one-time owner commands and rollout evidence in the change runbook. Document recovery from missing receipts, stored-blob unavailability and soft-deleted originals, plus code rollback preserving schema and blobs. Logs record operation/document/file IDs, state and machine error codes; omit raw contents, credentials and original filenames. Include a read-only query/runbook step to count Uploading operations and inspect storage failures. Do not change health semantics to make OCR/search a condition of original retrieval.

### Success Criteria:

#### Automated Verification:

- Full documented checks pass: `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`, `dotnet build services/api/ogarniamy-zwierzaki-api.csproj`, `npm run check --prefix apps/web`, `npm run build --prefix apps/web` and `infra/deploy.sh lint`.
- Read-only what-if remains consistent with the reviewed storage scope; expanded smoke checks pass after a separately approved deployment.

#### Manual Verification:

- On the deployed SWA origin, two synthetic accounts prove upload/retrieval isolation, anonymous pasted-link denial and original retrieval after API restart; verify storage access settings and recovery features in Azure.
- A real phone confirms camera-produced JPEG/PNG behavior, ordered capture and retry on a slow/interrupted connection; documents that cannot finish inside the proxy window show a retryable error.
- Owner reviews the original-recovery and rollback runbook; completed originals survive code rollback and future OCR/search unavailability is not a retrieval dependency.

**Implementation Note**: Production deployment and recovery mutations follow existing approvals; do not claim their outcomes from local Azurite results. Course commits require the module/lesson identifier supplied for that implementation task.

## Testing Strategy

### Unit Tests:

- Only focused tests where useful: manifest equivalence, file signatures, inclusive size boundaries, mode/count rules and local-date validation around midnight. Prefer integration tests for persisted state and permissions.

### Integration Tests:

- Owner A uploads a PDF and ten ordered images; Owner B cannot attach files, complete operations, list records or open originals belonging to A.
- Sizes 10,485,760 and 10,485,761 bytes produce acceptance and rejection respectively when format-valid; zero bytes are rejected.
- One/ten images are valid, eleven images and mixed PDF/images are invalid; a PDF may contain multiple internal pages without an image-count limit.
- Account preference starts at earliest-created animal, changes only on successful first completion, survives another device and is not changed by a late repeated completion.
- Today and historical dates are accepted, tomorrow rejected; use at least two zones on opposite sides of UTC midnight. Invalid dates/zones fail explicitly.
- Same-operation retries with identical content return the existing resource; changed manifests/content conflict. Test concurrent uploads and completion, a blob written before DB failure, and a completion response lost after commit.
- Every new mutation rejects missing/wrong CSRF tokens. Stored originals are byte-identical, private and readable without a content-processing service.
- Pending operations never appear in document lists; pagination and original sequence are stable. Storage outage or missing blob returns an honest retryable failure without erasing the record.

### Manual Testing Steps:

1. Register synthetic accounts A and B, create owned animals, and upload one PDF as A with an old event date.
2. Capture several JPEG/PNG photos on a real phone, reorder/remove pages, save, and compare every original with the selected files.
3. Navigate away, sign out/in, enter through Animals and reopen the document. Check separate event/upload date labels in PL/EN.
4. Open the detail/original URL without a session and as B; confirm denial without content leakage. Verify Blob anonymous access also fails.
5. Interrupt the network after some files and after completion but before its response; retry in the same form and check only one Stored document exists.
6. Check the ten-image and 10 MiB boundaries, malformed formats and a future event date. Change devices and confirm the account's last completed animal.
7. After approved Azure rollout, repeat the original flow through SWA, restart API and review recovery configuration and runbook.

## Performance Considerations

Ten maximum-sized photos form a 100 MiB logical document, but every request carries only one file. Hashing and upload are sequential; expose progress and avoid retaining extra copies of all files. A 10 MiB file may still exceed SWA's 45-second API window on a slow connection. Use bounded timeouts/retries and honest errors, not claims of guaranteed mobile upload latency. Test this on the deployed route before marking phone acceptance complete.

Lists use server pagination and ownership indexes. Stream originals without copying whole documents into API or browser memory. Range-enabled PDFs improve reading where feasible; authenticated download remains the fallback.

## Migration Notes

Schema changes are additive and compatible with the existing app release. Existing users receive a null preference; no documents need backfill. Deploy storage/access prerequisites first, then schema/API and UI through the current gated workflow. Startup migration behavior remains as documented.

Rollback restores code, not data. Retain the added tables, storage account, access grant and all original blobs. An older release may lack a UI to open new documents; preserve bytes and metadata for the corrected release rather than deleting them. The 30-day recovery window is finite and is not an independent archive backup. Azure role propagation and provider registration can delay the first release; verify them explicitly, not by widening CI permissions.

## References

- Requirements: `context/foundation/prd.md` (US-01, FR-002, FR-006–008, original guardrails and Access Control).
- Delivery boundaries: `context/foundation/roadmap.md` (S-02, S-03, S-06); `context/foundation/tech-stack.md`; `context/foundation/lessons.md`.
- UI reference: `context/foundation/ui-mockup/README.md` (E03/E04; starting point, not implemented behavior).
- Existing patterns: `services/api/Animals/OwnedAnimals.cs:44`, `services/api/Animals/AnimalEndpoints.cs:26`, `services/api.Tests/ApiFactory.cs:8`, `apps/web/src/lib/api.ts:21`.
- [SWA quotas](https://learn.microsoft.com/en-us/azure/static-web-apps/quotas) and [API constraints](https://learn.microsoft.com/en-us/azure/static-web-apps/apis-overview): 30 MB requests and 45-second API duration.
- [Minimal API parameter binding](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/parameter-binding?view=aspnetcore-10.0) and [file uploads](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-10.0): multipart and antiforgery behavior.
- [Blob upload](https://learn.microsoft.com/en-us/azure/storage/blobs/storage-blob-upload), [download](https://learn.microsoft.com/en-us/azure/storage/blobs/storage-blob-download) and [security recommendations](https://learn.microsoft.com/en-us/azure/storage/blobs/security-recommendations).
- [Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite) and [Azure privileged roles](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/privileged).

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: Private storage

#### Automated

- [x] 1.1 API restore and build pass: `dotnet restore services/api/ogarniamy-zwierzaki-api.csproj` and `dotnet build services/api/ogarniamy-zwierzaki-api.csproj --no-restore`. — a744454
- [x] 1.2 Bicep lint passes and read-only what-if shows the expected storage/configuration changes: `infra/deploy.sh lint` and `infra/deploy.sh what-if`; validate the owner entry point separately without applying it. — a744454
- [x] 1.3 Storage integration checks against Azurite preserve bytes, reject overwrite, support receipt lookup and reject anonymous reads. — a744454

#### Manual

- [x] 1.4 Owner reviews the storage diff and verifies the provider registration and scoped API role prerequisite; production apply remains a separate approved action. — a744454

### Phase 2: Document model

#### Automated

- [x] 2.1 API build and integration suite pass: `dotnet build services/api/ogarniamy-zwierzaki-api.csproj` and `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`. — 2c0c9d5
- [x] 2.2 New and existing databases migrate without losing accounts, animals or session keys; relational constraints preserve file order, blob-key uniqueness and valid references. — 2c0c9d5
- [x] 2.3 Repository tests prove owner isolation, pending-document exclusion, deterministic pagination and account-scoped capture defaults. — 2c0c9d5

### Phase 3: Document API

#### Automated

- [x] 3.1 API build and integration suite pass with PostgreSQL and Azurite: `dotnet build services/api/ogarniamy-zwierzaki-api.csproj` and `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`. — 2465785
- [x] 3.2 Upload tests prove 10 MiB inclusive, oversize rejection, one PDF or 1–10 images, signature/hash validation and today/past-date acceptance with future-date rejection in the supplied valid time zone. — 2465785
- [x] 3.3 Authorization tests deny anonymous and foreign upload/list/detail/original requests and reject missing or invalid antiforgery tokens before mutation. — 2465785
- [x] 3.4 Failure tests prove no partial visibility and one document after repeated/concurrent create, upload and commit, including blob-success/database-failure and lost completion response. — 2465785
- [x] 3.5 Original retrieval tests prove byte-for-byte equality and order, correct private headers, and retrieval without any OCR/search services. — 2465785

### Phase 4: Capture and archive interface

#### Automated

- [x] 4.1 Frontend checks pass: `npm ci --prefix apps/web`, `npm run check --prefix apps/web` and `npm run build --prefix apps/web`. — da732f8
- [x] 4.2 Existing API integration checks remain green after transport/antiforgery integration: `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`. — da732f8

#### Manual

- [x] 4.3 PL and EN desktop/phone flows capture a PDF and a reordered multi-photo document without typed titles, show progress/errors, and open every original after a fresh sign-in.
- [x] 4.4 Switching accounts/devices preserves only the correct account's last completed animal; changing locale preserves the document being viewed.
- [x] 4.5 Interrupted uploads retry in the open form without duplicates; future dates, HEIC/HEIF, an eleventh image and an oversized file produce understandable messages.

### Phase 5: Verification and documentation

#### Automated

- [x] 5.1 Full documented checks pass: `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`, `dotnet build services/api/ogarniamy-zwierzaki-api.csproj`, `npm run check --prefix apps/web`, `npm run build --prefix apps/web` and `infra/deploy.sh lint`. — 1253fe2
- [x] 5.2 Read-only what-if remains consistent with the reviewed storage scope; expanded smoke checks pass after a separately approved deployment.

#### Manual

- [x] 5.3 On the deployed SWA origin, two synthetic accounts prove upload/retrieval isolation, anonymous pasted-link denial and original retrieval after API restart; verify storage access settings and recovery features in Azure.
- [x] 5.4 A real phone confirms camera-produced JPEG/PNG behavior, ordered capture and retry on a slow/interrupted connection; documents that cannot finish inside the proxy window show a retryable error.
- [x] 5.5 Owner reviews the original-recovery and rollback runbook; completed originals survive code rollback and future OCR/search unavailability is not a retrieval dependency.
