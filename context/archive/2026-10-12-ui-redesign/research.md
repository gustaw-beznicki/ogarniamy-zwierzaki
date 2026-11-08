---
date: 2026-10-12T22:22:12+02:00
researcher: Claude (Opus 5.5) for Gustaw Beźnicki
git_commit: d92831d0491f261bf66d0c0a3ec9fd8f3155a213
branch: ui-redesign
repository: ogarniamy-zwierzaki (worktree 10xdevs-ui-redesign)
topic: "Visual redesign of apps/web from the design reference in context/changes/ui-redesign/design/"
tags: [research, apps-web, ui, design-tokens, icons, alert, app-shell, accessibility, fonts]
status: complete
last_updated: 2026-10-12
last_updated_by: Claude (Opus 5.5)
---

# Research: Visual redesign of apps/web

**Date**: 2026-10-12T22:22:12+02:00
**Researcher**: Claude (Opus 5.5) for Gustaw Beźnicki
**Git Commit**: d92831d (the `design/` folder and this change folder are untracked)
**Branch**: ui-redesign
**Repository**: ogarniamy-zwierzaki, worktree `/home/gustawb/code/10xdevs-ui-redesign`

## Research Question

How does the current `apps/web` frontend build its UI, and what has to change to match the design reference in `context/changes/ui-redesign/design/`? The owner's brief sets the scope:

- Tokens: the owner's palette, keeping the existing variable names where possible.
- Fonts: Bricolage Grotesque and Figtree, self-hosted.
- An SVG `Icon` component that replaces every Unicode icon.
- `AppShell`: a floating bottom bar on phones and a dark green sidebar on desktop.
- The six screens: sign-in, onboarding, animals, profile, add, document.
- One `Alert` component (info, warning, error, success) replacing `.error` and `.notice`, with field errors linked through `aria-describedby`.
- Motion in CSS only, switched off by `prefers-reduced-motion`.

Constraints: no API, form-logic or flow changes. All copy goes through `pl.ts` and `en.ts`. Contrast at least 4.5:1, touch targets at least 44 px, working at 390 px and 1280 px.

## Summary

- **One stylesheet holds all styling, so the redesign is mostly CSS plus small JSX changes.**
  - `apps/web/src/styles/tokens.css` (167 lines) is the only stylesheet. It is imported once in `layouts/BaseLayout.astro:2`. No component has inline styles or `<style>` blocks.
  - The current tokens are colours, three radii and one shadow (`tokens.css:1-22`). Everything else is hard-coded px.
- **Icons.** 13 distinct Unicode glyphs act as icons in 8 components (inventory below).
  - The ⓘ error prefix alone appears at 13 call sites.
  - The design's icon set (`design/Main.dc.html:107-130`, 24 entries) covers every glyph. It needs six extra glyphs that appear only in the screen files: three chevrons, an arrow-right, an upload arrow and an info circle.
- **Messages.**
  - There are 13 `<p className="error" role="alert">` call sites.
  - There are 8 `.notice` uses, plus a `stored-notice`, a `profile-status` live region and the `.notice.reading-status` panel.
  - No input in the app has `aria-describedby`. The only `aria-invalid` is on the animal-name field (`AnimalNameForm.tsx:50`).
  - `DocumentCaptureForm` sends every error, including field-level ones, to a single message above the Save button (`DocumentCaptureForm.tsx:465`).
- **Fonts.**
  - `@fontsource-variable/bricolage-grotesque@5.3.0` and `@fontsource-variable/figtree@5.3.0` (OFL-1.1, published 2026-07-19) ship `latin-ext` subsets, which cover Polish diacritics.
  - Importing them in `BaseLayout` self-hosts them through Vite, with no request to Google.
  - No CSP or `staticwebapp.config.json` exists that could block them.
- **Contrast.** Every text pair in the owner's palette passes 4.5:1, with these exceptions:
  - White on the accent `#F29E6B` is 2.13:1, so accent fills must carry dark text, as the design does.
  - The design's placeholder `#7A827D` is 3.78:1.
  - The input and card borders (`#D9DDD6`, `#E3E1D8`) fall below the 3:1 non-text guideline (1.31:1 measured for `#E3E1D8`).
- **The brief overrides two earlier owner decisions** recorded in `context/foundation/ui-mockup/README.md`:
  - "Single accent, errors have no colour of their own" (`:127`).
  - The system-ui font (`:112`).
  - That README's token table (`:102-125`) will be stale after this change.
- **Some design elements need data the API does not expose**, so they cannot be built without an API change, which is out of scope:
  - The PDF-vs-photos icon and "PDF" label on history rows.
  - "Data dodania" (uploaded date) on history rows is possible, because `DocumentSummary.uploadedAt` exists.
  - The single-page viewer with thumbnails is a presentational change to `DocumentDetails`, not a data gap.

