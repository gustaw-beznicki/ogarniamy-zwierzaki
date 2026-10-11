# Tests and checks

```bash
dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj   # needs Docker running
dotnet build services/api/ogarniamy-zwierzaki-api.csproj
npm run check --prefix apps/web
npm run build --prefix apps/web
infra/deploy.sh lint                                                  # needs the Azure CLI with Bicep
```

The tests boot the real API against throwaway PostgreSQL 17 and Azurite containers (the Azurite image is pinned to the version in `compose.yaml`). Each test class gets its own containers; startup applies the normal migrations and creates the private originals container, and the production Blob Storage adapter is used. No local database, emulator or Azure credentials are involved: the test host pins `DocumentReading:Auth` to `Disabled` and never starts the background worker, so tests never call Azure. Pull-request CI runs all of the above plus an infrastructure `what-if`.

## What the API tests cover

- Accounts, sessions, animals and isolation between accounts: anonymous requests get 401 without a redirect, and another account's animals, documents, upload operations and originals get 404.
- Animal profiles: name limits and duplicate names, stored-only document counts, active and all-animal lists, independent rename and activity changes, 428/400/412 `If-Match` handling, exactly one winner among competing edits with the same version, no-op version behaviour and no delete route. The migration test checks that existing animals, memberships and documents survive and become active with distinct versions.
- Animal lifecycle (`AnimalLifecycleTests`): two synthetic accounts with real session cookies and antiforgery tokens go through create, list, detail, rename, activity, capture, history and original routes; an edit from a second signed-in device makes the first device's version stale until it refreshes; with every animal inactive, archives stay open, onboarding is not reopened and an upload accepted before the inactivation still completes.
- Capture eligibility (`AnimalCaptureEligibilityTests`): both orders of a race between a fresh upload and an inactivation, coordinated with real PostgreSQL row locks; accepted operations stay retryable after inactivation; defaults fall back to the earliest active animal and return to a reactivated preference.
- Capture rules: one PDF (any number of internal pages) or 1–10 JPEG/PNG images; 10,485,760 bytes accepted and one byte more refused; empty files, the same file twice in one document, unsupported formats, mismatched bytes, malformed dates and unknown time zones refused; today and earlier accepted and tomorrow refused, in time zones on both sides of UTC midnight.
- Antiforgery: every capture mutation without a valid token for the signed-in account is refused before anything changes.
- Originals: returned byte for byte in their confirmed order with private, non-cacheable headers and byte ranges, after an API restart and a fresh sign-in, with no content-processing service.
- Retries and failures: repeated and concurrent create, upload and completion produce one document; failures are injected at the storage adapter (`FaultyOriginalStorage`) and the EF Core boundary (`InjectedDatabaseFaults`) to cover a blob written before a database failure, a lost storage response, a lost completion response, a storage outage during upload, completion and reading, and a missing blob. Each answers with a retryable 503 and keeps the records.
- Document reading: completion schedules exactly one job in the same transaction (also under concurrent and repeated completion) and a scheduling failure rolls the completion back; competing claims, lease expiry, stale-lease fencing and atomic publication run against real PostgreSQL. The worker is driven one job at a time (`RunOnceAsync`) with a fake OCR provider (`FakeOcrProvider`) and synthetic PDFs generated with PdfPig. It covers local-only PDF routing, mixed text/OCR pages, photo order, empty and over-limit documents, parser time limits, restarts after submission and retrieval, bounded retries, provider cleanup retry and byte-identical originals during reading failures.
- Reading status and Retry: owner isolation (401 anonymous, 404 for other accounts, missing and uploading documents), antiforgery, coalesced repeats, the durable hourly retry limit, and unchanged completion and detail responses.
- Reading with animal profiles (`DocumentReadingAcceptanceTests`), on a database migrated through both changes: an inactive animal's document keeps its original, status and Retry and is still read; a stale animal edit (old `If-Match`) is rejected with 412 and leaves reading untouched; a repeated completion returns the same document and keeps exactly one job.
- Logs: capture and reading logs carry IDs, states and failure codes, never file names, extracted text or provider payloads.

Test files are synthetic: `SampleOriginals` holds small well-formed PNG, JPEG and multi-page PDF files, `TestOriginal` generates bodies of exact sizes, and `ReadingFixtures` builds text, scanned and mixed PDFs (plus a committed fictional note image and a password-protected PDF). Never use real veterinary records or production credentials in tests.

The frontend has type and build checks only; there is no frontend test framework.
