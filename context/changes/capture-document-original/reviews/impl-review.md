<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Capture document original

- **Plan**: context/changes/capture-document-original/plan.md
- **Scope**: Full plan
- **Reviewed phases**: 1, 2, 3, 4, 5
- **Date**: 2026-10-04
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 4 warnings, 6 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | WARNING |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | WARNING |
| Success Criteria | WARNING |

Range reviewed: `a744454^..dcb2035`, plan commits only. The unrelated PRs in the same range are out of scope: logout sessions, language switch, error colour, deploy approval and sign-in form. Every planned file and contract item in Phases 1–5 is MATCH. The Phase 4 review fixes F1–F8 are present in the code. Automated checks pass: API build has 0 warnings, `dotnet test` passes 97/97, `astro check` reports 0 errors, the web build produces 16 pages, and `infra/deploy.sh lint` exits 0. Owner isolation was traced on all document routes: each one starts from `OwnedDocuments.OwnerMemberships` or `OwnedAnimals`, and foreign IDs return 404.

## Findings

### F1 — Upload body is buffered before the operation/ownership check

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: services/api/Documents/DocumentEndpoints.cs:69, services/api/Documents/SingleFileMultipartReader.cs:55-56
- **Detail**: `UploadFileAsync` reads the whole multipart file into a `MemoryStream` and only then calls `capture.UploadFileAsync`, which checks ownership and the slot. The buffer is pre-sized from the client's `Content-Length`, up to 10 MiB on the large-object heap. Any signed-in user with a CSRF token can send concurrent `PUT /api/document-uploads/<random-guid>/files/0`. Each request holds about 10 MiB until it gets a 404. Nothing limits concurrency, and the API runs on the F1 plan, which has about 1 GB of RAM.
- **Fix**: Resolve the owned Uploading operation and its slot before reading the body. Return 404 or 409 early, and size the buffer from the slot's `ByteLength`.
  - Strength: Unauthenticated-by-ownership requests no longer allocate memory. The manifest length is a trusted upper bound.
  - Tradeoff: Adds one DB lookup before the existing one, or a split of `UploadFileAsync` into "resolve slot" and "store". A per-user concurrency limiter (`AddRateLimiter`) would be a separate, optional step.
  - Confidence: HIGH — ordering verified in code.
  - Blind spot: Concurrent legitimate uploads by one owner still hold ≤10 MiB each; not load-tested.
- **Decision**: FIXED (service resolves the owned slot before the endpoint reads the body; buffer sized from the manifest ByteLength)

### F2 — Permanent Blob authorization failure is reported as retryable

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: services/api/Documents/TransientFailures.cs:18
- **Detail**: Every `RequestFailedException` is transient, including 403 `AuthorizationPermissionMismatch`. If the owner-run role grant from `storage-access.bicep` is missing or revoked, every upload and original read returns 503 `storage_unavailable`. The UI then offers Retry forever, and the log entry is only a Warning. `/api/health` checks only the database, so this misconfiguration is invisible.
- **Fix**: Treat `RequestFailedException` with status 401/403 (and other 4xx except 408/429) as non-transient. Log them at Error and return a non-retryable problem code.
- **Decision**: FIXED (only Blob status 0/408/429/5xx are transient; other 4xx become a 500 logged at Error)

### F3 — Deployed manual checks ticked without recorded evidence

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Success Criteria
- **Location**: context/changes/capture-document-original/notes/storage-runbook.md:3, 32-45
- **Detail**: Progress items 4.3–4.5 and 5.2–5.5 are `[x]`. Commit dcb2035 says the owner confirmed them on the deployed site. Phase 5, however, requires rollout evidence in the change runbook. Its status line still says "nothing in this runbook has been run against Azure", and the evidence table is empty. This covers the storage settings, the role scope, the smoke test with check 2c, two-account isolation, the restart, and the phone capture.
- **Fix**: Fill the runbook evidence table (date, result, who) and update the status line from the owner's deployed test session.
- **Decision**: FIXED (runbook status line updated; evidence table filled with the deploy run link and owner confirmations dated 2026-10-04)

### F4 — Unplanned `duplicate_file` rule not reflected in plan or docs

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Scope Discipline
- **Location**: services/api/Documents/DocumentCaptureService.cs:298-302, services/api/Documents/DocumentEndpoints.cs:159, apps/web/src/components/DocumentCaptureForm.tsx:184-201
- **Detail**: Commit e3b315b rejects a manifest that repeats a SHA-256 (400 `duplicate_file`), and the form skips photos it already has (`duplicate_photo`). This does not contradict "What We're NOT Doing", which excludes dedup only *across* submissions. But it is a new API contract rule that is missing from the plan's Phase 3 error-code list, `docs/architecture.md` and the capture rules in `docs/testing.md`.
- **Fix**: Add a short plan addendum and list `duplicate_file` alongside the other capture error codes in docs/architecture.md and docs/testing.md.
- **Decision**: FIXED (plan Addenda section; rule added to docs/architecture.md and docs/testing.md)

