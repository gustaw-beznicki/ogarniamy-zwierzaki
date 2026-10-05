# Animal profiles and history Implementation Plan

## Overview

Deliver S-06: create several name-only animal profiles, edit/mark inactive/reactivate them, and browse their existing document history by event date. Keep inactive archives accessible and exclude inactive animals from new captures.

Planning complexity MEDIUM, the shared five-question budget and this four-phase structure were confirmed on 2026-10-04. The owner accepted reversible activity and completion of accepted uploads after inactivation, and chose rejection of stale edits across tabs/devices. Planning does not authorize implementation, commits or deployment.

## Current State Analysis

At local commit `8a4887c081d510295b9c50b60a25d0eb6b54d03c`, the API already creates multiple animals with owner memberships and permits duplicate names. Animal and its response contain no activity/version data; the UI only lists animals. S-02 already provides a paginated, owner-scoped history and immutable original access. Extend its history screen rather than introducing another document-list route.

Capture defaults currently include all owned animals. The saved last-capture preference falls back to earliest creation time and ID. The onboarding gate uses whether the owner has any animals, not whether any are active; preserve that distinction. S-02 verification is pending and current code does not establish completed production acceptance.

## Desired End State

- Owners add name-only animals from Animals, rename profiles, mark inactive and reactivate without deletion.
- Animals shows active profiles and stored-document counts, with a collapsed inactive section preserving a route to those archives.
- The current `/animal-documents/?animalId=...` route becomes the profile, retaining paginated history and original navigation in PL/EN. Event date ordering remains distinct from upload time and does not promise a reliable medical timeline.
- Inactive animals remain editable, readable and searchable by future S-04 work; they are excluded from new capture defaults/assignments. With no active animals, Add guides owners to create/reactivate one while archive access remains available.
- Already accepted upload manifests may upload/retry/complete after inactivation. Fresh manifest creation and activity updates are serialized to define this boundary under concurrency.
- Stale rename/activity requests fail explicitly and offer refresh/reapply. Rename cannot accidentally change activity.

### Key Discoveries:

- `services/api.Tests/AnimalTests.cs:101` — multiple animals and duplicate names already supported.
- `services/api/Animals/OwnedAnimals.cs:27` — atomic profile/membership creation.
- `services/api/Documents/OwnedDocuments.cs:51` — existing EventDate, UploadedAt, ID descending order.
- `services/api/Auth/MeEndpoint.cs:26` and `apps/web/src/components/SessionGate.tsx:22` — onboarding depends on the all-owned count.
- `services/api/Documents/DocumentCaptureService.cs:39` — existing-operation lookup precedes fresh manifest creation.
- `context/foundation/ui-mockup/README.md` E06/E07 — profile management, inactive grouping and reactivation.

## What We're NOT Doing

- Animal/document deletion, sharing/caretaker roles, species/date-of-birth fields or medical inference.
- Document metadata reassignment/editing, a separate timeline feature, event-date filters or guaranteed chronology.
- OCR/search/embeddings, owned by S-03 and later slices.
- New capture assignment to inactive animals or cancellation/deletion of accepted uploads when an animal becomes inactive.
- Globally filtering inactive animals out of EF queries, original access or processing.
- A general account/security refactor, new dependencies or infrastructure resources.

## Implementation Approach

Add IsActive default true and an opaque Version UUID to animals; versions change only when name/activity actually changes. Expose status/version and a Stored-document count in owned responses. Separate name and activity routes with required If-Match headers. Use conditional writes and return a stale-change error rather than silently overwriting newer data.

Keep membership authorization independent of activity. Explicit active-only queries serve capture defaults and fresh manifest acceptance; all-owned queries serve onboarding, direct profile/history/original access and management grouping. Add antiforgery protection to animal creation and management, updating onboarding and test callers together.

Extend AnimalsList and AnimalDocuments with forms/actions using the existing shell, translation files and tokens. Preserve query parameters and existing routes. No additional frontend test framework is required.

## Critical Implementation Details

### Parallel implementation and migration order

Both changes release only after S-02 verification. Create separate worktrees from the same verified S-02 commit. S-06 owns and merges the animal migration first; S-03 rebases and generates its processing migration afterward. Domain code and tests can proceed in parallel, but EF snapshots must be generated against the integrated model.

S-06 owns Animals, active capture defaults, fresh upload eligibility, AnimalsList, AnimalDocuments and DocumentCaptureForm. S-03 owns reading files, completion scheduling and the document-detail status component. Compose OwnedDocuments edits rather than selecting one version. Integrate DbContext/snapshot, API client, translations, Program, shared tests and docs sequentially; retain both features.

