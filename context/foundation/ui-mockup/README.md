# UI mockup — screen index (M-1)

Index of the F-02 mockup (`whole-app-ui-mockup`). Slices S-01…S-06 find here the list of screens and states, their source in the PRD, the slice that refines each one, and what the mockup deliberately does not decide.

## Status and rules

- The mockup is the **starting point** for M-1. Each slice refines its screens in its own plan; the mockup is **not updated** after the slices. It is a snapshot of the initial state, not a living specification.
- Mid-fi (owner decision, 2026-09-26): layout, content and the order of elements, with modern styling (generous radius, soft shadows, pill chips) and a line icon set. No logo, illustrations or dark mode; the visual direction stops at the token table and the iconography note below.
- All UI text is in English. Polish UI text belongs only in future translation files (see `context/foundation/lessons.md`). Sample data: Czarek and Sonia (see "Sample data").
- Screen IDs (`E01`–`E07`) and state IDs (`E04-reading` etc.) are the shared key between this index, the artboard names and references in slice plans.
- The mockup contains no action that deletes documents or animals (PRD §Non-Goals).

## Artifact

- Artifact link: https://claude.ai/code/artifact/fb24d112-22b3-45be-8ff8-c437abbc86ff (published 2026-09-26, restyled 2026-09-26)
- Canvas source in the repo: `mockup.dc.html` (in this folder)

## Screens

Every screen uses a shared app shell: a bottom tab bar on phone and a side navigation on desktop, both with the entries **Search / Add / Animals**. S-01 turns this into the real layout. E01 and E02 are shown before entering the shell (no navigation).

| ID | Screen | Contents | States | PRD refs | Slice |
| --- | --- | --- | --- | --- | --- |
| `E01` | Sign in / Register | Tabs or a toggle "Sign in" / "Create account"; "Email" and "Password" fields; a primary button matching the selected tab. | — | FR-001 | S-01 |
| `E02` | First animal | Onboarding with a single "Animal's name" field and a "Save and continue" button. No species or date-of-birth fields (FR-003 overrides Success Criteria step 2). | — | FR-003 | S-01 |
| `E03` | Add document | "Take photo" or "Choose PDF"; animal selector as chips (one tap), defaulting to the last-used animal; "Event date" defaulting to today, editable; "Save" without typing anything. | — | US-01, FR-006, FR-007, FR-008 | S-02 |
| `E04` | Document | Preview or "Open original"; the animal; "Event date" and "Uploaded on" as two separate, labelled fields; content status ("Content read"). | `E04-reading`, `E04-failed` | FR-009, US-01 | S-02 / S-03 |
| `E05` | Search | Query field with the example "e.g. Czarek's heart"; results as documents: title, animal name, matching fragment, "Open original"; single-animal filter (chips "All / Czarek / Sonia"). | `E05-empty`, `E05-searching`, `E05-unavailable` | US-02, FR-010, FR-011, FR-013 | S-04 / S-05 |
| `E06` | Animal profile | Name; the animal's documents by event date, newest first; "Edit"; "Mark as inactive". | `E06-inactive` | FR-005, FR-014 | S-06 |
| `E07` | Animals | List of active animals (Czarek, Sonia) with document counts; collapsed "Inactive" section; "Add animal". | — | FR-004, FR-005 | S-06 |

## States

| ID | Screen | State | PRD refs |
| --- | --- | --- | --- |
| `E04-reading` | `E04` Document | Content is being read: status "Reading content…"; the original can already be opened. | FR-009 |
| `E04-failed` | `E04` Document | Reading failed: message "We couldn't read the content. The original is safe."; "Open original" still works. | §Guardrails, FR-009 |
| `E05-empty` | `E05` Search | An empty result as a correct answer: "No documents found on this topic." No suggested alternative words. | prd.md Business Logic |
| `E05-searching` | `E05` Search | A search taking longer than 2 s: a visible, continuous progress indicator "Searching…". | §NFR |
| `E05-unavailable` | `E05` Search | Explicit notice "Search is temporarily unavailable. Please try again shortly." No fallback results of any kind. | FR-012 |
| `E06-inactive` | `E06` Animal profile | Profile of an inactive animal (Figa): "Inactive" label, "Mark as active" action; documents still visible and openable. | FR-005 |