### F5 — No resource lock on the storage account

- **Severity**: 💬 OBSERVATION
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: infra/modules/storage.bicep, infra/bootstrap/storage-access.bicep
- **Detail**: Blob versioning and 30-day soft delete protect individual blobs, not the account itself. Deleting the account or the resource group would lose every original, which breaks the "stored original must remain retrievable" invariant. CI's Contributor role cannot create `Microsoft.Authorization/locks`, so a lock cannot go in `main.bicep`.
- **Fix**: Add a `CanNotDelete` lock on the storage account in the owner-run `storage-access.bicep`, and mention it in the runbook.
  - Strength: Fits the existing owner-only bootstrap path, so CI permissions stay narrow, as the plan requires.
  - Tradeoff: The owner must remove the lock deliberately before any intentional teardown; one more owner-run step.
  - Confidence: MED — lock creation needs Owner/User Access Administrator, which the owner entry point already assumes.
  - Blind spot: Not checked whether `infra/deploy.sh` or a teardown script would be blocked by the lock.
- **Decision**: SKIPPED

### F6 — Inline originals served without a sandbox CSP

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: services/api/Documents/DocumentEndpoints.cs:139-149
- **Detail**: Originals are served inline from the app origin with `nosniff` and a signature-checked type, but without `Content-Security-Policy`. A PDF polyglot is held back only by the browser's PDF viewer.
- **Fix**: Add `Content-Security-Policy: sandbox; default-src 'none'` to original responses, then confirm in a browser that inline PDF and image viewing still work.
- **Decision**: FIXED (differently: sandbox CSP on image originals only; PDFs keep no CSP because a sandboxed response blocks the browser's PDF viewer; tests assert both)

### F7 — Receipt ETag not enforced on read; missing hash metadata becomes 500

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: services/api/Documents/DocumentCaptureService.cs:207, services/api/Storage/BlobOriginalStorage.cs:57, 74
- **Detail**: Reads do not compare the recorded receipt ETag, so a blob changed outside the app would be served silently. A blob without `sha256` metadata throws `InvalidOperationException`. That becomes a non-retryable 500 and permanently blocks completion of the operation.
- **Fix**: Open with `IfMatch = ReceiptEtag` and treat a mismatch as `original_unavailable`. Document the missing-metadata case in docs/sop/recover-originals.md.
- **Decision**: FIXED (differently: reads compare length + sha256 metadata with the database instead of the ETag, so version restores keep working; mismatch → original_unavailable; SOP updated; new adapter test)

### F8 — "10 MB" wording vs. 10 MiB limit

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: apps/web/src/i18n/en.ts:79, apps/web/src/i18n/pl.ts
- **Detail**: Commit 7d47157 simplified the message to "The maximum size is 10 MB", while the plan, README and API use 10 MiB (10,485,760 bytes). Behaviour is correct and more permissive than the text, so this is only imprecise wording.
- **Fix**: Keep as a deliberate simplification, or say "10 MiB" if precision matters.
- **Decision**: SKIPPED

### F9 — Small duplications: double SHA-256 and copied `CurrentUserId`

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: services/api/Documents/DocumentFileValidator.cs:82, services/api/Storage/BlobOriginalStorage.cs:25, services/api/Documents/DocumentEndpoints.cs:183-184
- **Detail**: Each upload hashes the same buffer of up to 10 MiB twice, once in `Verify` and once in `CreateIfAbsentAsync`, which matters on F1's CPU quota. `CurrentUserId` is copied verbatim from `AnimalEndpoints.cs:55-56`.
- **Fix**: Pass the verified hash into the storage adapter, and move `CurrentUserId` to a shared helper in `Auth/`.
- **Decision**: FIXED (CreateIfAbsentAsync takes an optional verifiedSha256 passed by the capture service; CurrentUserId moved to Auth/CurrentUser.cs)

### F10 — Antiforgery applied only to document routes

- **Severity**: 💬 OBSERVATION
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Pattern Consistency
- **Location**: services/api/Animals/AnimalEndpoints.cs:17, services/api/Auth/AuthEndpoints.cs:14-16
- **Detail**: The new `AntiforgeryHeaderFilter` protects `/api/document-uploads` as planned. `POST /api/animals/` and the auth POST routes still rely on the JSON content-type and SameSite cookies, so logout can be triggered cross-site. This gap predates the change, and the plan scoped antiforgery to the new capture mutations only.
- **Fix**: Open a follow-up change that applies the filter to all cookie-authenticated mutations, together with the matching frontend token handling. Keep this change's scope unchanged.
  - Strength: The mechanism now exists and is tested; reuse is cheap.
  - Tradeoff: Touches auth and onboarding flows outside this change's scope.
  - Confidence: MED — the frontend `api.ts` already has a token-fetch path to reuse.
  - Blind spot: Not checked how the sign-in/register flows would get a token before a session exists.
- **Decision**: FIXED (queued as a separate change in follow-ups/review-fixes.md; no code change in this change)
