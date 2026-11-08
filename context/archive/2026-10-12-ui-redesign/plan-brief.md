# UI Redesign — Plan Brief

> Full plan: `context/changes/ui-redesign/plan.md`
> Research: `context/changes/ui-redesign/research.md`

## What & Why

This change restyles the whole web app (`apps/web`) to match the new design in `context/changes/ui-redesign/design/`. That means a new palette, Bricolage Grotesque headings with Figtree body text, one SVG icon set, a floating phone bar and a dark green desktop sidebar, one message component, and quiet CSS motion.

The goal is a calmer, more trustworthy look that renders the same on every device. Unicode glyph icons look different depending on the system font, and that problem goes away.

## Starting Point

All styling lives in one `tokens.css`, using an older cream and terracotta palette and the system font.

- 13 Unicode glyphs act as icons.
- Errors are a red-tinted `.error` paragraph. `.notice` covers warnings, empty states and the reading panel.
- No form field is linked to its error message.
- The add-document form shows every error in one place, above Save.

## Desired End State

Every screen matches the design at 390 px and at 1280 px:

- sign-in and registration
- onboarding
- animals
- profile and history
- add document
- document with its reading status

All icons are inline SVG. Every message is an `Alert` (info, warning, error or success), placed next to what it concerns. Field errors are announced together with their field.

All text meets 4.5:1 contrast and every target is at least 44 px. Motion stops when the system asks for reduced motion. API calls, form logic and navigation flows are unchanged.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Fonts | `@fontsource-variable` packages imported in `BaseLayout` | Self-hosted and pinned by the lockfile; Astro's Fonts API would download at build time. | Research |
| Tokens | Keep existing variable names, add primary, border, accent and state tokens | Old screens keep working while each phase restyles them. | Plan |
| Contrast fixes | Placeholder `#5B635E`, field border `#868D88`, dark text on accent | The design's placeholder (3.78:1), border and white-on-accent (2.13:1) fail the rules. | Research |
| Originals | Single-page viewer with thumbnails, two columns on desktop | Matches the design; a 10-photo document no longer scrolls endlessly. | Plan (user) |
| History | Month-grouped timeline from `eventDate`, no API change | Easier to scan; grouping is presentation only. | Plan (user) |
| History tile | One generic document tile | `DocumentSummary` has no content type, and changing the API is out of scope. | Research |
| Sign-in copy | Add tagline and "only the owner sees originals" (EN + PL keys) | Completes the hero and states the privacy promise. | Plan (user) |
| Avatars | Initial tile in peach, mint or lavender, picked by a hash of the animal id | Colour stays stable when the list order or activity changes. | Plan (user) |
| Confirm inactive | Stays an inline card, styled like the design's dialog | A modal would change the interaction flow. | Plan |
| Capture errors | One error value, rendered in the files, animal, date or form zone | Errors land next to their field without touching validation logic. | Plan |
| Language switch | Unchanged component, restyled by CSS (inverse on the sign-in hero), 44 px links | Behaviour must stay identical, including keeping the query string. | Research |
| Breakpoint | Keep 768 px for shell, 1024 px for the document's two columns | Matches the existing shell contract. | Research |

## Scope

**In scope:**

- tokens, fonts, `Icon`, `Alert`, `FieldError` and `PageState`
- `AppShell` and language-switch styling
- the six screens
- the search placeholder restyle
- removing dead CSS
- a "superseded" note in `context/foundation/ui-mockup/README.md`

**Out of scope:**

- search functionality
- toasts
- API, validation and flow changes
- a modal confirmation
- dark mode
- new test tooling
- a favicon or logo file

## Architecture / Approach

`tokens.css` stays the only stylesheet, organised as tokens, then base, then primitives, then per-screen sections, then motion with a global reduced-motion block. Four small shared components (`Icon`, `Alert`/`FieldError`, `PageState`, `AnimalAvatar`) replace glyphs and the old message classes.

Each screen phase changes only markup, classes and where messages render. Refs, handlers, roles and the persistent live regions stay in place, so focus return and announcements keep working.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Foundation | Fonts, tokens, primitives, `Icon`, `Alert`, `FieldError` | Old screens look odd in the new palette until restyled |
| 2. Shell & entry screens | Floating bar, green sidebar, switch, sign-in/registration, onboarding, gate, placeholder | Language switch overlapping the shell header or sidebar |
| 3. Animals & profile | Rows/card grid, inactive section, profile hero, month timeline | Breaking focus return or conflict handling while moving buttons |
| 4. Add document | Source tiles, files card, chips, date field, zone errors, progress, saved state | Moving errors without changing which error is shown |
| 5. Document | Details tiles, stored pill, 4-state reading panel, thumbnail viewer | The viewer hides other pages from assistive tech or breaks per-page actions |
| 6. Cleanup & review | Dead CSS removed, glyph and class grep gates, docs note, full review | Missed screen states at one width |

**Prerequisites:**

- Run `npm install` in `apps/web` to add the two font packages.
- A local API and web stack is needed for the manual review (`docs/local-development.md`).

**Estimated effort:** about 4–6 implementation sessions across 6 phases, each ending with `npm run check` and `npm run build` plus a manual review at both widths.

## Open Risks & Assumptions

- Visual verification is manual only, because the repo has no frontend test or screenshot tooling.
- The English translations of the new sign-in lines are a proposal and need the owner's approval along with the PL strings.
- `body:has()` is assumed to be available in the target browsers (current Chrome, Safari and Firefox).

## Success Criteria (Summary)

- Every screen at 390 px and 1280 px matches the design, with no Unicode glyph icons and no Google Fonts requests.
- Each error appears next to the field or control it concerns and is announced with it. Warnings, info and success messages each have their own distinct variant.
- `npm run check` and `npm run build` pass, the smoke-test marker is intact, and behaviour (forms, uploads, reading status, language switch) is unchanged.