### Archive access and stale writes

hasAnimals continues to count inactive owned profiles. No IsActive filter belongs in owner-history/original queries, accepted-operation lookup or OCR candidates. After any successful rename/activity change, a new UUID invalidates stale edit versions. An explicit refresh must replace the UI's version before a user reapplies an edit; never automatically replay a failed stale mutation.

### Upload/activity serialization

Fresh manifest insertion and activity mutation must lock the same owned animal row before checking eligibility/version and writing. Whichever transaction commits first defines whether the manifest was accepted before inactivation. Existing manifest retries/completion do not acquire activity eligibility or version requirements. Keep completion's document/account lock order unchanged; inactivation need not clear the saved capture preference because active defaults ignore it.


## Phase 1: Versioned animal model and API

### Overview

Add reversible activity and stale-edit protection while preserving existing onboarding and owner isolation.

### Changes Required:

#### 1. Activity/version schema and response

**Files**: services/api/Animals/Animal.cs, OwnedAnimal.cs, services/api/Data/AppDbContext.cs, Data/Migrations/<timestamp>_AnimalActivity.cs and generated companions

**Intent**: Preserve existing profiles and give clients enough information for management and concurrency.

**Contract**: Animal gets non-null IsActive default true and non-null Version UUID; backfill existing rows with distinct values using PostgreSQL gen_random_uuid() and generate a fresh UUID on an actual name/activity change. OwnedAnimal returns {id, name, isActive, version, storedDocumentCount}; counts include Stored documents only, projected in one owner-scoped query without N+1 reads. All-owned CountAsync remains separate from active list filtering.

#### 2. Owner repository and versioned routes

**Files**: services/api/Animals/OwnedAnimals.cs, AnimalEndpoints.cs, RenameAnimalRequest.cs, SetAnimalActivityRequest.cs, AnimalMutationResult.cs (new as needed)

**Intent**: Provide explicit rename/activity operations with owner-scoped concurrency checks.

**Contract**: GET /api/animals/ defaults to active profiles, with ?includeInactive=true returning both for grouping; retain creation-time/ID order. GET /api/animals/{id} returns either activity state when owned. POST creation retains trimmed nonempty name length 1–100 and duplicate-name support; new animals are active. PUT /api/animals/{id}/name accepts {name}; PUT /api/animals/{id}/activity accepts {isActive}. Both require If-Match containing the quoted response version UUID. Missing header: 428 version_required; malformed header/body: 400 invalid_version or invalid_activity; stale version: 412 animal_changed. Check ownership before disclosing version mismatch; missing/foreign IDs return 404. Conditional updates are atomic, and successful mutations return current OwnedAnimal. No-op requests with the current version preserve it; stale no-op requests still return 412. Serialize activity changes and fresh manifests on the same animal row; rename uses its expected version atomically. No delete route.

#### 3. Antiforgery integration and callers

**Files**: services/api/Animals/AnimalEndpoints.cs, apps/web/src/lib/api.ts, components/FirstAnimalForm.tsx, services/api.Tests/TestAccounts.cs or existing creation helper, AnimalTests.cs, AnimalIsolationTests.cs

**Intent**: Protect animal mutations without breaking onboarding or replaying non-idempotent creation.

**Contract**: Apply the existing AntiforgeryHeaderFilter to POST/PUT mutations while keeping GETs token-free. Add a general account-aware mutation helper separate from captureMutation: acquire token, send once, clear cached token on explicit rejection and require an explicit resubmit; do not blindly replay POST creation or stale PUTs. Update onboarding and synthetic test creation helpers to get real tokens. Animal type includes status/version/count; client update calls send If-Match. A lost create response offers refresh of Animals before another explicit submission; no exactly-once creation claim is made.

### Success Criteria:

#### Automated Verification:

- API restore/build/test and frontend check/build pass: `dotnet restore services/api/ogarniamy-zwierzaki-api.csproj`, `dotnet build services/api/ogarniamy-zwierzaki-api.csproj --no-restore`, `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`, `npm run check --prefix apps/web` and `npm run build --prefix apps/web`.
- Migration tests preserve existing animal/membership/document data, initialize activity/version, and retain all-owned onboarding counts.
- API tests prove name limits/duplicates, Stored-only counts, 401/404 isolation, antiforgery rejection, required/malformed/stale versions, atomic competing edits and no-op version behavior.

#### Manual Verification:

- Existing PL/EN registration and first-animal onboarding still work with antiforgery-protected creation.

