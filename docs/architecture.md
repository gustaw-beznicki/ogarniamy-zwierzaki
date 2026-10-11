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
   +------------------------------------+
   v                                    v
Azure Database for PostgreSQL           Azure Blob Storage
Flexible Server 17, Entra-only auth     private `originals` container, no keys or SAS
```

- The page and `/api/*` share one origin, so there is no CORS configuration. The App Service accepts traffic only through the Static Web App.
- The API never stores a database password or storage key in Azure: it signs in to both with its managed identity. Locally and in tests it uses a password connection string for PostgreSQL and an Azurite connection string for Blob Storage.
- Planned additions: OCR, pgvector search, and background indexing. Original retrieval will not depend on them, and `/api/health` checks only the database.

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

## Repository layout

```text
apps/web/            Astro frontend (strict TypeScript)
services/api/        ASP.NET Core API; EF Core model and migrations in Data/, capture and originals in
                     Documents/, the Blob Storage adapter in Storage/
services/api.Tests/  xUnit integration tests (WebApplicationFactory + Testcontainers)
infra/               Bicep templates and deploy.sh (lint / what-if / apply)
  bootstrap/         One-time, hand-applied setup: CI identities, resource group, budget, API storage access
scripts/smoke.sh     End-to-end, read-only check of a deployed environment
compose.yaml         Local PostgreSQL and Azurite
docs/                Developer documentation (this folder)
context/             Product and planning documents (PRD, stack, roadmap, change plans)
.github/workflows/   ci.yml (pull requests), deploy.yml (main)
```