## Detailed Findings

### 1. Styling sources and tokens

- The only stylesheet is `tokens.css`, imported at `BaseLayout.astro:2`. `public/` holds only `favicon.svg` (the default Astro logo) and `favicon.ico`.
- Current tokens (`tokens.css:8-21`):
  - Colours: `--color-bg` `#FCF9EA`, `--color-surface` `#DFE3D1`, `--color-chip` `#A8BBA3`, `--color-sage` `#97A87A`, `--color-accent` `#DB9558`, `--color-text` `#2B2620`, `--color-text-muted` `#5A5046`, and `--color-danger` / `-bg` / `-text`.
  - Radii: `--radius-control` 12, `--radius-card` 16, `--radius-pill` 999.
  - One shadow: `--shadow-card`.
- Existing names map onto the owner's palette as follows:
  - `--color-bg` → `#F7F5EF`.
  - `--color-surface` → `#FFFFFF` (cards).
  - `--color-text` → `#1A1F1C`.
  - `--color-text-muted` → `#5B635E`.
  - `--color-accent` → `#F29E6B`.
  - `--color-danger*` → the error trio `#B3261E` / `#FBE6E3` / `#8C1D16`.
- `--color-chip` and `--color-sage` have no direct counterpart. The design uses `#EEF1EC` / `#F3F2EC` for neutral control fills and `#DCE8E1` (mint) for icon bubbles.
- New names are needed for:
  - Primary `#1F4D3F`, its hover `#173B30` and its active background `#DCE8E1`.
  - Border `#E3E1D8`.
  - Accent background `#FDE7D8` and accent text `#8A4317`.
  - Info, warning and success trios.
- `tokens.css` has no `--color-success` or `--color-warning`.
- `rgba(...)` values hard-coded outside the tokens: `tokens.css:55`, `:158`, `:167`.
- `BaseLayout.astro:14` hard-codes `<meta name="theme-color" content="#FCF9EA">`. It must follow the new background.
- `BaseLayout.astro:8` sets `data-app="ogarniamy-zwierzaki"`, and `scripts/smoke.sh:41-45` greps `/signin/` and `/en/signin/` for that marker after deploy. The marker must stay.
- Every class used in TSX is defined in `tokens.css` except three:
  - `animal-name-form` (`AnimalNameForm.tsx:41`)
  - `chip-group` (`DocumentCaptureForm.tsx:445`)
  - `replace-notice` (`DocumentCaptureForm.tsx:419`)

  `tokens.css` has no unused selectors (subagent script cross-check).

### 2. Fonts (external research)

- npm registry, checked on 2026-10-12:
  - `@fontsource-variable/bricolage-grotesque` 5.3.0 and `@fontsource-variable/figtree` 5.3.0, both OFL-1.1 and published 2026-07-19.
  - Bricolage ships `index.css`, `opsz.css`, `standard.css`, `wdth.css` and `wght.css`.
  - Figtree ships `index.css`, `wght.css` and `wght-italic.css`.
- `index.css` declares one `@font-face` per subset, including `latin-ext`, which covers U+0100-02BA and so the Polish letters ą ć ę ł ń ś ź ż.
  - Bricolage: weight 200–800.
  - Figtree: weight 300–900.
  - Both use `font-display: swap`.
  - The family names are `'Bricolage Grotesque Variable'` and `'Figtree Variable'` (Fontsource v5 naming, confirmed in Context7 `/websites/fontsource`, "Update Variable Font Family Names").
- Unpacked package sizes: about 544 KB for Bricolage and 81 KB for Figtree. The browser downloads only the woff2 subsets whose `unicode-range` matches the page.
- Integration options (Context7 `/withastro/docs`, fonts guide):
  - Importing the Fontsource package CSS in a shared layout's frontmatter self-hosts the files.
  - Astro's Fonts API with `fontProviders.fontsource()` also self-hosts, but downloads the font at build time. That adds a network dependency to the build that a lockfile-pinned npm package does not have.
- Design font stacks:
  - Display: `'Bricolage Grotesque','Avenir Next',system-ui,sans-serif`, with `letter-spacing:-0.02em` (`design/Main.dc.html:16`).
  - Body: `Figtree,'Segoe UI',system-ui,sans-serif` (`Main.dc.html:46`).
- Type scale (`Main.dc.html:93-96`): display 52/800, H1 32/700, H2 20/700, body 16/400.
  - The mobile screens use larger H1 sizes: 38–40 px/800 (`Mobile-Animals`, `Mobile-Add`, `Mobile-Document`) and `clamp(40px,5vw,64px)` on desktop.