**Implementation Note**: Keep manual items pending until their evidence is confirmed; follow the implementation workflow before proceeding past a manual gate. Production mutations require separate approval.

## Phase 2: Capture eligibility and upload races

### Overview

Exclude inactive animals from fresh capture while keeping accepted work and archives intact.

### Changes Required:

#### 1. Active defaults and manifest acceptance

**Files**: services/api/Animals/OwnedAnimals.cs, services/api/Documents/OwnedDocuments.cs (defaults/AddUploadAsync only), DocumentCaptureService.cs, DocumentCaptureFailure.cs

**Intent**: Apply activity at the capture boundary instead of global archive filtering.

**Contract**: Capture defaults return active owned animals only: saved last-capture animal when active, otherwise earliest-created active animal then ID; no eligible animals means [] and null. Fresh manifest creation resolves ownership, locks the owned animal row and rechecks IsActive within the insertion transaction; inactive owned animal returns 409 animal_inactive, foreign/missing remains 404. Resolve existing operation IDs first: identical accepted manifests remain retryable regardless of current activity, changed manifests conflict. Completion retains its existing semantics and may write an inactive last-capture preference, which defaults ignore. Repeated/concurrent manifest ID collisions must still resolve through owner-scoped lookup.

#### 2. Capture UI when eligibility changes

**Files**: apps/web/src/components/DocumentCaptureForm.tsx, apps/web/src/lib/api.ts, apps/web/src/i18n/en.ts, pl.ts

**Intent**: Keep selection errors understandable without dropping files or confusing inactive state with onboarding.

**Contract**: With no active animals, replace fresh capture controls with create/reactivate guidance linking to Animals. A deep-linked inactive ID cannot become selected. Before a manifest is accepted, animal_inactive refreshes defaults, preserves files/date and asks for an eligible selection; never silently reassign files to another animal or reuse a changed manifest ID. After acceptance, freeze/retry the existing manifest even if defaults later exclude its animal. Rename/status changes from another tab may update labels on refresh without altering an accepted manifest. Keep normal session/account isolation and existing retry behavior.

#### 3. Concurrency and regression coverage

**Files**: services/api.Tests/AnimalCaptureEligibilityTests.cs (new), DocumentRepositoryTests.cs, DocumentRecoveryTests.cs

**Intent**: Prove strict acceptance ordering under inactivation races and durable archive access.

**Contract**: Use deterministic transaction coordination against real PostgreSQL to test both race orders: insert-first is accepted and may finish, inactivate-first rejects fresh insertion. Test retry-after-inactivation, all-inactive defaults with hasAnimals true, preference fallback/reactivation, pending invisibility, and byte-identical inactive originals. Do not introduce activity checks into history/FindUploadAsync/StoredForUser or S-03 job authorization.

### Success Criteria:

#### Automated Verification:

- API build/test and frontend check/build pass: `dotnet build services/api/ogarniamy-zwierzaki-api.csproj`, `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`, `npm run check --prefix apps/web` and `npm run build --prefix apps/web`.
- Real PostgreSQL tests prove both upload/inactivation race orders, accepted-manifest retry/completion, active default fallback and all-inactive archive access.

#### Manual Verification:

- Two tabs prove that inactivation removes fresh capture choices, preserves selected files when rejected, and does not strand an accepted upload.

**Implementation Note**: Keep manual items pending until their evidence is confirmed; follow the implementation workflow before proceeding past a manual gate. Production mutations require separate approval.

## Phase 3: Animal management and profile UI

### Overview

Deliver add/edit/inactivate/reactivate and history navigation using the existing screens in both locales.

### Changes Required:

#### 1. Animals management and shared form

**Files**: apps/web/src/components/AnimalsList.tsx, AnimalNameForm.tsx (new), FirstAnimalForm.tsx where reuse is useful, apps/web/src/i18n/en.ts, pl.ts, styles/tokens.css

**Intent**: Make multiple animals manageable without expanding profile data or duplicating navigation.

**Contract**: Animals requests includeInactive=true, shows active rows with Stored counts, a collapsed accessible Inactive section and Add animal action with a name-only form. Newly created profiles navigate to the existing profile/history route; normal submission requires explicit action and disables duplicate clicks. No active profiles is an empty active section with create/reactivate guidance, not an onboarding redirect. Inactive rows remain reachable. Keep separate errors/loading and keyboard/focus behavior; names are plain text.

#### 2. Extend profile/history and stale conflict UX

