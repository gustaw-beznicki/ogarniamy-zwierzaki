# Capture document original — Plan Brief

> Full plan: [plan.md](plan.md)

## What & Why

Deliver roadmap S-02: capture veterinary documents with minimal typing and keep the original as the source of truth. The archive must stay private and reopenable even when future OCR or search fails.

## Starting Point

Accounts, cookie sessions, owner-isolated animals, PostgreSQL, the bilingual shell and Azure CI/CD exist. Add is a placeholder; documents, Blob Storage and capture preferences are absent.

## Desired End State

An owner saves one PDF or several ordered photos with an animal and event date, then opens all originals from a minimal document list reached through Animals. Documents remain available after signing in again, and another account or an anonymous browser cannot open them.

## Key Decisions Made

| Decision | Choice | Why |
| --- | --- | --- |
| Formats | PDF, JPEG, PNG; no HEIC/HEIF conversion | Covers the agreed sources while preserving original bytes. |
| File size | 10 MiB = 10,485,760 bytes per file, inclusive | Owner selected this upload boundary. |
| Document unit | One PDF or 1–10 ordered photos; no mixed mode | Keeps a paper document together without a PDF-merging feature. |
| Event date | Device-local today by default; today or any past date | Fits an archive of completed events; upload time remains separate. |
| Retrieval entry | Minimal document list reached from Animals | Originals need a persistent entry before full profiles arrive in S-06. |
| Last animal | Account preference, updated once per completed capture | Consistent across devices; initially earliest-created owned animal. |
| Interrupted save | Retry safely in the open form with the same operation ID | Avoids duplicates without a draft-management feature. |
| Transport | One file per request, then explicit completion | Up to 100 MiB of photos would exceed the 30 MB SWA request limit. |
| Private originals | Authenticated owner-checked API streams; no public/SAS links | Preserves the PRD requirement that opening requires sign-in. |
| Runtime access | Managed identity, with a separate owner-run Bicep role grant | Current CI Contributor cannot grant Blob data roles. |

## Scope

**In scope:** capture/reorder photos, upload PDF, animal/date selection, private originals, minimal document navigation, safe retry, CSRF protection for new writes, PL/EN, Azurite tests and deployment instructions.

**Out of scope:** OCR/search, full animal profiles/editing, deletion, future event dates, HEIC/HEIF conversion, photo-to-PDF merging, typed titles, cross-submission deduplication, offline/background upload and resuming after the form closes.

## Architecture / Approach

The browser creates an immutable manifest, uploads each original separately and commits the complete document. PostgreSQL tracks the operation and ordered files; private Blob Storage retains unchanged bytes. Hash receipts and deterministic keys reconcile retries after partial failures. Only Stored documents appear in lists; pending uploads remain private without an automatic deletion policy. Reads always check membership through the animal.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Private storage | Blob/Azurite adapter, Bicep resources and access prerequisite | CI cannot assign runtime roles. |
| 2. Document model | Ordered files, immutable manifest and account preference | Partial operations must not appear as stored. |
| 3. Document API | Upload, retry, commit, owner isolation and original streams | Blob and database do not share a transaction. |
| 4. Capture and archive interface | Bilingual form, document list and original view | Camera format and interrupted mobile uploads. |
| 5. Verification and documentation | Failure coverage, real-phone/Azure checks and recovery runbook | Local emulation cannot prove deployed proxy behavior. |

**Prerequisites:** S-01 is complete; Docker is available for PostgreSQL/Azurite tests. Before first production rollout, an owner registers Microsoft.Storage if needed and applies the reviewed storage/access Bicep prerequisite. Existing production approval remains in force.
**Estimated effort:** roughly 20–30 hours across five phases, including failure tests and phone/Azure acceptance; this is a planning estimate, not a measured commitment.

## Open Risks & Assumptions

- SWA limits an API request to 45 seconds; even a valid 10 MiB file may need retry on a slow connection. Verify on the deployed route.
- The browser camera may produce an unsupported format; validate a real phone and explain conversion rather than silently changing originals.
- Pending operations/blobs are retained; cleanup and a draft UI are deferred. No stored original has an expiry rule.
- Blob/container soft delete lasts 30 days and versioning supports recovery, but these do not constitute an independent backup or eliminate outages.
- Azure provider/role state was not queried during planning. Verify it before rollout; do not broaden CI privileges to bypass the prerequisite.

## Success Criteria (Summary)

- PDF and 1–10-photo captures preserve bytes/order, animal and separate event/upload dates, and remain reopenable after sign-in.
- Anonymous/foreign access fails; original retrieval works without OCR/search; multipart mutations require antiforgery validation.
- Retrying partial uploads or a lost completion response produces exactly one stored document, with no partial document visible.