## Sample data

"Today" in the mockup: **2026-09-26**.

Animals:

| Animal | Status | Notes |
| --- | --- | --- |
| Czarek | active | Last used — default chip in `E03`. |
| Sonia | active | — |
| Figa | inactive | Only for the "Inactive" section in `E07` and the `E06-inactive` state. |

Documents:

| Title | Animal | Event date | Uploaded on | Fragment | Used in |
| --- | --- | --- | --- | --- | --- |
| Cardiology discharge summary | Czarek | 2025-03-14 | 2026-09-20 | "…echocardiography revealed hypertrophic cardiomyopathy; grade II/6 systolic murmur…" | `E05` (result for "Czarek's heart"), `E06` |
| Cardiology follow-up | Czarek | 2026-09-26 | 2026-09-26 | — (content being read) | `E04-reading`, `E06` |
| Chest X-ray report | Czarek | 2024-11-02 | 2026-09-20 | — (unreadable photo) | `E04-failed` |
| Rabies vaccination | Sonia | 2026-05-08 | 2026-05-08 | "…rabies vaccine administered, next dose due in 12 months…" | `E04`, `E05` |
| Complete blood count | Sonia | 2026-02-11 | 2026-09-20 | "…haematocrit 32% (reference 30–45%), white cells within normal range…" | `E05` (result for "Sonia's blood counts") |
| Spay discharge summary | Figa | 2021-06-17 | 2026-09-20 | "…procedure uneventful, stitches out after 10 days…" | `E06-inactive` |