**Files**: apps/web/src/components/AnimalDocuments.tsx, AnimalNameForm.tsx, apps/web/src/lib/api.ts, apps/web/src/i18n/en.ts, pl.ts, styles/tokens.css

**Intent**: Expose profile controls alongside the already implemented history without hiding archived documents.

**Contract**: Keep /animal-documents/?animalId=<uuid> and /en/animal-documents/ equivalents. Show name, activity, rename and explicit mark inactive/active action; inactivation includes a concise confirmation explaining originals/history are retained. Active profiles expose Add document preselected to this animal; inactive profiles expose reactivation instead. Retain existing paginated history order, unavailable state and original links. Mutation forms capture the version at edit start. On 412, preserve the typed draft, show that another tab/device changed the profile, and require Refresh followed by explicit reapply; activity conflicts require the owner to review current status and confirm again. Refresh updates name/status/version while retaining already loaded history; no blind mutation replay. Success updates response/version and labels. Locale switching preserves animalId. Event date remains clearly labeled and no generated clinical timeline claim is added.

### Success Criteria:

#### Automated Verification:

- Frontend check/build and API regression suite pass: `npm run check --prefix apps/web`, `npm run build --prefix apps/web` and `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`.
- Existing document ordering/pagination/isolation tests remain green; API conflict tests prove rename and activity cannot silently overwrite a newer version.

#### Manual Verification:

- PL/EN phone and desktop flows add, rename, inactivate/reactivate and browse active/inactive history, with keyboard-accessible controls and retained locale resource selection.
- Two-tab stale rename/activity conflicts preserve drafts, require refresh/reapply and never silently overwrite newer edits; all-inactive accounts keep archive access.

**Implementation Note**: Keep manual items pending until their evidence is confirmed; follow the implementation workflow before proceeding past a manual gate. Production mutations require separate approval.

## Phase 4: Integrated verification and documentation

### Overview

Verify the profile lifecycle on the deployed app and document the implemented behavior and parallel integration.

### Changes Required:

#### 1. Acceptance tests and product/developer docs

**Files**: services/api.Tests/AnimalLifecycleTests.cs (new), README.md, docs/architecture.md, testing.md, context/changes/animal-profiles-and-history/notes/lifecycle-verification.md (new during implementation)

**Intent**: Capture meaningful security/concurrency regressions and explain archive-preserving lifecycle behavior.

**Contract**: Test two synthetic accounts over create/list/detail/edit/status/capture/history/original routes with real auth/antiforgery. Confirm S-03 jobs/status, when integrated, remain available for inactive documents. Update current product status without describing unimplemented search as available. Document versions/If-Match/error semantics, no-active-animal state, accepted upload policy and forward-only migration/rollback. Store one-time deployed evidence under change notes. No infrastructure mutation is required by S-06; deployment follows existing approval workflow.

### Success Criteria:

#### Automated Verification:

- Full component checks pass: `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`, `dotnet build services/api/ogarniamy-zwierzaki-api.csproj`, `npm run check --prefix apps/web` and `npm run build --prefix apps/web`.
- After S-03 integration, the combined migration model has no pending changes and lifecycle/reading/original isolation regression tests pass.

#### Manual Verification:

- After separately approved deployment, two synthetic accounts verify management isolation, stale-edit rejection, all-inactive access and completion/retrieval of an upload accepted before inactivation.
- Owner reviews the lifecycle/rollback notes and confirms no deletion action or misleading event-date timeline promise appears in the interface.

**Implementation Note**: Keep manual items pending until their evidence is confirmed; follow the implementation workflow before proceeding past a manual gate. Production mutations require separate approval.

## Testing Strategy

### Unit Tests:

Use existing validation patterns. Test meaningful name/version parsing boundaries only where integration assertions do not cover them; do not add a frontend test framework solely for simple forms.

### Integration Tests:

Use PostgreSQL/Azurite, real cookies and antiforgery. Cover duplicate names and 100/101-character boundaries, atomic create/membership, active/all list scopes, Stored counts, name/status independence, 428/400/412 version behavior, two competing expected-version updates, lifecycle/upload races, preference fallback and inactive original/history access. Fresh and previous-schema migration tests preserve accounts and documents. Prove foreign requests cannot learn activity/version or mutate owner resources.

### Manual Testing Steps:

1. Add two animals, rename one and view their separate event-date histories.
2. Begin edits in two tabs; save one, then confirm the stale request is rejected and refresh/reapply is explicit.
3. Accept an upload, inactivate its animal in another tab, and finish/retry that accepted capture.
4. Try a fresh capture after inactivation, including a stale deep link; keep selected files and require an eligible choice.
5. Inactivate the final active animal; browse retained archives, then create/reactivate an animal and capture again.
6. Repeat PL/EN navigation and phone/keyboard checks with another account to verify isolation.

