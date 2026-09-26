# Whole-app UI mockup Implementation Plan

## Overview

Roadmap F-02: a low-fidelity mockup of every screen and state of the M-1 primary flow, at phone and desktop widths, with UI text in English and a minimal set of visual tokens. The mockup is the shared visual reference that S-01…S-06 take their screens from and refine in their own plans. It is built with `/design`, published as an Artifact, and its source and screen index are kept in `context/foundation/ui-mockup/`. No component or application code is written.

## Current State Analysis

- `apps/web/src/pages/index.astro` is the bare Astro placeholder (`<h1>Astro</h1>`). There is no layout, routing, components or styles, so the mockup has no existing UI to stay consistent with.
- The screen list comes from roadmap F-02 (Outcome): sign-in/registration, first-animal onboarding, document capture, search with results and fragments, and the animal profile with its document list. Its Unlocks add the S-03 "content read / reading failed" state. S-05 (per-animal filter, "search unavailable" notice) and S-06 (several animals, inactive, list by event date) also take their screens from the mockup.
- The PRD contradicts itself on onboarding. Success Criteria step 2 lists name, species and date of birth, but FR-003 (after the Socratic round) cut species and date of birth, leaving one field. The roadmap (S-01: "name only") follows FR-003, and that is binding for this plan.
- `/design` is a built-in skill, not a repo file. It drafts `.dc.html` artboards on one canvas and publishes them as an Artifact that can be edited visually.
- `context/foundation/` already holds the long-lived references (PRD, roadmap, tech-stack) that later changes read. `context/changes/<id>/` is moved to the read-only `context/archive/` by `/10x-archive`, so it isn't a lasting place for a reference.

## Desired End State

- `context/foundation/ui-mockup/README.md` is the mockup's index. It lists every screen (IDs `E01`–`E07`) and state with PRD refs and the slice that takes it, and it includes the English copy glossary, sample data, the minimal token table, the Artifact link, and the rule that the mockup is only the M-1 starting point.
- `context/foundation/ui-mockup/mockup.dc.html` is the source of the published canvas. For every screen ID it has a phone artboard (`<ID> · phone`) and a desktop artboard (`<ID> · desktop`), plus artboards for each state from the inventory.
- In `context/foundation/roadmap.md`, Open Roadmap Question 5 records the resolution ("starting point only") with a pointer to the index.

Verification: the commands in the Success Criteria confirm files, IDs, the link and the absence of a delete action. A human reviews the canvas at both widths against the index.

### Key Discoveries:

- `context/foundation/roadmap.md:93` gives the F-02 screen list; `:96` lists the unlocks for S-01…S-06 and the S-03 state.
- `context/foundation/prd.md:171-173`: FR-003, only the name is required. `prd.md:83-84` is the conflicting Success Criteria text.
- `prd.md:141-143`: results are documents and fragments only; "search unavailable" is an explicit notice. `prd.md:290-292`: an empty result is a correct answer.
- `prd.md:265-266`: a search that takes more than about 2 s shows continuous progress.
- `prd.md:355-357`: nothing is deleted; an animal is marked inactive (FR-005, `prd.md:181-187`).
- `prd.md:203-209`: the event date defaults to today and is a known limitation. The UI keeps the event date and the upload date visibly separate (AGENTS.md invariant).
- `context/foundation/roadmap.md:204`: Open Question 5, which this plan closes.

## What We're NOT Doing