- No CSP restricts the change: there is no `staticwebapp.config.json`, and `infra/modules/static-web-app.bicep` defines no headers. The deploy uploads `apps/web/dist` with `skip_app_build: true` (`.github/workflows/deploy.yml:158-165`).

### 3. Icons

**Unicode glyphs currently used as icons** (subagent inventory; anchors spot-checked against the components read in full):

| Glyph | Where | Purpose | Design replacement |
| --- | --- | --- | --- |
| ⌕ | `AppShell.tsx:15` (rendered `:31`, `:35`) | Search nav | search |
| `+` | `AppShell.tsx:16` | Add nav | plus |
| ♧ | `AppShell.tsx:17`, `AnimalsList.tsx:23` | Animals nav, row avatar | paw (the design replaces the row avatar with an initial letter tile, `Mobile-Animals.dc.html:58`) |
| ↪ | `AppShell.tsx:35` | Mobile sign-out (the glyph is the whole label, with `aria-label`) | logout |
| ⓘ | 13 sites: `AppShell:34`, `AuthForm:37`, `AnimalNameForm:53`, `SessionGate:43`, `AnimalsList:128`, `DocumentDetails:50`, `DocumentReadingStatus:168`, `AnimalDocuments:193,200,268`, `DocumentCaptureForm:384,389,465` | Error prefix | the Alert's error glyph |
| › | `AnimalsList.tsx:26`, `AnimalDocuments.tsx:265` | Row chevron | chevron-right `m9 6 6 6-6 6` |
| ‹ | `AnimalDocuments.tsx:189`, `DocumentDetails.tsx:50,56` | Back link | chevron-left `m15 6-6 6 6 6` |
| ▤ | `AnimalDocuments.tsx:263`, `DocumentDetails.tsx:70`, `DocumentCaptureForm.tsx:412,433` | Document / PDF | document, pdf |
| ✓ | `DocumentDetails.tsx:64`, `DocumentCaptureForm.tsx:449` | Stored notice, selected chip | shield-check, check |
| ◉ | `DocumentCaptureForm.tsx:410` | Take photo | camera |
| ＋ | `DocumentCaptureForm.tsx:411` | Add photos | photos |
| ↑ ↓ ✕ | `DocumentCaptureForm.tsx:437-440` | Move up, move down, remove (button text with `aria-label`) | arrow-up, arrow-down, x |

Notes on this inventory:

- `·` at `DocumentDetails.tsx:81`, `AnimalDocuments.tsx:264` and `DocumentCaptureForm.tsx:428` is a text separator, not an icon.
- `i18n/*.ts` contain only typographic characters (… — „ ”).

**Icon set in `design/Main.dc.html:107-130`.** It has 24 glyphs: search, plus, paw, document, camera, photos, pdf, ocr, shield-check, clock, check, alert-circle, refresh, calendar, external, download, pencil, archive, arrow-up, arrow-down, x, mail, lock and logout.

- Spec: viewBox 24, stroke 1.75, round caps and joins, `fill:none`, `currentColor` (`Main.dc.html:15`).
- The paw is a filled glyph, used for the brand mark (`Main.dc.html:51`). The `Main.dc.html:109` variant is the stroked one, for the nav.

**Extra glyphs used in the screen files but absent from Main:**

- chevron-right `m9 6 6 6-6 6` (`Mobile-Animals.dc.html:60`)
- chevron-left `m15 6-6 6 6 6` (`Mobile-Profile.dc.html`, back link)
- chevron-down `m6 9 6 6 6-6` (inactive `<summary>`, "Show more")
- arrow-right `M5 12h14M13 6l6 6-6 6` (sign-in CTA, desktop animal card)
- upload `M12 20V9M7 14l5-5 5 5M5 4h14` ("Data dodania" field)
- info-circle `circle r9 + M12 11v5M12 8h.01` (capture-limits hint)

**Alert badge glyphs** (`Messages.dc.html:18,31,44,57`) are drawn at stroke 2.25 in white on the variant colour:

- info `M12 11v6M12 7h.01`
- warning `M12 7v6M12 17h.01`
- error `M7 7l10 10M17 7 7 17` (square badge, radius 9)
- success `m6 12.5 4 4L18 8.5`

**Earlier icon rule** (`context/foundation/ui-mockup/README.md:131`): inline SVG, Lucide-style, `aria-hidden`, no icon font or fetch, and icons never replace a text label. The brief's Icon spec matches it.

Two current controls break the "never replace a label" rule and keep only an `aria-label`:

