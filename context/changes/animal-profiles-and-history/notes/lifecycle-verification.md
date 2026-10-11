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

## PENDING: S-03 integration checks

S-03 (`read-document-content`: reading/OCR jobs and document status) is not integrated on this branch, so none of this has been exercised yet. After S-03 is rebased onto S-06 and its migration is generated against the combined model:

- [ ] The combined EF Core model has no pending changes.
- [ ] Reading/OCR jobs are created and processed for documents of inactive animals, and no activity check is added to job authorization or candidate selection.
- [ ] The document status shown in the UI stays available for documents of inactive animals.
- [ ] Lifecycle, reading and original isolation regression tests pass together.

Evidence: _not yet recorded_.

## PENDING: post-deployment two-account evidence

Deployment requires separate approval through the existing workflow; S-06 needs no infrastructure change. Nothing has been deployed or verified in Azure for this change yet. After an approved deployment, with two synthetic accounts (no real veterinary records):

- [ ] Management isolation: account B cannot see, open, rename, change the status of or capture into account A's animals, histories or originals.
- [ ] Stale-edit rejection: an edit from a second tab/device is rejected in the first until it refreshes and reapplies; nothing is silently overwritten.
- [ ] All-inactive access: with every animal inactive, Animals shows guidance plus the Inactive section, Add offers no fresh capture, and history/originals open.
- [ ] An upload accepted before inactivation completes and its originals are retrievable byte for byte.
- [ ] No deletion action and no promise of a reliable medical timeline appears in the PL or EN interface.

Evidence (date, release/commit, environment host, account labels, results): _not yet recorded_.

## Owner review

- [ ] Owner reviewed these lifecycle and rollback notes.
