# Whole-app UI mockup — Plan Brief

> Full plan: `context/changes/whole-app-ui-mockup/plan.md`

## What & Why

Roadmap F-02: a low-fidelity mockup of every screen and state of the M-1 primary flow, at phone and desktop widths. The owner wants to see the whole UI before building its parts, and slices S-01…S-06 take their screens from the mockup and refine them in their own plans. The main risk named in the roadmap is polishing the mockup and delaying S-04.

## Starting Point

`apps/web` has only the bare Astro placeholder. There is no layout, components or styles. The screen list comes from roadmap F-02 and the PRD. The PRD contradicts itself on onboarding; FR-003 (name only) is binding.

## Desired End State

`context/foundation/ui-mockup/` contains `README.md`, which holds:
- screens `E01`–`E07` and 6 states, with PRD refs and slices;
- English UI copy and sample data;
- a minimal token table;
- the Artifact link.

The same folder also contains `mockup.dc.html`, the source of the published `/design` canvas, with phone and desktop artboards. Open Roadmap Question 5 is closed.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) |
| --- | --- | --- |
| Location | Artifact + repo copy in `context/foundation/ui-mockup/` | Slices read it like other foundation docs, and it survives `/10x-archive` of this change. |
| UI language | English, with Czarek/Sonia sample data | Lesson: context and code files are English only; Polish UI text belongs only in future translation files. |
| States | Happy path + states slices need | Covers exactly what the roadmap says S-03/S-05/S-06 take from the mockup. |
| Fidelity | Low-fi + minimal visual direction | S-01 gets a starting point for its app shell. |
| Direction scope | One accent colour, neutrals, 3–4 step type scale, spacing; no logo, icons or dark mode | Enough for S-01, without fine-tuning details. |
| Upkeep (roadmap Q5) | Starting point only | Zero sync overhead under time pressure; slices refine in their own plans. |
| Onboarding | Name only | FR-003 overrides Success Criteria step 2 (species and date of birth were cut). |

## Scope

**In scope:**
- E01 sign-in/registration, E02 first animal, E03 add document, E04 document, E05 search, E06 animal profile, E07 animal list, with a shared app shell.
- States: reading / reading failed, empty result, progress over 2 s, "search unavailable", inactive animal.
- Phone and desktop artboards; English copy glossary; tokens.

**Out of scope:**
- Any code in `apps/web` / `services/api`.
- Validation and sign-in errors, upload limits, edit forms.
- Logo, icons, dark mode.
- Deletion, keyword search, generated health answers, sharing.
- Syncing the mockup after later slices.

## Architecture / Approach

Inventory first, then drawing. Phase 1 fixes the screens, states, copy and tokens in a text index; that is the checkpoint against polishing. Phase 2 turns the index 1:1 into `/design` artboards, named `<ID> · phone|desktop`, and publishes the Artifact. After the last canvas edit, it reads the Artifact again and saves its source in the repo. The IDs `E01`–`E07` are the shared key between the index, the canvas and slice plans.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Screen inventory and tokens | `ui-mockup/README.md` + roadmap Q5 closed | Missing a state that a slice expects |
| 2. Mockup canvas | Published Artifact + `mockup.dc.html` + link in the index | Polishing; the repo copy drifting from the last published version |

**Prerequisites:** access to `/design` (Artifacts) on the owner's account.
**Estimated effort:** about 1–2 sessions across 2 phases.

## Open Risks & Assumptions

- The tokens will probably change once S-01 builds the real shell; that's accepted.
- The mockup will drift from the app after a few slices; that's accepted by the "starting point only" decision.
- The automated checks are structural only (IDs, link, no "Delete"/"Remove" button); the quality of the screens is checked by manual review.

## Success Criteria (Summary)

- The owner can walk the E01 → E07 flow in the Artifact on phone and desktop and understand every screen and state without explanation.
- A slice plan can point to a screen by its ID and find its PRD refs and states in the index.
- The mockup shows the product's guardrails: the original is openable after a reading failure, there's no delete action, and there's no generated answer.