- The mobile sign-out tab (`AppShell.tsx:35`).
- The move and remove icon buttons in capture.

The design keeps both as icon-only with an `aria-label` (`Mobile-Animals.dc.html:98`, `Mobile-Add.dc.html` file rows).

### 4. Messages: current usage and the design's Alert

**Current `.error`** (all `<p className="error" role="alert">` with an ⓘ prefix):

- Page-level:
  - `SessionGate.tsx:43`
  - `AnimalDocuments.tsx:193`
  - `DocumentDetails.tsx:50`
  - `DocumentCaptureForm.tsx:384`
- Session expired:
  - `DocumentCaptureForm.tsx:389`
- Form-level:
  - `AuthForm.tsx:37`
  - `DocumentCaptureForm.tsx:465`
- Field-adjacent:
  - `AnimalNameForm.tsx:53`
- Inline:
  - `AnimalsList.tsx:128` (inside `.list-error` with a Retry button)
  - `AnimalDocuments.tsx:200` (rendered at `:223` and `:249`)
  - `AnimalDocuments.tsx:268`
  - `DocumentReadingStatus.tsx:168`
- Shell:
  - `AppShell.tsx:34` (`.shell-error`; sign-out failure shown at the top of the main area, away from the sign-out control).

**Current `.notice`:**

- Warnings with a Refresh action and `role="alert"`:
  - `AnimalsList.tsx:120` (createUncertain)
  - `AnimalDocuments.tsx:196` (`conflictNotice`, rendered at `:222`, `:240` and `:248`)
- Info or empty states with no role:
  - `AnimalsList.tsx:133-134`
  - `AnimalDocuments.tsx:212` (inactive profile)
  - `AnimalDocuments.tsx:259` (no documents)
- Confirmation prompt, `role="group"`:
  - `DocumentCaptureForm.tsx:419`
- Success with no background or role:
  - `DocumentDetails.tsx:64` (`stored-notice`)
- Success and info live region, empty when there is no message:
  - `AnimalDocuments.tsx:213` (`profile-status`, `role="status"`)
- Reading panel, `section.notice.reading-status`:
  - `DocumentReadingStatus.tsx:184`
  - Its inner states are `role="status"` paragraphs (`:148`, `:153`, `:160`), and `accountChanged` is `role="alert"` (`:150`).

**Design Alert anatomy** (`design/Messages.dc.html:14-62`):

- Container:
  - Padding 16, radius 18, background `bg`, 1 px border (info `#C9D8EA`, warning `#F1DCA6`, error `#F2C7C1`, success `#C2E0D1`), text colour `text`.
- Icon badge:
  - 32 px, circular for info, warning and success, square radius 9 for error, filled with the `icon` colour.
- Message text: 15/1.45/600.
- Optional action button:
  - Indented 44 px, min-height 40 px, `icon` colour fill (warning uses its text colour `#5E4108`), white text.
  - 40 px is below the brief's 44 px rule. `Mobile-Messages.dc.html` uses 44 px for the same button.
- Compact variant: padding 10/12, radius 14, 24 px badge, 14 px text (`:23`, `:36`, `:49`, `:61`).
- Roles in the design:
  - info, warning and success: `role="status"`.
  - error: `role="alert"`.
- Design rule (`Messages.dc.html:70`): errors are never toasts. They stay in the content next to what they concern until the problem goes away.

**Field error** (`Messages.dc.html:103-117`):

- The field wrapper gets a 2 px `#B3261E` border and a `#FFFAF9` background.
- The input gets `aria-invalid="true"` and `aria-describedby="<id>"`.
- The message below the field: a 16 px alert-circle icon plus 14/600 text in `#8C1D16`.

**Confirmation** (`Messages.dc.html:121-133`) is drawn as a modal dialog (`role="dialog"`, `aria-modal`). The current confirm-inactive is an inline card (`AnimalDocuments.tsx:245-255`, `role="group"`, `autoFocus`) with focus return through `focusLater` (`:81-84`). A modal would change the interaction flow. Restyling the inline card in the dialog's visual language keeps the behaviour.

**Page-level error** (`Messages.dc.html:136-148`): an illustration, an H3 with `role="alert"`, and a primary "back" link.

### 5. Field errors and `aria-describedby`

- `AuthForm.tsx:35-36`: email and password have no `aria-invalid` or `aria-describedby`.
  - The form is not `noValidate`, so the browser's native validation is the only field-level feedback.
  - Server errors such as `invalid_email` and `password_too_short` show as a form-level message (`:37`).
  - Codes that belong to a field: `invalid_email`, `email_taken` (email) and `password_too_short` (password). The code-to-message lookup is at `:23`.
