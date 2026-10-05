---
date: 2026-10-04T12:41:03+02:00
researcher: Codex
git_commit: 8a4887c081d510295b9c50b60a25d0eb6b54d03c
branch: capture-document-original-p1
repository: 10xdevs
topic: "S-06 animal lifecycle, capture eligibility and archive integration"
tags: [research, codebase, animals, documents]
status: complete
last_updated: 2026-10-04
last_updated_by: Codex
last_updated_note: "Owner accepted inactive capture policy and chose rejection of stale edits."
---

# Research: Animal profiles and history

## Research Question

What remains to implement for multiple animals, editing/inactivation and event-date history, and how should S-06 integrate safely alongside S-03?

## Summary

The backend already accepts multiple animals and duplicate names (`services/api.Tests/AnimalTests.cs:101`). The inspected UI lists animals without creation/edit/activity management (`apps/web/src/components/AnimalsList.tsx:17`). Owner-scoped, paginated event-date history exists (`services/api/Documents/OwnedDocuments.cs:38`, `apps/web/src/components/AnimalDocuments.tsx:35`); extend it into the profile rather than duplicating it.

Activity must affect capture eligibility rather than archive authorization. Preserve the onboarding predicate based on owning an animal, so an account with inactive animals retains archive access (`services/api/Auth/MeEndpoint.cs:26`, `apps/web/src/components/SessionGate.tsx:22`).

## Detailed Findings

### Model and API

- Animal stores ID, name and creation time (`services/api/Animals/Animal.cs:3`); its DTO contains ID/name (`services/api/Animals/OwnedAnimal.cs:3`). No activity field exists in these inspected types.
- The route mapper exposes list/get/create (`services/api/Animals/AnimalEndpoints.cs:15`). Creation trims names, rejects blank names and caps length at 100 (`AnimalEndpoints.cs:39`). Duplicate names are supported by `AnimalTests.cs:101` and should not acquire a new uniqueness rule silently.
- Creation saves the animal and owner membership together (`services/api/Animals/OwnedAnimals.cs:27`). The membership query currently scopes by user but not explicitly by role (`OwnedAnimals.cs:44`); the database presently constrains role to owner (`services/api/Data/AppDbContext.cs:53`). Extend mutations with explicit owner qualification.
- Use additive activity data with existing rows defaulting active. Separate rename and activity-set operations avoid stale rename requests inadvertently reactivating an animal. New mutations should follow capture antiforgery handling (`services/api/Documents/DocumentEndpoints.cs:24`). Protecting existing create requires updating onboarding/client/test callers together.

### Archive visibility and default selection

- History sorts EventDate, UploadedAt and ID descending (`services/api/Documents/OwnedDocuments.cs:51`), with real PostgreSQL coverage in `services/api.Tests/DocumentRepositoryTests.cs:150`. Keep this ordering; it is not a promise of clinically reliable dates.
- Default selection uses the saved capture preference if eligible, otherwise the earliest-created animal and ID tie-breaker (`OwnedDocuments.cs:23`). Use an active-only capture query while retaining membership-only reads for history/originals.
- Requested capture animal wins when present in defaults (`apps/web/src/components/DocumentCaptureForm.tsx:112`). Active-only defaults exclude inactive deep-link choices. Empty defaults currently lack a dedicated no-active-animals management state (`DocumentCaptureForm.tsx:119`).
- The profile's Add document action currently appears without an activity condition (`AnimalDocuments.tsx:75`). Inactive profiles need readable history plus reactivation, with capture unavailable until active.
- Preserve all-owned CountAsync (`OwnedAnimals.cs:23`) for hasAnimals; SessionGate uses this to decide onboarding (`apps/web/src/components/SessionGate.tsx:22`). A global active filter would strand all-inactive accounts.

### Upload/deactivation race

- Manifest creation checks for an existing operation before insertion (`services/api/Documents/DocumentCaptureService.cs:39`). Proposed rule: retry/finish accepted manifests after inactivation, but reject fresh assignments to an inactive animal. This distinguishes archive durability from capture eligibility.
- A plain activity precheck before insert permits a race with deactivation. Lock the same owned animal row in both fresh manifest creation and activity mutation if strict sequencing is selected. Accepted-operation lookups and completion should remain independent of activity.
- Completion locks document then account (`OwnedDocuments.cs:196`); keep the new lifecycle lock out of that completion path. The saved preference may remain inactive and be ignored by active defaults, avoiding an unnecessary cross-resource lock during deactivation.

## Code References

- `services/api/Animals/Animal.cs:3` — name-only model.
- `services/api/Animals/OwnedAnimals.cs:27` — atomic create and membership.
- `services/api/Animals/AnimalEndpoints.cs:39` — name validation.
- `services/api/Documents/OwnedDocuments.cs:23` — capture defaults.
- `services/api/Documents/OwnedDocuments.cs:51` — existing history ordering.
- `apps/web/src/components/AnimalsList.tsx:17` — current list UI.
- `apps/web/src/components/AnimalDocuments.tsx:35` — history screen to extend.
- `apps/web/src/components/SessionGate.tsx:22` — onboarding/archive routing invariant.

## Architecture Insights

Do not add a global EF activity filter. Separate active capture selectors from all-owned authorization. Prefer reusing `/animal-documents/?animalId=...` and its EN equivalent for the profile to preserve original navigation. S-06 owns animal lifecycle, capture defaults and capture form eligibility; S-03 owns processing entities, worker and document-status UI. Shared model/snapshot, API client, translations and docs need explicit integration ownership. Generate the second EF migration from the first merged migration and compose both model changes.

## Historical Context

`context/changes/capture-document-original/plan.md` deliberately implemented a minimal retrieval list while deferring full profiles. Current history source supports that contract. `context/foundation/ui-mockup/README.md` E06/E07 includes name edit, inactive grouping and reactivation; document counts are a mockup detail rather than a separate PRD requirement.

## Related Research

`context/changes/read-document-content/research.md` covers background processing; activity must not stop it from reading an archived animal's stored documents.

## Open Questions

Internal discovery is complete. On 2026-10-04 the owner accepted reversible activity, rename on inactive profiles, active-only new capture, completion of accepted manifests after inactivation, and create/reactivate guidance when no active animals remain. The owner chose to reject stale edits across tabs/devices; use version tokens and explicit refresh/reapply UX rather than last-write-wins. No new secrets or external provider are needed for this scope. S-02 verification remains the release prerequisite.
