# Animal lifecycle verification and rollback notes

Owner-facing notes for S-06 (`animal-profiles-and-history`). Product and developer behaviour is documented in `README.md`, `docs/architecture.md` (Animals and profiles) and `docs/testing.md`; this file holds the change-specific rollback guidance and verification evidence.

## Lifecycle summary

- Animals are never deleted. Owners rename them and mark them inactive or active again; documents and originals of inactive animals stay listed and retrievable.
- Inactive animals take no new captures: capture defaults offer active animals only, and a fresh upload for an inactive animal answers 409 `animal_inactive`. An upload accepted before the inactivation still uploads, retries and completes.
- An account whose animals are all inactive keeps `hasAnimals: true`, is not sent back to onboarding, sees create/reactivate guidance and keeps every archive.
- Rename and activity changes require `If-Match: "<version>"`; a stale version answers 412 `animal_changed`, and the UI requires an explicit refresh before the owner reapplies the edit.

## Migration and rollback

- `20261005180733_AnimalActivity` is additive and forward-only: it adds non-null `IsActive` (default true) and `Version` (backfilled per row with `gen_random_uuid()`). Existing animals became active; memberships and documents are untouched.
- Rollback restores code, not data. Reverting or redeploying an earlier release leaves the columns, activity values, versions, documents and originals in place. Do not downgrade the database, delete rows or remove memberships.
- A pre-S-06 release does not know about activity: it has no rename or activity routes and would offer and accept inactive animals for capture. If the release is rolled back, pause new captures (tell users not to add documents) until a forward fix restores the activity rule. Original retrieval and history stay available throughout.
- S-03's processing migration must be generated after this one, against the combined model (see the plan's migration order).

## Local verification evidence (2026-10-11)

Automated (local PostgreSQL and Azurite via Testcontainers):

- `AnimalVersioningTests`, `AnimalActivityMigrationTests`, `AnimalCaptureEligibilityTests` (both race orders with real row locks) and the existing document isolation/ordering suites pass at commits `6789443`, `fbf7e1c` and `fcd174a`.
- `AnimalLifecycleTests` (Phase 4) passes locally: two synthetic accounts over create, list, detail, rename, activity, capture, history and original routes with real session cookies and antiforgery tokens; a second signed-in device's edit makes the first device's version stale (412) until it refreshes and reapplies; with every animal inactive, archives and originals stay available to the owner only, onboarding is not reopened, an upload accepted before inactivation completes, and reactivation restores the capture default.

Manual browser checks (local, two tabs):

- Phase 1: PL/EN registration and first-animal onboarding work with antiforgery-protected creation.
- Phase 2: inactivation in one tab removes the animal from fresh capture choices in the other; a rejected fresh capture keeps the selected files and date and asks for an eligible animal; an accepted upload is not stranded.
- Phase 3: stale rename, inactivate and reactivate from a second tab are rejected with the draft kept and an explicit refresh/reapply; an all-inactive account keeps archive and original access; switching to EN keeps `animalId`; PL/EN phone and desktop flows add, rename, inactivate/reactivate and browse active/inactive history with keyboard-accessible controls.

## S-03 integration checks (2026-10-12)

S-03 (`read-document-content`, PR #28) was merged into this branch from `main` in `d163e8e`. Migrations run in the planned order: `20261005180733_AnimalActivity`, then `20261005194436_DocumentReading`.

- [x] The combined EF Core model has no pending changes (`dotnet ef migrations has-pending-model-changes`: "No changes have been made to the model since the last migration").
- [x] Reading/OCR jobs are created and processed for documents of inactive animals, and no activity check is added to job authorization or candidate selection. `DocumentReadingWorkerTests.A_document_of_an_animal_marked_inactive_is_still_read` captures a document, marks its animal inactive through the API, runs the worker and gets a `read` job, published text and a `read` status. Adding an active-animal filter to `ClaimNextAsync` turns the test red. `IsActive` appears nowhere in `services/api/DocumentReading/`.
- [x] The document status shown in the UI stays available for documents of inactive animals. S-03's `DocumentReadingStatusTests.An_inactive_animals_document_keeps_its_status_and_retry` covers status and Retry through the API; a local browser check (worker disabled) shows "Treść czeka na odczytanie." / "Waiting to read the content." on the document page after the animal was marked inactive.
- [x] Lifecycle, reading and original isolation regression tests pass together: 213/213 `dotnet test`, plus `npm run check` and `npm run build` in `apps/web`.

## Post-deployment two-account evidence (2026-10-12)

Release `4afbb78` (merge of PR #27) was deployed by `deploy` run 38185382270 after approval. Infrastructure, API and web deployed; the run's smoke test then got one transient 502 on `/api/documents/{id}` right after the API restart (health had already passed). A rerun of `scripts/smoke.sh` a few minutes later passed every check, and the checks below ran after it.

Environment: `mango-bush-08ef40b0f.5.azurestaticapps.net` (API `app-ogarniamy-api-3jhak2vhze7d2`, reading worker enabled). Two synthetic accounts `e2e-s06-1791763214770-a@example.test` (A) and `e2e-s06-1791763217071-b@example.test` (B), animals named "S06 test …", one 625-byte synthetic text PDF ("S06 synthetic test document - not a medical record"). Driven with Playwright through the real UI and API (session cookies, antiforgery, If-Match); all 21 checks passed.

- [x] Management isolation: B got 404 on A's animal detail, rename, activity, history, document, original and reading status, and on a manifest for A's animal; B's list holds only B's animal; A's animal (name, status, version) is unchanged; the profile page shows the unavailable state to B.
- [x] Stale-edit rejection: with two tabs of A, the second tab's rename made the first tab's save fail with the "changed in another tab" notice, the draft was kept, the newer name stayed, Refresh did not replay the edit, and only the explicit save applied it.
- [x] All-inactive access: with both of A's animals inactive, `hasAnimals` stayed true, Animals showed the guidance and "Nieaktywne (2)" without an onboarding redirect, Add showed no capture controls, the inactive profile listed its document and the original downloaded byte for byte.
- [x] An upload accepted before inactivation completes: the animal was marked inactive after the manifest was accepted and the first file upload was interrupted; Retry completed the document on the now-inactive animal and its original is byte-identical.
- [x] No deletion action and no promise of a reliable medical timeline appears on the Animals, profile and document pages in PL or EN (no "usuń", "delete", "remove", "oś czasu" or "timeline").

Additionally, both synthetic documents of the inactive animals were read by the production worker (`state: read`, completed 2026-10-12 00:00 UTC), confirming that reading ignores animal activity.

## Owner review

- [x] Owner reviewed these lifecycle and rollback notes (2026-10-11).