- `AnimalNameForm.tsx:42-53`:
  - `aria-invalid` is set only for client validation.
  - Caller errors (`error` prop) are not marked invalid.
  - The message sits directly under the only field but is not linked to it.
  - Used by `FirstAnimalForm.tsx:23`, `AnimalsList.tsx:108` and `AnimalDocuments.tsx:228`. A single component change covers onboarding, add-animal and rename.
- `DocumentCaptureForm.tsx`: the codes from `validate()` (`:245-251`) and `addFiles` (`:165-213`) map to fields:
  - `files_required` and every selection error from `checkFiles` (`:71-95`) → files.
  - `animal_required` and `animal_inactive` → animal.
  - `invalid_event_date` and `future_event_date` → date.
  - All of them render at `:465`.
  - Choosing where a message renders from `errorCode` is presentation only: `errorCode` stays one state value and no validation or flow changes.
- `id` generation: React 19 `useId()` is available (React `^19.3.0`, `apps/web/package.json`). `AnimalNameForm` already accepts an optional form `id` (`:15`).

### 6. AppShell and language switch

- Current markup (`AppShell.tsx:29-36`):
  - `aside.sidebar` with brand, nav and sign-out.
  - `div.main-area`.
  - Fixed `nav.tab-bar` with 3 links and an icon-only sign-out button.
- Breakpoint 768 px (`tokens.css:149-162`): 232 px light sidebar on desktop, tab bar below.
- Design, mobile (`Mobile-Animals.dc.html:94-100`):
  - Floating `nav` at left/right/bottom 16, padding 6, radius 28, white, large shadow.
  - Grid `repeat(3,1fr) 60px`.
  - Tabs: min-height 56, radius 20, 12 px labels. Active tab: `#1F4D3F` fill, white text, `tabIn` scale animation.
  - The Add tab carries a 28 px accent circle around its icon.
  - Page content gets `padding-bottom: 120px`.
  - The real app needs `env(safe-area-inset-bottom)` added to the bar's bottom offset. The current bar uses it at `tokens.css:55`.
- Design, desktop (`Desktop-Animals.dc.html:48-63`):
  - Sidebar `#1F4D3F`, padding 28/18.
  - Brand: 44 px accent tile with a filled paw, plus a two-line display wordmark.
  - Nav links: 48 px, radius 16, `#E3EDE7`. Active: accent fill with `#1A1F1C` text.
  - Sign-out pinned to the bottom, in `#CFE0D7`.
  - Main area: `padding: 28px clamp(20px,4vw,56px) 56px`, `max-width: 1040px`.
  - Contrast: `#E3EDE7` on `#1F4D3F` is 8.00:1, `#CFE0D7` on `#1F4D3F` is 6.98:1, `#1A1F1C` on `#F29E6B` is 7.86:1.
- Language switch:
  - Rendered globally in `BaseLayout.astro:17`, outside AppShell, `position:absolute` top-right (`tokens.css:164-167`). It also covers sign-in and onboarding.
  - `LanguageSwitch.tsx` keeps the query string after hydration (`:18-19`) and swaps the `/en` prefix (`:10-13`). Earlier plans require locale switching to keep the resource being viewed (`context/archive/2026-10-04-capture-document-original/plan.md:255`, `context/changes/animal-profiles-and-history/plan.md:191`).
  - The design places it:
    - Mobile app pages: top-right in a header row, alongside the brand tile or back link.
    - Desktop: top-right of the main column.
    - Sign-in: over the green hero, with a translucent track.
  - Its links are about 28 px tall (`tokens.css:165`; design `padding: 7px 10px`, 13 px). The 44 px rule needs a taller hit area: min-height 44 on the link, or padding with a negative margin.
- Hit areas below 44 px today (CSS estimates, not rendered):
  - `.language-switch a`
  - `.auth-tabs button` (about 39 px)
  - `.back-link` (about 20 px)
  - `.brand`
  - `.sign-out` (about 43 px)

### 7. Screens: design versus current components

**Sign-in.** `Mobile-Signin.dc.html` vs `AuthForm.tsx`:

- Green hero (360 px, radius 0 0 40 40) with drifting paw marks.
- Brand tile 64 px plus a 38 px wordmark.
- New tagline: "Prywatne archiwum dokumentów weterynaryjnych" (`:218`).
- White card overlapping the hero (radius 28), segmented tabs with a sliding accent indicator (`.seg`, transform 0 / 100%).
- Fields with a leading mail or lock icon inside a 52 px wrapper. Focus ring `0 0 0 4px #DCE8E1` on a `#1F4D3F` border.
- Primary 54 px button with an arrow icon.
- New footer line: "Oryginały widzi tylko właściciel konta" (`:249`), with a shield-check icon.
- The two lines (`:218`, `:249`) are new copy that needs `en.ts` and `pl.ts` keys.