## Performance Considerations

Project document counts in scoped SQL rather than fetching histories per animal. Retain existing indexed, paginated history and date/ID tie-breakers. Activity/manifest transactions hold one animal lock briefly and perform no Blob or OCR calls while locked. Small owner-managed animal lists keep their current non-paginated shape; no scale claim is implied. Stale version rejection requires one explicit refresh, not polling every profile.

## Migration Notes

Generate and integrate S-06's additive migration first, then S-03's against the combined model. All existing animals become active; backfill independent versions. Rollback restores code, not activity/version/document rows. A pre-S-06 release does not enforce inactive capture eligibility; if rolled back, pause new capture until a forward correction restores the rule while leaving original retrieval available. Do not downgrade/delete data or remove memberships. Preserve archived animals for future search and keep hasAnimals based on the all-owned set.

## References

- `context/changes/animal-profiles-and-history/research.md`; companion `context/changes/read-document-content/plan.md`.
- `context/foundation/prd.md` FR-004/005/014 and Access Control; `roadmap.md`; `ui-mockup/README.md` E06/E07; `lessons.md`.
- `services/api/Animals/OwnedAnimals.cs:27`, `services/api/Documents/OwnedDocuments.cs:51`, `services/api/Auth/MeEndpoint.cs:26`.
- `apps/web/src/components/AnimalDocuments.tsx:35`, `SessionGate.tsx:22`.

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: Versioned animal model and API

#### Automated

- [x] 1.1 API restore/build/test and frontend check/build pass: `dotnet restore services/api/ogarniamy-zwierzaki-api.csproj`, `dotnet build services/api/ogarniamy-zwierzaki-api.csproj --no-restore`, `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`, `npm run check --prefix apps/web` and `npm run build --prefix apps/web`.
- [x] 1.2 Migration tests preserve existing animal/membership/document data, initialize activity/version, and retain all-owned onboarding counts.
- [x] 1.3 API tests prove name limits/duplicates, Stored-only counts, 401/404 isolation, antiforgery rejection, required/malformed/stale versions, atomic competing edits and no-op version behavior.

#### Manual

- [x] 1.4 Existing PL/EN registration and first-animal onboarding still work with antiforgery-protected creation.

### Phase 2: Capture eligibility and upload races

#### Automated

- [ ] 2.1 API build/test and frontend check/build pass: `dotnet build services/api/ogarniamy-zwierzaki-api.csproj`, `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`, `npm run check --prefix apps/web` and `npm run build --prefix apps/web`.
- [ ] 2.2 Real PostgreSQL tests prove both upload/inactivation race orders, accepted-manifest retry/completion, active default fallback and all-inactive archive access.

#### Manual

- [ ] 2.3 Two tabs prove that inactivation removes fresh capture choices, preserves selected files when rejected, and does not strand an accepted upload.

### Phase 3: Animal management and profile UI

#### Automated

- [ ] 3.1 Frontend check/build and API regression suite pass: `npm run check --prefix apps/web`, `npm run build --prefix apps/web` and `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`.
- [ ] 3.2 Existing document ordering/pagination/isolation tests remain green; API conflict tests prove rename and activity cannot silently overwrite a newer version.

#### Manual

- [ ] 3.3 PL/EN phone and desktop flows add, rename, inactivate/reactivate and browse active/inactive history, with keyboard-accessible controls and retained locale resource selection.
- [ ] 3.4 Two-tab stale rename/activity conflicts preserve drafts, require refresh/reapply and never silently overwrite newer edits; all-inactive accounts keep archive access.

### Phase 4: Integrated verification and documentation

#### Automated

- [ ] 4.1 Full component checks pass: `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`, `dotnet build services/api/ogarniamy-zwierzaki-api.csproj`, `npm run check --prefix apps/web` and `npm run build --prefix apps/web`.
- [ ] 4.2 After S-03 integration, the combined migration model has no pending changes and lifecycle/reading/original isolation regression tests pass.

#### Manual

- [ ] 4.3 After separately approved deployment, two synthetic accounts verify management isolation, stale-edit rejection, all-inactive access and completion/retrieval of an upload accepted before inactivation.
- [ ] 4.4 Owner reviews the lifecycle/rollback notes and confirms no deletion action or misleading event-date timeline promise appears in the interface.
