# Animal profiles and history — Plan Brief

> Full plan: `context/changes/animal-profiles-and-history/plan.md`
> Research: `context/changes/animal-profiles-and-history/research.md`

## What & Why

Let owners manage several name-only animal profiles and browse their veterinary documents. Inactivation preserves the archive and supports reactivation instead of deleting records.

## Starting Point

The backend already supports multiple animals/duplicate names, and S-02 implements owner-scoped paginated history. Management UI, activity/version data and stale-edit protection are missing.

## Desired End State

Owners add/rename/inactivate/reactivate animals and browse active or inactive history in PL/EN. New capture uses active animals, accepted uploads can finish after inactivation, and stale edits require explicit refresh/reapply.

## Key Decisions Made

| Decision | Choice | Why | Source |
| --- | --- | --- | --- |
| Profile fields | Name only; preserve duplicate names and existing limits | Keep the agreed MVP scope and existing behavior | PRD + Research |
| Inactive policy | Reversible; rename/history/originals retained; no fresh capture | Owner selected an archive-preserving lifecycle | Owner, 2026-10-04 |
| Accepted uploads | Finish/retry after inactivation; serialize fresh assignment with activity changes | Preserve already accepted work and define the race | Owner + Research |
| Concurrent edits | Reject stale changes with UUID Version and required If-Match | Owner rejected silent overwrites across tabs/devices | Owner + Plan |
| All-inactive account | Keep hasAnimals true; Add guides create/reactivate | Preserve archive navigation instead of onboarding loops | Owner + Research |
| Profile route/history | Extend existing animal-documents route and date ordering | Avoid a second history implementation and broken links | Research + Plan |
| Mutations | Separate name/activity; CSRF-protect create and updates | Avoid accidental activity changes and protect writes | Research + Plan |
| Migration order | S-06 migration first, then S-03 against integrated model | Keep snapshots coherent while domain work proceeds in parallel | Plan |

## Scope

**In scope:**

- Activity/version model, owner-scoped list/create/rename/activity API and Stored document counts.
- Stale conflicts, CSRF caller integration, active capture defaults and upload/inactivation sequencing.
- Animals management, inactive grouping, profile/history controls and PL/EN acceptance.

**Out of scope:**

- Deletion/sharing, species/date of birth, document reassignment/editing, clinical timeline claims and OCR/search.

## Architecture / Approach

Keep all-owned membership queries for archive/onboarding and explicit active-only queries for fresh capture. Conditional versioned mutations protect edits; fresh manifest creation and activity changes lock the same animal row. Extend the current list/profile screens and retain original access.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Versioned animal model and API | Activity/version data, stale rejection and protected callers | Onboarding regression or silent overwrites |
| 2. Capture eligibility and upload races | Active defaults and accepted-upload preservation | Global filters hiding archives or race ambiguity |
| 3. Animal management and profile UI | Create/edit/activity/history and explicit conflict flow | Lost drafts or inaccessible inactive records |
| 4. Integrated verification and documentation | Security/deployed acceptance and lifecycle notes | Combined migration or old-release rollback behavior |

**Prerequisites:** S-02 verification before release; a common verified base for separate worktrees. S-06 can be implemented independently of OCR, but merged migrations/shared code must follow the integration sequence.

**Estimated effort:** Four phases, roughly 3–5 focused sessions; an estimate, not a measured commitment. Domain work can overlap S-03.

## Open Risks & Assumptions

- A stale edit intentionally requires user refresh/reapply; never auto-replay it.
- An upload accepted earlier may finish after an animal becomes inactive.
- Today's event-date default remains the accepted bulk-import limitation; history is ordering, not a guaranteed clinical chronology.
- A pre-S-06 rollback loses inactive capture enforcement; pause new capture until a forward fix, while keeping retrieval available.
- Concurrent S-02 changes must be preserved when rebasing to the verified base.

## Success Criteria (Summary)

- Owners manage active/inactive profiles and retain original/history access even when all animals are inactive.
- Stale edits are rejected, and new capture versus inactivation has deterministic tested behavior.
- Two-account isolation and PL/EN phone/desktop acceptance pass without adding deletion or premature search claims.