**Onboarding.** No design file. `FirstAnimalForm.tsx` should reuse the sign-in card language and the shared field and button styles.

**Animal list.**

- Mobile (`Mobile-Animals.dc.html`):
  - Header row: brand tile plus the language switch.
  - H1 40/800 and a pill "Add animal" button (white, mint icon bubble).
  - Rows: white, radius 24, 56 px initial tile, name 18/700, count 14 muted, 40 px chevron bubble.
  - Inactive section: `<details>` with an archive icon and a rotating chevron. Inactive rows have a dashed border and a grey tile and pill.
- Desktop (`Desktop-Animals.dc.html:66-104`):
  - Card grid `repeat(auto-fit,minmax(min(280px,100%),1fr))`, 72 px tile, 28 px display name, arrow bubble.
- Tile colours cycle through peach `#FDE7D8`/`#8A4317`, mint `#DCE8E1`/`#1F4D3F` and lavender `#E9E4F2`/`#4A3A6B`. Lavender is not in the brief's palette. Contrast is 7.99:1.
- Current component: `AnimalsList.tsx:21-28` (row) and `:101-140`.
- The add-animal form opens inline (`:104-124`). The design does not show it.

**Profile and history.** `Mobile-Profile.dc.html`, `Mobile-Messages.dc.html` vs `AnimalDocuments.tsx`:

- Green hero card with avatar, name, status pill (green dot), document count, and glass buttons "Zmień imię" / "Oznacz jako nieaktywnego".
- Inactive state (`Mobile-Messages.dc.html:41-63`): grey hero `#ECEBE5` and a warning Alert holding the "Oznacz jako aktywnego" action.
- Document count: `Animal.storedDocumentCount` exists (`lib/api.ts:13`).
- History is a timeline:
  - 2 px rail with dots.
  - Month group headings (uppercase 12/700).
  - Rows with a 50×60 type tile, event date 17/700, file count, and "Data dodania: dd.mm.yyyy".
- Data available for history rows:
  - `DocumentSummary` has `eventDate`, `uploadedAt` and `fileCount` (`lib/api.ts:78-84`).
  - It has no content type, so the photo-vs-PDF tile and the "PDF" label cannot be told apart without an API change.
  - Month grouping can be derived client-side from `eventDate`. It is presentational.
- "Add document" moves from the header (`AnimalDocuments.tsx:210`) to the history header as an accent button.
- "Show more": white 48 px button with a chevron-down.
- Current copy reused: `profileTitle`, `documentsOrder`, `uploadedOn`.

**Add document.** `Mobile-Add.dc.html` vs `DocumentCaptureForm.tsx`:

- Three source tiles (min-height 116, radius 22). The first is primary green, the others white with mint or peach icon bubbles.
- Limits hint with an info icon.
- "Selected files" white card. The design uses a decorative skeleton thumbnail; the app has real previews (`:432`).
- 44 px icon buttons. The remove button has error-coloured text.
- Animal chips:
  - In the design they are `button[aria-pressed]`.
  - In the app they are visually hidden radios inside labels (`:447-449`). Keeping the radios keeps the logic.
- Date field: 52 px wrapper with a calendar icon.
- Save: 56 px primary.
- Progress: card with a 10 px bar and a shimmer; the bar is green on mint.
- Done state: success tile with a pop and draw check (`:123-128`).
- The current `<progress>` element (`:461`) carries real values. A styled `<progress>`, or a div bar with `role="progressbar"` and values, keeps the semantics.

**Document.** `Mobile-Document.dc.html`, `Desktop-Document.dc.html` vs `DocumentDetails.tsx` and `DocumentReadingStatus.tsx`:

- Eyebrow is the animal name, H1 is "Dokument". The current code does the reverse (`DocumentDetails.tsx:57-58`).
- Two `dl` tiles: event date with calendar icon, uploaded date with upload icon. The animal field is omitted in the design; the current code shows it (`:60`).
- Stored notice: a success pill (`#E2F1EA` with `#1F6E52` text, 5.28:1).
- Reading panel (white card) by state:
  - queued: grey clock tile with a breathe animation.
  - reading: peach OCR tile with a pulse and an indeterminate bar.
  - read: green check with pop and draw, plus a timestamp.
  - failed: red alert tile, error text, a detail line, and a secondary Retry button whose icon rotates on hover.