- No Astro components, styles, routing, and no changes to `apps/web` or `services/api`, so no builds are needed (`npm run build` / `dotnet build` aren't part of this change).
- No pixel-perfect design, logo, icon set, illustrations or dark mode. The visual direction stops at the minimal token table.
- No full state catalogue. Form validation errors, sign-in errors, upload size/format limits, animal edit forms, password reset and settings are left to the slices.
- No species or date-of-birth fields in onboarding (FR-003).
- No delete action anywhere (documents or animals). No keyword search and no generated health sentence in the results.
- No sharing, caretaker role or invitations (PRD Non-Goals).
- No mechanism to keep the mockup in sync with later slices. It is a snapshot of the M-1 starting point.

## Implementation Approach

Inventory first, drawing second. Phase 1 fixes the list of screens and states, the English copy, the sample data and the tokens in a text index. That is the checkpoint against polishing. Phase 2 turns the approved index 1:1 into `/design` artboards, publishes the Artifact and saves its source in the repo. The screen IDs (`E01`–`E07`) are the shared key between the index, the artboard names, and later references from slice plans.

## Critical Implementation Details

- **Repo copy after the last canvas edit.** When the owner changes something in the published canvas and saves, a new version of the Artifact appears. Before the Phase 2 commit, read the Artifact again (`Artifact` action `read`) and overwrite `mockup.dc.html` with that version, so the repo holds the final version, not the first draft.

## Phase 1: Screen inventory and tokens

### Overview

Write the mockup's index and close roadmap question 5, so the scope of the drawing is agreed before any artboard exists.

### Changes Required:

#### 1. Mockup index

**File**: `context/foundation/ui-mockup/README.md`

**Intent**: A single place where slices find which screens and states exist, where they come from in the PRD, which slice refines them, and what the mockup deliberately does not decide.

**Contract**: The file has these sections, in this order:

1. **Status and rules.** The mockup is the M-1 starting point: slices refine screens in their own plans, and the mockup is not updated after them. Low-fi. English UI (Polish UI text belongs only in future translation files; see lessons.md).
2. **Artifact.** The Artifact link and the source path `mockup.dc.html`. Phase 1 leaves a `TBD — Phase 2` placeholder here.
3. **Screens.** A table with columns ID | Screen | Contents | States | PRD refs | Slice, with these rows:
   - `E01` Sign in / Register: tabs or a toggle between sign-in and registration (FR-001, S-01).
   - `E02` First animal: onboarding with a single name field (FR-003, S-01).
   - `E03` Add document: take a photo or choose a PDF; an animal selector as one tap on chips, defaulting to the last-used animal; event date defaulting to today and editable; save without typing (US-01, FR-006/007/008, S-02).
   - `E04` Document: preview or open the original; the animal; event date and upload date shown separately, labelled; content status (FR-009, US-01, S-02/S-03).
   - `E05` Search: a query field with an example like "Czarek's heart"; results as documents with the animal name, the matching fragment and "Open original"; a per-animal filter (US-02, FR-010/011/013, S-04/S-05).
   - `E06` Animal profile: the animal's documents by event date, newest first; "Edit"; "Mark as inactive" (FR-005, FR-014, S-06).
   - `E07` Animals: a list of active animals plus a collapsed "Inactive" section (FR-004/005, S-06).

   Every screen uses a shared app shell: a bottom tab bar on phone and a side navigation on desktop, both with the entries Search / Add / Animals. S-01 turns this into the real layout.
4. **States.** A table with columns ID | Screen | State | PRD refs:
   - `E04-reading`: content is being read (FR-009).
   - `E04-failed`: reading failed, and the original is still openable (§Guardrails).
   - `E05-empty`: an empty result as a correct answer, with no suggested words (prd.md Business Logic).
   - `E05-searching`: a search running longer than 2 s shows visible progress (§NFR).
   - `E05-unavailable`: an explicit "search unavailable" notice with no fallback results (FR-012).
   - `E06-inactive`: an inactive animal's profile, whose documents are still visible (FR-005).
5. **Sample data.** Czarek and Sonia, and 4–6 English document titles and fragments (e.g. a cardiology discharge summary mentioning "cardiomyopathy", a rabies vaccination, a blood count). The dates must show at least one case where the event date differs from the upload date.
6. **Copy glossary.** The key English labels: buttons, statuses, notices and field names.
7. **Tokens.** One accent colour, neutrals, a 3–4 step type scale and a spacing scale, as a table of token → value → use. Accent and neutral contrast for body text is at least 4.5:1 (WCAG AA).
8. **What the mockup does not decide.** A list copied from "What We're NOT Doing" that applies to the UI.

#### 2. Resolve roadmap question 5

**File**: `context/foundation/roadmap.md`

**Intent**: Record the decision so slices don't reopen it.

**Contract**: Only item 5 of `## Open Roadmap Questions` changes. Append the resolution "starting point only; slices refine screens in their own plans" and the path `context/foundation/ui-mockup/README.md`. Leave the other items, and all Status fields other than F-02's, unchanged.

### Success Criteria:

#### Automated Verification:

- The index exists: `test -f context/foundation/ui-mockup/README.md`
- The index lists every screen and state ID: `for id in E01 E02 E03 E04 E05 E06 E07 E04-reading E04-failed E05-empty E05-searching E05-unavailable E06-inactive; do grep -q -- "$id" context/foundation/ui-mockup/README.md || echo "MISSING $id"; done | (! grep .)`
- The index has a token section and the "starting point" rule: `grep -qi "token" context/foundation/ui-mockup/README.md && grep -qi "starting point" context/foundation/ui-mockup/README.md`
- Roadmap question 5 points to the index: `grep -n "^5\. " context/foundation/roadmap.md | grep -q "ui-mockup/README.md"`

#### Manual Verification:

- The owner confirms the screen and state list is complete for M-1 and doesn't go beyond it (no species/date-of-birth fields, no delete action).
- The owner approves the English copy glossary, the sample data and the token table.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Mockup canvas

### Overview

Draw the approved inventory as `/design` artboards at phone and desktop widths, publish the Artifact, and save its source and link in the repo.

### Changes Required:

#### 1. `/design` canvas

**File**: `context/foundation/ui-mockup/mockup.dc.html`

**Intent**: A visual reference for the slices, reflecting the index 1:1 with nothing extra.

**Contract**:
- Artboard names use the format `<ID> · phone` (about 390 px wide) and `<ID> · desktop` (about 1280 px wide) for `E01`–`E07`. States are named `<state ID> · phone`. A state that looks different on desktop in a meaningful way also gets `· desktop`.
- Artboards are laid out in rows by flow: E01 → E02 → E03 → E04 → E05 → E06 → E07, with each screen's states next to it.
- Artboards use only the tokens and copy from the index, plus the sample data (Czarek, Sonia).
- They must not contain a delete or remove button ("Delete", "Remove"), a keyword search, or a generated sentence about the animal's health.
- E04 shows the event date and the upload date as two separate, labelled fields.
- Publish the canvas as an Artifact via `/design`. The repo file is the source of the last published version (see Critical Implementation Details).

#### 2. Link in the index

**File**: `context/foundation/ui-mockup/README.md`

**Intent**: Point slices to the live canvas.

**Contract**: The `TBD — Phase 2` placeholder in the Artifact section is replaced with the Artifact URL (`https://claude.ai/...`) and the publish date. Nothing else in the index changes, unless an ID was added or dropped during drawing, in which case both tables are updated to match the canvas.

### Success Criteria:

#### Automated Verification:

- The canvas source exists: `test -f context/foundation/ui-mockup/mockup.dc.html`
- The canvas has phone and desktop artboards for every screen: `for id in E01 E02 E03 E04 E05 E06 E07; do for w in phone desktop; do grep -q -- "$id · $w" context/foundation/ui-mockup/mockup.dc.html || echo "MISSING $id · $w"; done; done | (! grep .)`
- The canvas has every state: `for id in E04-reading E04-failed E05-empty E05-searching E05-unavailable E06-inactive; do grep -q -- "$id" context/foundation/ui-mockup/mockup.dc.html || echo "MISSING $id"; done | (! grep .)`
- There is no delete action in the mockup: `! grep -qiE '>[[:space:]]*(delete|remove)[[:space:]]*<' context/foundation/ui-mockup/mockup.dc.html`
- The index has the Artifact link and no placeholder: `grep -q "https://claude.ai/" context/foundation/ui-mockup/README.md && ! grep -q "TBD — Phase 2" context/foundation/ui-mockup/README.md`

#### Manual Verification:

- The owner opens the Artifact and walks the flow E01 → E07 on the phone and desktop artboards; each screen matches its row in the index.
- The states read unambiguously: reading/failed with the original openable, an empty result, progress over 2 s, and "search unavailable" with no results.
- The owner confirms the mockup is low-fi and not polished (only the tokens from the index, no icons or illustrations).

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Testing Strategy

### Unit Tests:

- Not applicable: the change contains no code.

### Integration Tests:

- Not applicable. The only automated checks are the structural `test`/`grep` commands in the Success Criteria (file presence, ID coverage, link, no delete action).

### Manual Testing Steps:

1. Read `context/foundation/ui-mockup/README.md` and check the screen and state list against roadmap F-02 and the PRD.
2. Open the Artifact from the index and walk E01 → E07 at phone width, then at desktop width.
3. Find each state from the States table on the canvas and check that it conveys the rule from the PRD.
4. Check that `mockup.dc.html` in the repo matches the last published version of the Artifact.

## Performance Considerations

Not applicable.

## Migration Notes

Not applicable. There is no data or existing UI.

## References

- Roadmap item: `context/foundation/roadmap.md` (F-02, Open Roadmap Question 5)
- PRD: `context/foundation/prd.md` (§Success Criteria Primary, US-01, US-02, FR-001…FR-014, §NFR, §Non-Goals)
- Linear: 10X-6

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Screen inventory and tokens

#### Automated

- [x] 1.1 The index exists — 998b9a5
- [x] 1.2 The index lists every screen and state ID — 998b9a5
- [x] 1.3 The index has a token section and the "starting point" rule — 998b9a5
- [x] 1.4 Roadmap question 5 points to the index — 998b9a5

#### Manual

- [x] 1.5 The owner confirms the screen and state list is complete for M-1 — 998b9a5
- [x] 1.6 The owner approves the copy glossary, the sample data and the tokens — 998b9a5

### Phase 2: Mockup canvas

#### Automated

- [ ] 2.1 The canvas source exists
- [ ] 2.2 The canvas has phone and desktop artboards for every screen
- [ ] 2.3 The canvas has every state
- [ ] 2.4 There is no delete action in the mockup
- [ ] 2.5 The index has the Artifact link and no placeholder

#### Manual

- [ ] 2.6 The owner walks the E01 → E07 flow on phone and desktop in the Artifact
- [ ] 2.7 The states read unambiguously
- [ ] 2.8 The owner confirms the mockup is low-fi and not polished