Several documents have an event date that differs from the upload date (e.g. Czarek's discharge summary: event 2025-03-14, uploaded 2026-09-20) — the case of digitising an old binder in one evening.

## Copy glossary

| Kind | Label | Where |
| --- | --- | --- |
| Navigation | Search · Add · Animals | app shell |
| Button | Sign in | `E01` |
| Button | Create account | `E01` |
| Field | Email · Password | `E01` |
| Field | Animal's name | `E02` |
| Button | Save and continue | `E02` |
| Button | Take photo · Choose PDF | `E03` |
| Field | Animal | `E03`, `E04` |
| Field | Event date | `E03`, `E04`, `E06` |
| Field | Uploaded on | `E04` |
| Button | Save | `E03` |
| Button | Open original | `E04`, `E05` |
| Field | Ask about your documents… (placeholder: "e.g. Czarek's heart") | `E05` |
| Filter | All · Czarek · Sonia | `E05` |
| Button | Edit | `E06` |
| Button | Mark as inactive | `E06` |
| Button | Mark as active | `E06-inactive` |
| Button | Add animal | `E07` |
| Section | Inactive | `E07` |
| Status | Content read | `E04` |
| Status | Reading content… | `E04-reading` |
| Status | We couldn't read the content. The original is safe. | `E04-failed` |
| Status | Inactive | `E06-inactive`, `E07` |
| Notice | Searching… | `E05-searching` |
| Notice | No documents found on this topic. | `E05-empty` |
| Notice | Search is temporarily unavailable. Please try again shortly. | `E05-unavailable` |

## Tokens

Owner palette (2026-09-26): four colours (terracotta, cream, sage, light sage) plus one warm dark and one warm muted text neutral. Contrast computed with the WCAG 2.x relative-luminance formula (script, not estimated). None of the four palette colours reaches 4.5:1 as text on cream, so all text uses the two neutrals; palette colours are used only for fills, borders and icon backgrounds, and never as the only cue.

| Token | Value | Use | Contrast |
| --- | --- | --- | --- |
| `color-bg` | `#FCF9EA` (cream) | Screen background, field fill | — |
| `color-surface` | `#DFE3D1` (light sage 35% over cream) | Cards, notices, navigation bars, status pills | — |
| `color-chip` | `#A8BBA3` (light sage) | Unselected chips, secondary buttons, auth toggle track, animal avatar | 1.93:1 on `color-bg` (fill; the label identifies the control) |
| `color-sage` | `#97A87A` (sage) | Icon badges (document fields, "Take photo" / "Choose PDF") | 2.42:1 on `color-bg` (decorative fill) |
| `color-accent` | `#DB9558` (terracotta) | Primary buttons, active nav item, selected chip, selected auth tab, progress fill | 2.35:1 on `color-bg`; never the only cue: selected chips add a check icon and bold text, active nav adds bold text |
| `color-text` | `#2B2620` (warm dark) | All body and heading text, text on accent/chip/sage fills, links ("Open original", underlined + external-link icon), icons | 14.19:1 on `color-bg`; 11.45:1 on `color-surface`; 6.03:1 on `color-accent`; 7.35:1 on `color-chip`; 5.85:1 on `color-sage` |
| `color-text-muted` | `#5A5046` (warm muted) | Field labels, dates, fragments, counts, placeholders; field and "Inactive" borders | 7.44:1 on `color-bg`; 6.01:1 on `color-surface` (never used as text on `color-chip`: 3.86:1) |
| `font-family` | system sans-serif (`system-ui, -apple-system, 'Segoe UI', sans-serif`) | Whole interface | — |
| `text-sm` | 14 px / 1.45 | Dates, field labels, statuses, nav labels | — |
| `text-base` | 16 px / 1.5 | Body text, fragments, form fields, buttons | — |
| `text-lg` | 20 px / 1.3, 600 | Document titles, section headings | — |
| `text-xl` | 28 px / 1.2, 700 | Screen title | — |
| `space-1` | 4 px | Inside chips, icon–text gap | — |
| `space-2` | 8 px | Between a label and its field | — |
| `space-3` | 16 px | Card padding, side margin on phone | — |
| `space-4` | 24 px | Between sections | — |
| `space-5` | 40 px | Layout margins on desktop | — |
| `radius-control` | 12 px | Fields, buttons, icon badges | — |
| `radius-card` | 16 px | Cards, notices, document preview | — |
| `radius-pill` | 999 px | Chips, search field, status pills, nav highlight, avatar | — |
| `shadow-card` | `0 1px 2px rgba(43, 38, 32, 0.06), 0 8px 24px rgba(43, 38, 32, 0.08)` | Cards, notices, search field | — |

Error and unavailable states have no colour of their own: they are communicated as text (`color-text`) with an alert icon on `color-surface`, to keep a single accent.

## Iconography

Inline SVG line icons drawn in the canvas (Lucide-style, 24 px grid, stroke 1.75, `currentColor`, `aria-hidden`); no external icon font or fetch. Icons always accompany a text label, never replace it. Set used: search, plus-circle, paw (nav: Search / Add / Animals; the animal avatar is a paw in a light-sage circle), camera ("Take photo"), file ("Choose PDF", document preview), calendar (event dates), upload ("Uploaded on"), external-link ("Open original"), check-circle ("Content read"), check (selected chip), loader (reading, searching), alert-circle (failed, unavailable), pencil ("Edit"), archive ("Mark as inactive", "Inactive" label), archive-restore ("Mark as active"), chevron-right (animal rows, collapsed "Inactive" section), mail and lock (E01 "Email" / "Password" fields).

## What the mockup does not decide

- No Astro components, styles or routing; no changes to `apps/web` or `services/api`.
- No pixel-perfect design, logo, illustrations or dark mode. Styling is mid-fi with a line icon set (owner decision, 2026-09-26); the visual direction stops at the token table and the iconography note.
- No full state catalogue: form validation errors, sign-in errors, upload size/format limits, the animal edit form, password reset and settings belong to the slices.
- No species or date-of-birth fields in onboarding (FR-003).
- No delete action anywhere (documents or animals). No keyword search and no generated sentence about the animal's health in results.
- No sharing, caretaker role or invitations (PRD Non-Goals).
- No mechanism to keep the mockup in sync with later slices — it is a snapshot of the M-1 starting point.