- Originals viewer: one large page preview (with a scan line while reading), thumbnail buttons and an Open / Download pair. On desktop it is a two-column layout: viewer 3fr, details 2fr.
  - The current code lists every page with its own open and download links (`DocumentDetails.tsx:76-89`) and gives each image a descriptive `alt` (`:80`).
  - A thumbnail switcher would add local UI state but no data or flow change.

**Search.** `search.astro` and `en/search.astro` render `Placeholder` (`Placeholder.tsx:3-5`). Out of scope beyond restyling the card.

### 8. Motion

- No `prefers-reduced-motion` exists anywhere in the repo today (subagent search of code and context).
- Every design file ends with `@media (prefers-reduced-motion: reduce){*{animation:none!important;transition:none!important}}`. The motion-bearing files add overrides that put final states back, for example `.draw{stroke-dashoffset:0}` and `.bar{width:100%}` (`Mobile-Add.dc.html` style block).
- Motion spec (`Main.dc.html:156-207`):

  | Motion | Timing |
  | --- | --- |
  | List entrance | 420 ms ease-out, 60 ms stagger |
  | Tab indicator | 280 ms, `cubic-bezier(.34,1.3,.5,1)` |
  | Reading pulse | 1.6 s loop, in-progress states only |
  | Saved | pop 300 ms, then check draw 420 ms |
  | Upload shimmer | 1.2 s |
  | OCR scan line | 2.4 s |

- Other motion in the screen files:
  - Error shake (`Mobile-Document.dc.html`, 0.45 s, once).
  - Field-error drop-in (0.3 s).
  - Card hover lift.
  - Paw drift on sign-in.
- All of it is pure CSS keyframes and transitions, so no JS animation library is needed.

### 9. Contrast (WCAG 2.x relative luminance, computed by script)

| Pair | Ratio |
| --- | --- |
| text `#1A1F1C` on bg `#F7F5EF` / card `#FFFFFF` | 15.33 / 16.71 |
| muted `#5B635E` on bg / card / `#ECEBE5` | 5.68 / 6.19 / 5.18 |
| white on primary `#1F4D3F` / hover `#173B30` | 9.58 / 12.32 |
| primary on mint `#DCE8E1` / on white / on bg | 7.61 / 9.58 / 8.79 |
| text on accent `#F29E6B` | 7.86 |
| **white on accent** | **2.13 (fails)** |
| accent text `#8A4317` on `#FDE7D8` | 6.08 |
| info / warning / error / success text on bg | 9.92 / 8.53 / 7.61 / 8.12 |
| success icon colour `#1F6E52` as text on `#E2F1EA` | 5.28 |
| white on info / warning-text / error / success buttons | 6.88 / 9.39 / 6.54 / 6.16 |
| sidebar `#E3EDE7` / `#CFE0D7` on primary | 8.00 / 6.98 |
| label `#3A413D` on white | 10.48 |
| **placeholder `#7A827D` on `#FBFAF7`** | **3.78 (fails 4.5)** |
| lavender `#4A3A6B` on `#E9E4F2` | 7.99 |
| border `#E3E1D8` on white (non-text) | 1.31 |
| warning icon `#B7791F` on `#FFF3D9` (non-text) | 3.31 |

## Code References

- `apps/web/src/styles/tokens.css:1-167`: all styling. Tokens `:1-22`, focus `:40`, shell `:51-56`, desktop `:149-162`, language switch `:164-167`.
- `apps/web/src/layouts/BaseLayout.astro:2,8,14,17`: CSS import, `data-app` marker, `theme-color`, global `LanguageSwitch`.
- `apps/web/src/components/AppShell.tsx:14-36`: nav model and markup.
- `apps/web/src/components/LanguageSwitch.tsx:10-29`: path swap, query preservation, `aria-current`.
- `apps/web/src/components/AuthForm.tsx:29-40`: tabs, fields, form-level error.
- `apps/web/src/components/AnimalNameForm.tsx:40-59`: shared name form, `aria-invalid`, error.
- `apps/web/src/components/AnimalsList.tsx:21-28,101-140`: row, list, add form, inactive `<details>`.
- `apps/web/src/components/AnimalDocuments.tsx:186-270`: profile header, notices, edit and confirm cards, history list.
- `apps/web/src/components/DocumentCaptureForm.tsx:383-474`: capture UI, progress, single error slot `:465`.
- `apps/web/src/components/DocumentDetails.tsx:47-89`: details, stored notice, originals.
- `apps/web/src/components/DocumentReadingStatus.tsx:139-188`: reading panel body per view and state.
- `apps/web/src/lib/api.ts:8-14,69-84`: `Animal`, `StoredDocument`, `DocumentSummary` shapes.
- `context/changes/ui-redesign/design/Main.dc.html:15,46,107-130,156-207`: icon spec, fonts, icon set, motion.
- `context/changes/ui-redesign/design/Messages.dc.html:14-62,103-148`: Alert variants, field error, confirmation, page error.
- `scripts/smoke.sh:41-45`: `data-app` marker check.
- `.github/workflows/ci.yml:13-32`: web CI is `npm ci`, `npm run check`, `npm run build`.

