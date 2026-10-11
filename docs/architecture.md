# Architecture

```text
Browser
   |
   v
Azure Static Web Apps (Standard)        apps/web      Astro, static output
   |  /api/*  same-origin proxy (linked backend)
   v
Azure App Service (Linux)               services/api  ASP.NET Core 10
   |  Entra tokens of the App Service managed identity
   +------------------------------------+------------------------------------+
   v                                    v                                    v
Azure Database for PostgreSQL           Azure Blob Storage                   Azure Document Intelligence
Flexible Server 17, Entra-only auth     private `originals` container,       S0, `prebuilt-read`, Entra-only
                                        no keys or SAS                       (OCR for the background reader)
```

- The page and `/api/*` share one origin, so there is no CORS configuration. The App Service accepts traffic only through the Static Web App.
- The API never stores a database password or storage key in Azure: it signs in to both with its managed identity. Locally and in tests it uses a password connection string for PostgreSQL and an Azurite connection string for Blob Storage.
- Background reading (text extraction and OCR) runs inside the API process; see [Document reading](#document-reading). Original retrieval does not depend on it, and `/api/health` checks only the database.
- Planned additions: pgvector semantic search over the read text. It does not exist yet.

## Accounts and sessions

Accounts are ASP.NET Core Identity users. Signing in creates one server-side session per device in the `auth_sessions` table (the authentication ticket, encrypted with Data Protection, with a 14-day sliding expiry); the HttpOnly `oz_session` cookie carries only that session's key. `POST /api/auth/logout` deletes the current device's session, so a copy of the old cookie no longer authenticates even if the expiring `Set-Cookie` never reaches the browser; other devices stay signed in. Every `/api/*` response is sent with `Cache-Control: no-store` unless the endpoint sets its own.

## Animals and profiles

An animal has a name (trimmed, 1–100 characters; duplicates allowed), an `isActive` flag and an opaque `version` UUID, and belongs to its creator through an owner membership created in the same transaction. Every owned animal response is `{id, name, isActive, version, storedDocumentCount}`, where the count includes `Stored` documents only and is projected in one owner-scoped query. There is no delete route for animals or documents; an animal is marked inactive instead.

- `GET /api/animals` lists active animals in creation order; `?includeInactive=true` lists both, which Animals uses to show active rows and a collapsed Inactive section. `GET /api/animals/{id}` returns an owned animal in either state.
- `POST /api/animals` creates an active animal. `PUT /api/animals/{id}/name` takes `{name}` and `PUT /api/animals/{id}/activity` takes `{isActive}`; renaming never changes activity and vice versa. All three require an antiforgery token; reads need none.
- Both `PUT` routes require `If-Match` with exactly one strong entity tag: the quoted `version` from the latest response (`If-Match: "<uuid>"`). A missing header answers 428 `version_required`; a malformed header (weak, `*`, unquoted, not a UUID) 400 `invalid_version`; a malformed activity body 400 `invalid_activity`; invalid names 400 `name_required` or `name_too_long`. Ownership is checked first, so another account's or a missing animal answers 404 whatever the version. A version that is no longer current answers 412 `animal_changed` and changes nothing, even when the request would be a no-op. A successful change returns the animal with a new version; a no-op with the current version keeps it. The update is a single conditional write, so of several edits sent with the same version exactly one succeeds.
- The web app never replays a rejected edit: on 412 the profile keeps the typed draft, explains that another tab or device changed the animal, and requires an explicit Refresh, which loads the current name, status and version, before the owner saves again. A lost create response offers a refresh of Animals before another explicit submission; creation is not exactly-once.

Activity limits only new captures. Capture defaults (`GET /api/capture-defaults`) offer active animals only: the account's last-used animal while it is active, otherwise the earliest-created active animal. Creating a fresh upload operation locks the animal row and rechecks activity in the same transaction as the insert, and marking an animal inactive takes the same lock, so whichever commits first decides: an inactive owned animal answers 409 `animal_inactive`, while an operation accepted before the inactivation stays retryable, uploadable and completable. Completion may record an inactive animal as last used; defaults ignore it. History, document detail and original routes, and `/api/me`'s `hasAnimals` flag, ignore activity: an account whose animals are all inactive is not sent back to onboarding, sees create/reactivate guidance on Animals and Add, and keeps every archive. Future processing and search are expected to keep including inactive animals' documents.

The `/animal-documents/?animalId=<id>` page (`/en/animal-documents/` in English) is the animal's profile: name, status, rename and mark inactive/active actions, Add document preselected to the animal while it is active, and the paginated document history. History is ordered by the owner-entered event date, then upload time and ID, newest first; it is a list of stored documents, not a guaranteed medical chronology.

The `AnimalActivity` migration is additive: existing animals became active and each received its own random version. It is forward-only; rolling back the code leaves the columns and values in place, and a release without the activity rule must not take new captures for inactive animals until a forward fix restores it.

## Documents and originals

A document belongs to one animal and holds either one PDF or 1–10 JPEG/PNG images in a confirmed order, each at most 10,485,760 bytes. The same file (same SHA-256) cannot appear twice in one document (400 `duplicate_file`); separate documents may hold identical files. Ownership follows the animal's membership; another account's animal, document or file answers 404, and every route requires a session (401 otherwise).

Capture is a retryable operation with a browser-generated ID, uploaded one file per request to stay within the Static Web Apps proxy limits (30 MB per request, 45 seconds per API call):

1. `PUT /api/document-uploads/{operationId}` stores a frozen manifest (animal, event date, IANA time zone, file names, types, lengths and SHA-256 hashes) as an `Uploading` document. The event date may not be later than today in that time zone.
2. `PUT /api/document-uploads/{operationId}/files/{position}` checks one multipart file against its slot (length, hash and format signature), writes it to Blob Storage under `documents/{documentId}/{fileId}` without ever overwriting, then records the storage receipt in PostgreSQL.
3. `POST /api/document-uploads/{operationId}/complete` recovers any receipt missing from the database from storage, then switches the document to `Stored` and updates the account's last-used animal in one transaction.

All three require an antiforgery token (`GET /api/antiforgery`, sent back in the `X-CSRF-TOKEN` header). Repeating a step with the same content returns the existing result; different content under the same operation ID is a conflict. There is no transaction across PostgreSQL and Blob Storage: a blob written before a database failure is reconciled on retry, and only `Stored` documents appear in lists. Incomplete operations are never deleted automatically.

`GET /api/animals/{animalId}/documents` and `GET /api/documents/{documentId}` read `Stored` documents. `GET /api/documents/{documentId}/files/{fileId}/original` streams the unchanged bytes after a fresh ownership check, with `Cache-Control: private, no-store`, byte ranges and `?download=true` for an attachment; it never redirects to storage. A missing blob returns 503 `original_unavailable` and a storage outage 503 `storage_unavailable`; the document record is kept in both cases.

Capture logs carry operation, document and file IDs, positions, states and failure codes, never file names or contents.

## Document reading

Completing a capture creates a durable reading job in the same transaction (exactly one per document; a repeated completion leaves it unchanged). The migration that introduced reading queued every document that was already `Stored`. Reading status is separate from storage state: an original stays retrievable whatever happens to reading.

A `BackgroundService` in the API (`DocumentReadingWorker`) processes one job at a time. There is no separate queue service or WebJob: jobs live in PostgreSQL.

- **Claims and leases.** A worker claims a due job (`FOR UPDATE SKIP LOCKED`) with a two-minute lease that it renews while working. No database transaction is held during parsing or OCR. Only the holder of the current lease and run generation may write progress or publish, so competing instances and expired workers cannot overwrite each other.
- **Extraction.** A PDF's own text layer is read locally by a child process: the API's own assembly started with a hidden `pdf-text-helper` command, so it ships with the API. The child has a 60-second timeout, a 256 MiB heap limit and no access to the API's credentials. Pages without usable text, and every JPEG/PNG photo, go to Azure Document Intelligence `prebuilt-read` (API `2024-11-30`). The original bytes are sent with the API's managed identity; there is no public URL or SAS.
- **Durable progress.** Each original of a run has a run file that records its stage, the provider operation ID and the checkpointed output. After a restart a saved operation is polled again instead of being submitted twice, and checkpointed output is not fetched twice.
- **Publication.** All originals' text is published at once as one revision in `document_text_pages`, with the source file, its position and the page number, and whether the page came from the text layer or OCR. A document with no text, more than 2,000 PDF pages or more than 10,000,000 characters fails explicitly; nothing is published partially or truncated.
- **Retries.** Transient failures (throttling, provider or storage outages, timeouts) are retried up to five attempts, after 30 seconds, 2, 10 and 30 minutes. Other failures stop the job with a sanitized failure code.
- **Provider cleanup.** After the output is saved, the API asks the provider to delete the analysis result. Failed deletions are retried with backoff after restarts, independently of the job's outcome. An acknowledged deletion is a request, not verified erasure. A submission whose response was lost cannot be deleted early and expires under the provider's retention (up to 24 hours). This is the retention exception recorded in the PRD.
- **F1 hosting.** The API runs on App Service F1, which has no Always On and unloads after idle time. When the app starts or wakes, the worker first retries due cleanup, then picks up due and lease-expired jobs. While the app sleeps, new jobs, retries and cleanup wait until the next request wakes it.

Owner routes (session required, 401 otherwise; another account's, missing and `Uploading` documents answer 404):

- `GET /api/documents/{documentId}/reading` returns `state` (`queued`, `reading`, `read` or `failed`), `failureCode` (only when failed), `canRetry`, `retryAfter` and `completedAt`. The document view polls it every five seconds while the content is queued or being read, for at most ten minutes.
- `POST /api/documents/{documentId}/reading/retry` (antiforgery token required) queues a failed job again in a new run generation. Repeated clicks coalesce, and at most three retries per document per hour are accepted; more answer 429 `retry_limited` with `retryAfter`.

"Read" means the text was extracted and stored, not that the document is searchable. Reading is configured with the `DocumentReading` settings ([local development](local-development.md#configuration)). Misconfiguration turns reading off with a startup warning instead of stopping the API, and `DocumentReading:WorkerEnabled` switches the worker on and off separately. Reading logs carry document, file and run-file IDs, states, failure codes and timings, never extracted text, file names, provider payloads or tokens.

## Repository layout

```text
apps/web/            Astro frontend (strict TypeScript)
services/api/        ASP.NET Core API; EF Core model and migrations in Data/, capture and originals in
                     Documents/, the Blob Storage adapter in Storage/, background reading and its routes
                     in DocumentReading/
services/api.Tests/  xUnit integration tests (WebApplicationFactory + Testcontainers)
infra/               Bicep templates and deploy.sh (lint / what-if / apply)
  bootstrap/         One-time, hand-applied setup: CI identities, resource group, budget, API storage and
                     Document Intelligence access
scripts/smoke.sh     End-to-end, read-only check of a deployed environment
compose.yaml         Local PostgreSQL and Azurite
docs/                Developer documentation (this folder)
context/             Product and planning documents (PRD, stack, roadmap, change plans)
.github/workflows/   ci.yml (pull requests), deploy.yml (main)
```