## Architecture Insights

- Pages are thin Astro wrappers: `BaseLayout` › `SessionGate` › (`AppShell` ›) the feature component, all `client:load` (for example `pages/animals.astro:10`). Every component change applies to both locales automatically.
- Copy rules:
  - `en.ts` is the source of `Messages`, and `pl.ts` `satisfies Messages` (`i18n/pl.ts:1,180`), so a missing key fails `astro check`.
  - Polish appears only in `pl.ts` (`context/foundation/lessons.md:12-17`).
- Accessibility conventions to preserve:
  - 3 px focus outline (`tokens.css:40`).
  - Focus return with `requestAnimationFrame` (`AnimalsList.tsx:64-70`, `AnimalDocuments.tsx:81-84`).
  - Errors use `role="alert"`; loading and progress use `role="status"`.
  - Decorative glyphs are `aria-hidden`.
  - Accessible names on icon-only buttons.
- Frontend verification is limited to `astro check` and `astro build`. There is no frontend test framework, lint or Playwright config (`docs/testing.md:6-7,30`). Visual review at 390 and 1280 px has to be manual. Chromium headless shells are cached locally under `~/.cache/ms-playwright/`, which allows screenshot review without adding a dependency.

## Historical Context (from prior changes)

- `context/archive/2026-09-26-whole-app-ui-mockup/change.md:14` and `context/foundation/ui-mockup/README.md:100-127`: earlier palette, single-accent rule and system font.
  - Supported as history.
  - Superseded by this brief for palette, state colours and fonts.
  - The README token table needs an in-place update, or a note that it reflects M-1.
- `context/foundation/ui-mockup/README.md:131`: icon rule (inline SVG, `aria-hidden`, no icon font). Still valid and matches the brief.
- `context/archive/2026-09-28-account-and-first-animal/plan.md:331-338`: AppShell contract (`locale`, `messages`, `active`), tab bar below 768 px, sidebar from 768 px.
  - Still matches the code, except `alternateLocaleHref`: the language switch was later moved to `BaseLayout`, so that prop does not exist in `AppShell.tsx:7-12`.
- Language switching must keep the viewed resource (`context/archive/2026-10-04-capture-document-original/plan.md:255`, `context/changes/animal-profiles-and-history/plan.md:191`). `LanguageSwitch.tsx:16-19` implements this today.
- Stale edits keep the draft and need an explicit Refresh (`context/changes/animal-profiles-and-history/plan.md:22,192`). The conflict Alert must keep its Refresh action next to the form.
- PRD NFR (`context/foundation/prd.md:325-326`): a stored original stays openable whatever the reading state. The originals section must render independently of the reading panel, as `DocumentDetails.tsx:66` does today.

## Related Research

- `context/changes/animal-profiles-and-history/research.md`: profile, history and conflict behaviour.
- `context/changes/read-document-content/`: reading-status states rendered by `DocumentReadingStatus`.

## Open Questions

Each is a decision for `/10x-plan`:

1. **Originals viewer.** Keep the list of all pages, which needs no state and keeps per-page actions, or add the design's single-page viewer with thumbnails, which needs local state only.
2. **History rows without a content type.** Use one generic document tile for all rows, or derive it from `fileCount` (1 file could still be a PDF or a photo, so this is unreliable). Recommendation: a generic document tile and the existing file count.
3. **Month grouping in history.** Grouping by `eventDate` month is presentational, but it is new UI copy (month names through `Intl.DateTimeFormat`, no new keys) and adds visual structure the brief did not list explicitly.
4. **Confirm-inactive.** Keep it inline (current flow), styled like the design's dialog. A real modal would change the interaction flow.
5. **Sign-in copy.** Add the tagline and the "only the owner sees originals" line from the design. Both are new keys in `en.ts` and `pl.ts`.
6. **Placeholder and border tokens.**
   - The placeholder colour needs to be at least `#5B635E` to reach 4.5:1.
   - Field borders need at least 3:1 for the control boundary. The design's `#D9DDD6` is below that.
   - The visual weight changes slightly from the design.
7. **Lavender avatar.** The animal tile colour is outside the brief's palette. Use only peach and mint, or accept lavender as a third tile colour.
8. **Breakpoint.** The current shell switches at 768 px. The design shows only 390 and about 1280. Keep 768.
