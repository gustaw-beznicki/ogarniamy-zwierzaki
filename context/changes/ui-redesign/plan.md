# UI Redesign Implementation Plan

## Overview

Restyle the whole `apps/web` frontend to match the design reference in `context/changes/ui-redesign/design/`:

- a new palette and self-hosted Bricolage Grotesque + Figtree fonts;
- an SVG `Icon` component that replaces every Unicode glyph;
- a floating bottom bar on phones and a dark green sidebar on desktop;
- one `Alert` component for info, warning, error and success, plus field errors linked through `aria-describedby`;
- CSS-only motion that `prefers-reduced-motion` switches off;
- restyled screens: sign-in and registration, onboarding, animal list, profile with history, add document, and document with reading status.

API calls, form logic and navigation flows do not change. Every new string goes into both `en.ts` and `pl.ts`.

## Current State Analysis

Full evidence is in `context/changes/ui-redesign/research.md`. In short:

- **One stylesheet.** All styling is in `apps/web/src/styles/tokens.css`, imported once in `layouts/BaseLayout.astro:2`. There are no inline styles or other stylesheets.
- **Tokens.** Current tokens are 10 colours (`tokens.css:8-17`), 3 radii and 1 shadow. The font is `system-ui`.
- **Icons.** 13 distinct Unicode glyphs act as icons across 8 components. The ⓘ error prefix alone appears at 13 call sites.
- **Messages.**
  - Errors are `<p className="error" role="alert">`.
  - `.notice` is used for warnings, empty states and the reading-status panel.
  - No field has `aria-describedby`.
  - `DocumentCaptureForm` renders every error in one slot above Save (`DocumentCaptureForm.tsx:465`).
- **Shell.**
  - `AppShell` switches at 768 px between a fixed light tab bar and a 232 px light sidebar.
  - `LanguageSwitch` is rendered globally in `BaseLayout.astro:17`, positioned absolutely at the top right.
- **Verification.** The only checks are `npm run check` and `npm run build` (`.github/workflows/ci.yml:13-32`). There is no frontend test framework (`docs/testing.md:30`).
- **Smoke test.** The deploy smoke test needs `data-app="ogarniamy-zwierzaki"` on `/signin/` and `/en/signin/` (`scripts/smoke.sh:41-45`).

## Desired End State

Every screen at 390 px and 1280 px matches the design's visual language. Specifically:

- **Fonts.** Bricolage Grotesque for headings, Figtree for text, loaded from the app's own origin. No request goes to `fonts.googleapis.com` or `fonts.gstatic.com`.
- **Icons.** Every icon is an inline SVG from one `Icon` component. `git grep` finds no Unicode icon glyph in `apps/web/src/components`.
- **Messages.** Every message uses `Alert` (`info` / `warning` / `error` / `success`):
  - Errors appear in the content next to what they concern.
  - Field errors set `aria-invalid` and `aria-describedby` on their input.
- **Shell.** Phones get a floating white bottom bar with a green active tab. Desktop gets a `#1F4D3F` sidebar with an accent active item. The language switch keeps its exact behaviour.
- **Contrast and targets.** All text meets 4.5:1, control boundaries meet 3:1, and every interactive target is at least 44 × 44 px.
- **Motion.** Motion is CSS-only. With `prefers-reduced-motion: reduce`, nothing animates and final states (drawn checks, full bars) are shown.
- **Checks.** `npm run check` and `npm run build` pass, and the smoke-test marker is still present.

### Key Discoveries:

- `en.ts` is the source of `Messages` and `pl.ts` `satisfies Messages` (`i18n/pl.ts:1,180`). A key missing from either file fails `astro check`.
- `AnimalNameForm` is shared by onboarding, add animal and rename (`FirstAnimalForm.tsx:23`, `AnimalsList.tsx:108`, `AnimalDocuments.tsx:228`). One change covers all three.
- Focus return after edits uses `requestAnimationFrame` (`AnimalsList.tsx:64-70`, `AnimalDocuments.tsx:81-84`). The markup changes must keep the same refs on the same buttons.
- `DocumentSummary` has `eventDate`, `uploadedAt` and `fileCount` but no content type (`lib/api.ts:78-84`). History rows therefore use one generic document tile.
- The reading-status panel must never hide the originals (PRD NFR, `prd.md:325-326`). `DocumentDetails.tsx:66` renders it as an independent component, and that stays.
- Computed contrast:
  - White on the accent `#F29E6B` is 2.13:1, so accent fills always carry `#1A1F1C` text.
  - The design's placeholder `#7A827D` is 3.78:1, so placeholders use `#5B635E` (5.68–6.19:1).
  - The design's field border `#D9DDD6` fails 3:1, so field borders use `#868D88` (3.12:1 on `#F7F5EF`, 3.40:1 on white).
- Fonts: `@fontsource-variable/bricolage-grotesque` and `@fontsource-variable/figtree`, both 5.3.0 and OFL-1.1.
  - The family names are `'Bricolage Grotesque Variable'` and `'Figtree Variable'`.
  - The `latin-ext` subsets cover Polish.

## What We're NOT Doing

- **Search.** No search functionality and no `Mobile-Search` / `Desktop-Search` screens. The search page stays the `Placeholder`, only restyled.
- **Toasts.** Success messages stay inline in a live region.
- **API changes.** That includes adding a content type to `DocumentSummary`, so there is no photo-vs-PDF tile or "PDF" label in history rows.
- **Logic and flow changes.** Validation rules, API calls, state machines, redirects and the order of steps stay as they are.
  - The confirm-inactive step stays an inline card with its current focus behaviour. It is not converted to a modal, even though `Messages.dc.html` draws a dialog.
  - Native browser validation on the sign-in form stays as it is.
- **Display themes.** No dark mode and no theme switcher.
- **Dependencies.** No new testing, linting or Playwright setup, and no CSS framework.
- **Assets.** No new illustrations beyond the inline SVG paw marks and decorative shapes drawn in CSS. The favicon is not replaced.

## Implementation Approach

The work is split into six phases, and each one leaves the app building and usable.

**Phase 1** lays the foundation:

- tokens, fonts and element-level base styles;
- a set of shared CSS primitives (buttons, field wrapper, cards, tiles);
- the `Icon`, `Alert` and `FieldError` components.

Old class names keep working, so untouched screens still render with the new palette.

**Phases 2–5** restyle one screen group each. In every phase:

- swap glyphs for `Icon`;
- swap `.error` / `.notice` for `Alert`;
- link field errors.

**Phase 6** removes dead CSS, gates on a glyph grep, updates the foundation mockup README and runs the full two-width review.

`tokens.css` stays the single stylesheet: a token block, then base, primitives and per-screen sections. Do not add per-component CSS files; one global stylesheet is the existing convention.

**Class naming.** Keep current class names where the element keeps its role (`primary`, `secondary`, `card`, `back-link`, `animal-link`, `document-link`, `tab-bar`, `sidebar`, `language-switch`, …). Introduce new names only for new structures (hero, timeline, viewer, alert).

**Motion.**

- All keyframes live in `tokens.css`.
- One global `@media (prefers-reduced-motion: reduce)` block sets `animation: none` and `transition: none` on everything.
- The same block restores end states, e.g. `stroke-dashoffset: 0` for drawn checks and full-width progress shimmer.

**Copy.** Every new visible or accessible text is a new key added to both `en.ts` and `pl.ts` in the same edit. Polish strings are written only in `pl.ts` (lessons: English everywhere else).

## Critical Implementation Details

**Live regions**

- A `role="status"` region announces changes only if it exists before its content changes.
- Where a message appears dynamically, keep a persistent live-region wrapper and render a non-live `Alert` inside it:
  - the profile save notice, `AnimalDocuments.tsx:213`;
  - the capture progress, `DocumentCaptureForm.tsx:462`;
  - the reading-status texts.
- `Alert` must therefore accept `role` as a prop, defaulting by variant: `error` → `alert`, others → `status`. It must also allow `role={null}` to render no role.
- Existing warning notices that currently use `role="alert"` keep it: the conflict notice `AnimalDocuments.tsx:196` and the uncertain create `AnimalsList.tsx:120`.

**Field-error routing in the capture form**

- `errorCode` stays one state value.
- A pure lookup from code to zone (`files` | `animal` | `date` | `form`) decides only *where* the single `Alert` renders.
- The input of a zone with an error gets `aria-invalid` and `aria-describedby` pointing at that alert's id.
- Codes and zones:
  - `files`: `files_required`, `empty_file`, `file_too_large`, `heic_not_supported`, `unsupported_file_type`, `one_pdf_only`, `too_many_photos`, `duplicate_photo`, `file_unreadable`, `insecure_context`.
  - `animal`: `animal_required`, `animal_inactive`.
  - `date`: `invalid_event_date`, `future_event_date`.
  - `form`, rendered above Save as today: every other code, including all upload, retry and session errors.
- `duplicate_photo` and `too_many_photos` render as `warning`, because the selection is still valid. Every other code renders as `error`.

**Thumbnail viewer**

- The selected page index is local state that resets to 0 whenever the loaded document changes.
- Thumbnails are `<button>` elements with `aria-label` = the existing `pageLabel` and `aria-current="true"` on the selected one.
- Open and Download act on the selected page and keep the existing per-page `aria-label` pattern (`DocumentDetails.tsx:85-86`).
- A single-PDF document keeps the file card. A single-image document shows no thumbnail strip.

**Month grouping**

- Group the already-ordered `documents` array by the `YYYY-MM` of `eventDate` without re-sorting.
- When "Show more" appends rows for a month already shown, they join that month's group, so a heading is rendered once per distinct month.
- Month labels come from a small `formatEventMonth(locale, isoDate)` helper added to `i18n/format.ts`, so there are no new message keys.
  - It parses the date the same way `formatEventDate` does (`format.ts:20-27`, UTC).
  - It formats with `Intl.DateTimeFormat(locale, { month: 'long', year: 'numeric', timeZone: 'UTC' })`.
- Without a day, Polish ICU already gives the nominative form: "wrzesień 2026" (checked in Node 24 ICU). Do not add a day to these options, because a day switches the month to the genitive "września".

**Avatar colour**

- Pick the colour from a stable hash of `animal.id`, modulo 3 (peach, mint, lavender), so it does not change when the list order or an animal's activity changes.
- Inactive animals always use the neutral tile (`#ECEBE5` / `#5B635E`).
- The tile shows the first grapheme of the name, uppercased via `toLocaleUpperCase(locale)`, and is `aria-hidden` (the row's `aria-label` already names the animal).

**Language switch over the green hero**

- `LanguageSwitch` stays in `BaseLayout` and keeps its markup and behaviour.
- Its inverse colours on the sign-in hero come from CSS only (`body:has(.auth-hero) .language-switch`). Do not add a new prop.
- Its links get `min-height: 44px`.

## Phase 1: Foundation: fonts, tokens, primitives, Icon and Alert

### Overview

The new visual system that every screen builds on. After this phase the whole app renders in the new palette and fonts with the old layouts. Nothing is broken; it simply isn't redesigned yet.

### Changes Required:

#### 1. Fonts

**File**: `apps/web/package.json`, `apps/web/package-lock.json`, `apps/web/src/layouts/BaseLayout.astro`

**Intent**: Self-host both fonts from npm so there is no request to Google, and pin them through the lockfile.

**Contract**:

- `@fontsource-variable/bricolage-grotesque` and `@fontsource-variable/figtree` are added at `^5.3.0`, matching the existing caret convention in `package.json`. Install with `npm install` in `apps/web` so the lockfile updates.
- `BaseLayout.astro` frontmatter imports both packages' default CSS before `../styles/tokens.css`.

#### 2. Layout head

**File**: `apps/web/src/layouts/BaseLayout.astro`

**Intent**: Keep the head consistent with the new background.

**Contract**:

- `<meta name="theme-color">` becomes `#F7F5EF`.
- `data-app="ogarniamy-zwierzaki"` and `lang` stay unchanged.

#### 3. Tokens and base styles

**File**: `apps/web/src/styles/tokens.css`

**Intent**: Replace the palette with the owner's palette, keep the existing variable names where the role is the same, and add tokens for the new roles. Restyle the element-level base and the shared primitives so every screen inherits the new look.

**Contract**:

Kept names, new values:

| Token | Value |
| --- | --- |
| `--color-bg` | `#F7F5EF` |
| `--color-surface` | `#FFFFFF` (cards) |
| `--color-text` | `#1A1F1C` |
| `--color-text-muted` | `#5B635E` |
| `--color-accent` | `#F29E6B` |
| `--color-danger` | `#B3261E` |
| `--color-danger-bg` | `#FBE6E3` |
| `--color-danger-text` | `#8C1D16` |
| `--radius-control` | 14px |
| `--radius-card` | 24px |
| `--radius-pill` | 999px |
| `--shadow-card` | the design's soft two-layer shadow |

New tokens:

| Token | Value |
| --- | --- |
| `--color-primary` | `#1F4D3F` |
| `--color-primary-hover` | `#173B30` |
| `--color-primary-active-bg` | `#DCE8E1` |
| `--color-border` | `#E3E1D8` (decorative dividers only) |
| `--color-field-border` | `#868D88` |
| `--color-neutral` | `#F3F2EC` (neutral control and bubble fill) |
| `--color-accent-bg` | `#FDE7D8` |
| `--color-accent-text` | `#8A4317` |
| `--color-info`, `--color-info-bg`, `--color-info-text` | `#2F5D8A`, `#E6EEF7`, `#1E3A5A` |
| `--color-warning`, `--color-warning-bg`, `--color-warning-text` | `#B7791F`, `#FFF3D9`, `#5E4108` |
| `--color-success`, `--color-success-bg`, `--color-success-text` | `#1F6E52`, `#E2F1EA`, `#174F3B` |
| `--color-lavender-bg`, `--color-lavender-text` | `#E9E4F2`, `#4A3A6B` |
| `--color-inactive-bg` | `#ECEBE5` |
| `--color-on-primary-muted` | `#CFE0D7` |

Other new tokens:

- Each alert variant gets a matching border token: `#C9D8EA`, `#F1DCA6`, `#F2C7C1`, `#C2E0D1`.
- `--font-body`: `'Figtree Variable', 'Segoe UI', system-ui, sans-serif`.
- `--font-display`: `'Bricolage Grotesque Variable', 'Avenir Next', system-ui, sans-serif`.
- `--ease-out`: `cubic-bezier(.2,.8,.2,1)`.
- `--ease-spring`: `cubic-bezier(.34,1.3,.5,1)`.

Removed: `--color-chip` and `--color-sage`. Their uses are replaced by `--color-neutral` and `--color-primary-active-bg`.

Base:

- `body` uses `--font-body`.
- `h1`, `h2` and the `.display` utility use `--font-display` with `letter-spacing: -0.02em`. H1 is 38–40 px/800 on phone and `clamp(40px, 5vw, 60px)` on desktop; H2 is 24–26 px/800.
- `:focus-visible` is a 3 px `--color-primary` outline with a 3 px offset. Inside the dark sidebar and hero it is white.
- `input::placeholder` uses `--color-text-muted`.

Primitives:

| Primitive | Spec |
| --- | --- |
| `.primary` | green fill, white text, min-height 52, hover `--color-primary-hover`, active scale .97 |
| `.secondary` | `--color-neutral` fill, primary text, min-height 44 |
| `.accent` | accent fill, `--color-text` text |
| `.field` | 52 px wrapper with leading icon, 1.5 px `--color-field-border`, radius 16; `:focus-within` adds a primary border plus a 4 px `--color-primary-active-bg` ring and keeps the outline; `.field.invalid` has a 2 px `--color-danger` border and a `#FFFAF9` background |
| `.icon-button` | 44 × 44, radius 14, `--color-neutral` |
| `.card` | white, radius 24, `--shadow-card` |
| `.tile` | the icon bubble used in rows and reading status |
| `.chip` | min-height 44, pill, check icon when selected |

Motion:

- Keyframes: `rise`, `tabIn`, `pop`, `draw`, `pulse`, `breathe`, `indet`, `shimmer`, `scan`, `shake`, `dropIn`.
- A `.rise` utility with stagger via `--i`.
- The global reduced-motion block described in Critical Implementation Details.

Old class rules that screens still use stay until the phase that rewrites that screen. They are restyled only through the changed tokens.

#### 4. Icon component

**File**: `apps/web/src/components/Icon.tsx` (new)

**Intent**: One source for every interface icon, drawn from the design's SVG paths, so glyph rendering no longer depends on the system font.

**Contract**:

- Signature:
  ```ts
  export type IconName = 'search' | 'plus' | 'paw' | 'document' | 'camera' | 'photos' | 'pdf' | 'ocr' | 'shieldCheck' | 'clock' | 'check' | 'alertCircle' | 'refresh' | 'calendar' | 'external' | 'download' | 'pencil' | 'archive' | 'arrowUp' | 'arrowDown' | 'close' | 'mail' | 'lock' | 'logout' | 'chevronLeft' | 'chevronRight' | 'chevronDown' | 'arrowRight' | 'upload' | 'info';
  export function Icon({ name, size = 24, className }: { name: IconName; size?: number; className?: string }): JSX.Element
  ```
- Renders `<svg viewBox="0 0 24 24" width={size} height={size} aria-hidden="true" focusable="false">`, with `fill: none`, `stroke: currentColor`, stroke width 1.75 and round caps and joins. These come from an `.icon` class in `tokens.css`.
- Paths are copied from `design/Main.dc.html:107-130`. The six extras are listed in research §3.
- A separate `BrandMark` export renders the filled paw (`Main.dc.html:51`) for the logo tile.

#### 5. Alert and FieldError components

**File**: `apps/web/src/components/Alert.tsx` (new)

**Intent**: One component for the four message variants, replacing `.error` and `.notice`, with the design's badge, colours and optional action. Field errors get a small dedicated variant.

**Contract**:

- Signature:
  ```ts
  export type AlertVariant = 'info' | 'warning' | 'error' | 'success';
  export function Alert(props: { variant: AlertVariant; children: ReactNode; id?: string; compact?: boolean; role?: 'alert' | 'status' | null; action?: ReactNode; className?: string }): JSX.Element
  export function FieldError({ id, children }: { id: string; children: ReactNode }): JSX.Element
  ```
- The default `role` is `'alert'` for `error` and `'status'` for the other variants. `null` renders no role.
- The badge glyphs come from `Messages.dc.html:18,31,44,57`: white strokes on the variant icon colour, circular for info/warning/success and square for error. The badge is `aria-hidden`.
- Action buttons inside an Alert are at least 44 px tall. This deliberately deviates from the design's 40 px.
- `FieldError` renders the 16 px `alertCircle` icon plus text in `--color-danger-text`, with the given `id` and no role. The field-level message is reached through `aria-describedby`; the existing form-level announcement behaviour is kept by the caller.
- The `.alert-*` CSS lives in `tokens.css`. The error variant may use `shake` once on mount; nothing loops.

### Success Criteria:

#### Automated Verification:

- Type check passes: `npm run check` in `apps/web`
- Build passes: `npm run build` in `apps/web`
- Font files are emitted locally and no Google Fonts URL is referenced: `ls apps/web/dist/_astro/*.woff2` lists files and `grep -rE "fonts\.(googleapis|gstatic)" apps/web/dist` finds nothing
- The smoke marker is still present: `grep -l 'data-app="ogarniamy-zwierzaki"' apps/web/dist/signin/index.html apps/web/dist/en/signin/index.html`

#### Manual Verification:

- Every existing screen still renders and works in the new palette and fonts at 390 px and 1280 px, with no unreadable text or broken layout
- Polish diacritics render in Bricolage and Figtree (no fallback glyphs) on the sign-in and animals screens

**Implementation Note**: After automated verification passes, pause for human confirmation of the manual checks before Phase 2.

---

## Phase 2: Shell, language switch, sign-in, onboarding, gate and placeholder

### Overview

The frame of every screen and the screens outside it: the floating bottom bar, the green sidebar, the restyled language switch, the sign-in/registration hero, onboarding, the session-gate states and the search placeholder.

### Changes Required:

#### 1. AppShell

**File**: `apps/web/src/components/AppShell.tsx`, `apps/web/src/styles/tokens.css`

**Intent**: Implement the design's floating mobile bar and dark green desktop sidebar while keeping the nav model, sign-out logic and the 768 px breakpoint.

**Contract**:

- Same props `{ locale, messages, active, children }`. The `nav` items carry an `IconName` instead of a glyph.
- **Below 768 px:**
  - A mobile header row with `BrandMark` in a 40 px primary tile. It leaves room for the language switch at the top right, which stays outside the shell.
  - `nav.tab-bar`: fixed, `left/right: 16px`, `bottom: calc(16px + env(safe-area-inset-bottom))`, radius 28, white, large shadow.
  - Grid `repeat(3, 1fr) 60px`. Tabs are at least 56 px tall with a 12 px label.
  - The active tab has a primary fill, white text and `aria-current="page"`. Add `aria-current` (new) alongside the class.
  - The Add tab's icon sits in a 28 px accent circle.
  - The last cell is the icon-only sign-out button and keeps `aria-label={messages.signOut}`.
  - Main content gets enough bottom padding (about 120 px plus the safe area) so the bar never covers it.
- **From 768 px:**
  - `aside.sidebar`: `--color-primary` background, sticky full height, padding 28/18.
  - Brand link: 44 px accent `BrandMark` tile plus a two-line display wordmark, at least 44 px tall.
  - Nav links: 48 px, radius 16, text `#E3EDE7`. Active: accent fill, `--color-text`, `aria-current="page"`.
  - The sign-out button is pinned to the bottom with the `logout` icon and text, in `--color-on-primary-muted`.
  - Main column: `padding: 28px clamp(20px, 4vw, 56px) 56px`, inner `max-width: 1040px`.
- The sign-out failure becomes `<Alert variant="error">` with `messages.signOutFailed`.
  - On desktop it is rendered in the sidebar directly above the sign-out button, in compact form.
  - On mobile it is rendered at the top of the main content.
  - The message stays next to the control it concerns, and the same state value drives both placements.

#### 2. LanguageSwitch styling

**File**: `apps/web/src/styles/tokens.css` (component markup unchanged)

**Intent**: Restyle the switch to match the design and meet the 44 px rule without touching its behaviour.

**Contract**:

- `.language-switch`: white pill track with a soft shadow. Active link: primary fill and white text. Inactive link: `--color-text`. Each link has `min-height: 44px` and `min-width: 44px`.
- On mobile app pages it aligns with the shell header row at the top right.
- On desktop it sits at the top right of the main column, absolute within the viewport content area, and must not overlap the sidebar.
- `body:has(.auth-hero) .language-switch`: translucent white track (`rgba(255,255,255,.14)`), white text, and a white active link with primary text.
- `LanguageSwitch.tsx` is not edited.

#### 3. Sign-in and registration

**File**: `apps/web/src/components/AuthForm.tsx`, `apps/web/src/styles/tokens.css`, `apps/web/src/i18n/en.ts`, `apps/web/src/i18n/pl.ts`

**Intent**: Build the green hero screen from `Mobile-Signin.dc.html` around the unchanged form logic, and put server errors that concern a single field next to that field.

**Contract**:

- **Hero.**
  - A `.auth-hero` green band (height about 360 px on phone, radius `0 0 40px 40px`) with three decorative `aria-hidden` paw marks drifting slowly.
  - `BrandMark` in a 64 px accent tile.
  - The H1 `messages.appName`.
  - The new tagline `messages.authTagline`.
- **Card.**
  - A white card (radius 28) overlapping the hero.
  - Segmented tabs (existing `role="tablist"` / `tab` buttons, `aria-selected` unchanged, at least 44 px tall) with a sliding accent indicator that moves by transform.
  - Fields use `.field` with the `mail` and `lock` icons.
  - Submit is a 54 px primary button with an `arrowRight` icon.
- **Footer line.** The new `messages.authPrivacyNote` with the `shieldCheck` icon in `--color-success`, below the card.
- **Desktop.** The same structure centred: the hero band spans the width and the card is at most 460 px. No separate desktop design exists; keep it centred and calm.
- **Field errors.**
  - `invalid_email` and `email_taken` render as `FieldError` under the email field. `password_too_short` renders under the password field.
  - That input gets `aria-invalid="true"` and `aria-describedby`.
  - Every other code renders as `<Alert variant="error">` above the submit button, as today.
  - The error is cleared exactly when it is today (mode switch, new submit).
  - The `code → field` lookup is a pure function next to the component. It does not change which message is chosen (`AuthForm.tsx:23`).
- **New keys** in both `en.ts` and `pl.ts`:
  - `authTagline`: EN "Private archive of veterinary documents" / PL "Prywatne archiwum dokumentów weterynaryjnych".
  - `authPrivacyNote`: EN "Only the account owner can see the originals" / PL "Oryginały widzi tylko właściciel konta".
  - The owner reviews both PL strings.

#### 4. Onboarding and AnimalNameForm field error

**File**: `apps/web/src/components/FirstAnimalForm.tsx`, `apps/web/src/components/AnimalNameForm.tsx`

**Intent**: Give onboarding the sign-in card language, and make the shared name form link its error to its field (this covers onboarding, add animal and rename).

**Contract**:

- **Onboarding:**
  - A white card on the background, at most 560 px wide.
  - `BrandMark` tile, display H1 `firstAnimalTitle` and the intro in muted text.
  - Outside the shell, as today. It does not use `.auth-hero`, so the language switch keeps its default styling.
- **AnimalNameForm:**
  - The input sits in a `.field` wrapper with the `paw` icon.
  - When a message is shown (client validation or caller `error`), it renders as `FieldError` with an id from `useId()`.
  - The input gets `aria-invalid="true"` and `aria-describedby={thatId}` whenever a message is shown. This extends `aria-invalid` to caller errors; validation logic is unchanged.
  - Wrap the `FieldError` in a persistent `role="alert"` container so the current immediate announcement stays.
  - The action buttons use the new `.primary` / `.secondary`.

#### 5. Session gate and search placeholder

**File**: `apps/web/src/components/SessionGate.tsx`, `apps/web/src/components/Placeholder.tsx`, `apps/web/src/components/PageState.tsx` (new)

**Intent**: Give loading, page-level failures and the placeholder the design's styling. Add one shared page-level state component that the later phases reuse for "unavailable" screens.

**Contract**:

- `PageState({ title, tone: 'error' | 'info', action?: ReactNode })`:
  - A centred white card with a decorative `aria-hidden` document illustration in CSS and an `Icon` badge (error: `close` on danger; info: `search` on primary).
  - A display H2 with `role="alert"` when `tone` is `error`, and an optional action.
  - It follows `Messages.dc.html:136-148`.
- `SessionGate` failure renders `PageState` with `request_failed`. Loading stays `role="status"` with `.loading` styling (muted text and a pulsing paw `BrandMark`).
- `Placeholder` renders `PageState` with `tone="info"`, title = the given title and body `comingSoon`.

### Success Criteria:

#### Automated Verification:

- Type check passes: `npm run check` in `apps/web`
- Build passes: `npm run build` in `apps/web`
- Every new message key exists in both catalogues (enforced by `satisfies Messages` in `npm run check`)
- No Unicode icon glyphs remain in the files changed in this phase: `git grep -nP "[⌕♧↪ⓘ]" -- apps/web/src/components/AppShell.tsx apps/web/src/components/AuthForm.tsx apps/web/src/components/AnimalNameForm.tsx apps/web/src/components/SessionGate.tsx` returns nothing
- No Polish outside `pl.ts`: `git grep -nP "[ąćęłńóśźżĄĆĘŁŃÓŚŹŻ]" -- apps/web/src ':!apps/web/src/i18n/pl.ts'` returns nothing

#### Manual Verification:

- At 390 px the bottom bar floats with a 16 px gap, never covers the last content row, and the active tab is green with white text; at 1280 px the sidebar is dark green with an accent active item and sign-out at the bottom
- The language switch on sign-in (hero styling), onboarding, animals and a document page switches PL/EN and keeps `?animalId=` / `?id=`; every link is at least 44 px tall
- Sign-in and registration: the tab indicator slides, a wrong-format email from the API shows next to the email field and a screen reader announces it with the field, and a wrong password shows above the button
- Onboarding: an empty name shows the error under the field, linked by `aria-describedby`
- A forced sign-out failure (API stopped) shows the error Alert next to the sign-out control on desktop and at the top on mobile
- Keyboard: Tab order through the shell is logical and every focus ring is visible on both light and green backgrounds

**Implementation Note**: After automated verification passes, pause for human confirmation of the manual checks before Phase 3.

---

## Phase 3: Animal list and profile with history timeline

### Overview

The animals screen (mobile rows, desktop card grid, inactive section) and the profile hero with its edit, activity and conflict states, plus the month-grouped history timeline.

### Changes Required:

#### 1. Animal avatar helper

**File**: `apps/web/src/components/AnimalAvatar.tsx` (new)

**Intent**: One initial-letter tile used in list rows, desktop cards and the profile hero, with a stable colour per animal.

**Contract**:

- Signature: `AnimalAvatar({ animal, locale, size: 'row' | 'card' | 'hero' })`.
- Sizes: 56 / 72 / 76 px.
- Colour: `hash(animal.id) % 3` picks peach, mint or lavender. When `!animal.isActive` the tile is always the inactive neutral.
- The tile is `aria-hidden`.

#### 2. Animals list

**File**: `apps/web/src/components/AnimalsList.tsx`, `apps/web/src/styles/tokens.css`

**Intent**: Match `Mobile-Animals.dc.html` and `Desktop-Animals.dc.html` without changing loading, create or uncertain-create logic.

**Contract**:

- **Header.** Display H1 `animalsTitle`. The Add animal button keeps the `addButton` ref:
  - mobile: white pill with a mint `plus` bubble;
  - desktop: 52 px primary with a `plus` icon.
- **Rows** (`animal-link`, `aria-label` unchanged):
  - Mobile: white, radius 24, avatar, name 18/700, muted count, and a 40 px `chevronRight` bubble that turns primary on hover.
  - From 768 px: a card grid `repeat(auto-fit, minmax(min(280px, 100%), 1fr))` with a 72 px avatar, display name and an `arrowRight` bubble.
  - Entrance uses `.rise` with a 60 ms stagger.
- **Inactive section.** The `<details>` summary gets the `archive` icon and a rotating `chevronDown` and stays at least 44 px tall. Inactive rows have a dashed border, a neutral avatar and the `statusInactive` pill.
- **Messages:**
  - Load failure: `<Alert variant="error" action={retry button}>`.
  - `noAnimals` and `noActiveAnimalsManage`: `<Alert variant="info">`.
  - `createUncertain`: `<Alert variant="warning" role="alert" action={refresh button}>` inside the add form, keeping today's place.
- **Add form.** The card keeps `aria-labelledby="add-animal-title"` and renders as a white card.

#### 3. Profile and history

**File**: `apps/web/src/components/AnimalDocuments.tsx`, `apps/web/src/i18n/format.ts`, `apps/web/src/styles/tokens.css`

**Intent**: Build the profile hero and the history timeline from `Mobile-Profile.dc.html` and `Mobile-Messages.dc.html`, keeping every mutation, conflict and focus-return path unchanged.

**Contract**:

- **Back link.** `backToAnimals` with a 40 px white `chevronLeft` bubble, at least 44 px tall.
- **Hero.**
  - A `section.profile-hero` with `aria-labelledby` pointing at the H1. Contents: avatar (hero size), display H1 name, status pill with a dot (`statusActive` / `statusInactive`) and `countLabel(documentCount)` from `animal.storedDocumentCount`.
  - Active hero: primary background, white text, decorative paw mark. Inactive hero: `--color-inactive-bg` with `--color-text`.
  - Glass buttons inside the hero:
    - `editName` with `pencil` (keeps `editButton` ref);
    - when active, `markInactive` with `archive` (keeps `activityButton` ref).
- **Inactive profile.**
  - `inactiveProfileNotice` becomes `<Alert variant="warning">` below the hero.
  - Its `action` is the existing `markActive` button: same handler, same `activityButton` ref, same disabled logic. It is moved from the action row into the Alert.
- **Profile save notice.** The existing persistent `role="status"` `.profile-status` element stays. When `notice` is non-empty it contains `<Alert variant={notice === messages.profileRefreshedActivity ? 'info' : 'success'} compact role={null}>`.
- **Errors and conflicts:**
  - `errorText` becomes `<Alert variant="error">`.
  - `conflictNotice` becomes `<Alert variant="warning" role="alert" action={refresh button}>`. It is rendered at the same three places as today.
  - `profileRefreshedDraft` becomes `<Alert variant="info" compact role={null}>` inside the rename form.
- **Rename and confirm-inactive cards.**
  - Both are white cards with the same `aria-*` attributes, `tabIndex` and `autoFocus` as today.
  - The confirm card adopts the dialog look from `Messages.dc.html:124-131`: a warning `archive` tile, display H2 and right-aligned actions. The primary action comes last.
- **History.**
  - Header: display H2 `documentsTitle`, muted `documentsOrder`, and, when active, the accent `addDocument` link with a `plus` icon (moved from the page header).
  - Timeline: a decorative `aria-hidden` rail and dots. Month headings are rendered as `<h3>` (uppercase 12/700 muted), grouped as described in Critical Implementation Details.
  - Rows (`document-link`, `aria-label` unchanged): a generic `document` tile (50 × 60, neutral background, primary icon), event date 17/700, `countLabel(fileCount)`, a muted `` `${messages.uploadedOn}: ${formatTimestamp(locale, document.uploadedAt)}` `` line, and `chevronRight`.
  - The uploaded line reuses `formatTimestamp` (`i18n/format.ts:30`, date plus time, as on the document page). Do not add a new date format.
- **Empty and failure states:**
  - `documentsEmpty` becomes `<Alert variant="info">`.
  - The load-more failure becomes `<Alert variant="error">` above "Show more".
  - "Show more" is a full-width white 48 px button with `chevronDown`.
- **Page-level** `unavailable` / `failed` render `PageState` (tone `error`) with the back-to-animals link as its action.

### Success Criteria:

#### Automated Verification:

- Type check passes: `npm run check` in `apps/web`
- Build passes: `npm run build` in `apps/web`
- No Unicode icon glyphs remain in `AnimalsList.tsx` and `AnimalDocuments.tsx`: `git grep -nP "[♧›‹▤ⓘ]" -- apps/web/src/components/AnimalsList.tsx apps/web/src/components/AnimalDocuments.tsx` returns nothing

#### Manual Verification:

- Animals at 390 px shows rows and at 1280 px a card grid; the inactive section expands and collapses with a rotating chevron, and avatar colours stay the same after reload and after another animal is marked inactive
- Profile: renaming saves and announces the success message, focus returns to "Edit name", and a forced 412 conflict shows the warning Alert with Refresh while keeping the typed draft
- Marking inactive turns the hero grey and shows the warning Alert with "Mark as active"; activating returns the green hero and focus lands on the activity button
- History groups rows by month with a single heading per month after "Show more" loads further rows, and Polish month names read naturally (e.g. "wrzesień 2026")
- Unavailable profile (`?animalId=` of a missing or foreign animal) shows the page-level state with a working back link

**Implementation Note**: After automated verification passes, pause for human confirmation of the manual checks before Phase 4.

---

## Phase 4: Add document

### Overview

The capture screen from `Mobile-Add.dc.html`: source tiles, selected files card, animal chips, date field, save, progress and the saved state. Errors are placed next to the zone they concern.

### Changes Required:

#### 1. Capture form

**File**: `apps/web/src/components/DocumentCaptureForm.tsx`, `apps/web/src/styles/tokens.css`

**Intent**: Restyle the capture flow and route each error to the zone it concerns, with no change to selection, validation, upload or retry logic.

**Contract**:

- **Header.** Display H1 `captureTitle` and muted `captureIntro`.
- **Source tiles.**
  - Three `.source-button`s, at least 116 px tall, radius 22.
  - "Take photo" is primary green with an accent `camera` bubble. "Add photos" is white with a mint `photos` bubble. "Choose PDF" is white with a peach `pdf` bubble.
  - Hidden inputs and click handlers are unchanged.
- **Limits.** `captureLimits` with the `info` icon.
- **Files zone:**
  - The `files`-zone error renders here as an `Alert`, directly under the source tiles and before the selection card.
  - It has an `id`. The selection `section` gets `aria-describedby` pointing at it while it is shown.
- **Replace prompt.** `replacement` becomes `<Alert variant="warning" role={null} action={replace + keep buttons}>` inside the existing `role="group"` wrapper with its `aria-label`.
- **Selected files.**
  - A white card with the H2 and the count.
  - Rows show the real preview (radius 14) or a `pdf` tile, then the name.
  - 44 px `icon-button`s with `arrowUp`, `arrowDown` and `close`; remove uses `--color-danger`.
  - `aria-label`s are unchanged and the disabled logic is unchanged.
- **Animal chips.**
  - The visually hidden radios inside labels stay.
  - The selected chip has an accent fill and the `check` icon. Unselected chips are white.
  - The focus ring shows on the label via `:focus-within`.
  - The `animal`-zone error renders under the chips. The `fieldset` gets `aria-describedby`; the radios do not get `aria-invalid`.
- **Date.**
  - `.field` wrapper with the `calendar` icon, at most 240 px wide.
  - The `date`-zone error renders as `FieldError` under it. The date input gets `aria-invalid` and `aria-describedby`.
- **Summary.** `captureSummary` stays as is.
- **Save.** A 56 px primary button.
- **Progress:**
  - The existing `<progress>` stays as the semantic element, with the same `max`, `value` and `aria-label`. It is styled as a 10 px rounded bar (primary on mint, shimmer) using `::-webkit-progress-value` / `::-moz-progress-bar`.
  - The phase text stays in the persistent `role="status"` paragraph.
  - Inside a white card.
  - When `progress.phase === 'done'`, the card shows the success tile with the pop and draw check next to `phase_done`. The redirect is unchanged.
- **Form-zone errors.** They render as `Alert` (error, or warning per Critical Implementation Details) where `.error` is today, above Save or Retry.
- **Session expired.** `<Alert variant="error">` plus the `signInAgain` primary link.
- **No active animals.** `<Alert variant="info">` with `noActiveAnimals` plus the primary link.
- **Load failure.** `PageState` tone `error`.

### Success Criteria:

#### Automated Verification:

- Type check passes: `npm run check` in `apps/web`
- Build passes: `npm run build` in `apps/web`
- No Unicode icon glyphs remain in `DocumentCaptureForm.tsx`: `git grep -nP "[◉＋▤↑↓✕✓ⓘ]" -- apps/web/src/components/DocumentCaptureForm.tsx` returns nothing

#### Manual Verification:

- Selecting a HEIC or over-10 MB file shows the error directly under the source tiles; Save with no files shows `files_required` there; a future date shows the error under the date field and a screen reader reads it with the date input
- Adding the same photo twice shows a warning (not an error) and keeps the selection
- Reordering and removing photos works with 44 px buttons, and disabled up/down states are visible
- Saving shows the progress card with a moving shimmer, then the saved check, then opens the document; with reduced motion enabled the bar and check appear static and complete
- Picking a PDF over existing photos shows the warning prompt with Replace and Keep, and both work as before
- Layout works at 390 px (three tiles in one row, no horizontal scroll) and at 1280 px

**Implementation Note**: After automated verification passes, pause for human confirmation of the manual checks before Phase 5.

---

## Phase 5: Document details, reading status and originals viewer

### Overview

The document screen from `Mobile-Document.dc.html` and `Desktop-Document.dc.html`: the details tiles, the stored notice, the reading panel in four states and the originals viewer with thumbnails (two columns on desktop).

### Changes Required:

#### 1. Document details and viewer

**File**: `apps/web/src/components/DocumentDetails.tsx`, `apps/web/src/i18n/en.ts`, `apps/web/src/i18n/pl.ts`, `apps/web/src/styles/tokens.css`

**Intent**: Match the design's hierarchy and add the single-page viewer, keeping the same-origin guard, URLs and accessible labels.

**Contract**:

- **Header.**
  - `backToDocuments` link (chevron bubble).
  - An eyebrow with `document.animalName` (muted 14/700) and a display H1 `documentTitle`. This is the reverse of the current order, as in the design.
- **Details.**
  - Two `dl` tiles: `eventDate` with `calendar`, and `uploadedOn` with `upload`. White cards, two columns on phone; on desktop a single card with a divider.
  - The `animal` field is removed from the list, because the animal name is now the eyebrow.
- **Stored notice.** `storedNotice` becomes a compact success pill (`--color-success-bg`, `--color-success` text, `shieldCheck`). It is not a live region.
- **Layout.** From 1024 px: a two-column flex. The viewer is `flex: 3 1 480px`; details, notice and reading panel are `flex: 2 1 320px`. Below that, everything is stacked in this order: details, notice, reading, originals.
- **Images viewer** (one or more image files):
  - A `figure` with the selected page `<img>` (current `alt` pattern, `object-fit: contain`, max height 70vh on desktop, warm `#E7E3D8` stage).
  - A `figcaption` with `pageLabel`.
  - When there are 2+ pages, a thumbnail strip of `<button>`s that are `<img>` thumbnails of the same original URLs with `alt=""`. The button has `aria-label` = `pageLabel` and `aria-current="true"` when selected, and a primary 2 px border when selected.
  - Open (primary, `external`) and Download (secondary, `download`) for the selected page, with the existing `aria-label`s that include the page label.
  - New keys:
    - `originalPagesLabel`: EN "Pages" / PL "Strony". Used as the thumbnail group's `aria-label`.
- **PDF.** A white card with the `pdf` tile, the file name, and `openPdf` / `downloadPdf` with icons.
- **Reading scan line.** While the reading state is `reading`, the decorative scan line shows over the stage. `DocumentReadingStatus` reports its state upward through an optional `onStateChange?: (state: ReadingState | null) => void` prop. This is presentation only; polling is unaffected.
- **Page states.** `unavailable` / `failed` render `PageState` with the back-to-animals action.

#### 2. Reading status panel

**File**: `apps/web/src/components/DocumentReadingStatus.tsx`, `apps/web/src/styles/tokens.css`

**Intent**: Give each reading state the design's tile and motion while keeping polling, retry and the live-region roles exactly as they are.

**Contract**:

- **Container.** `section.reading-status` becomes a white card with `aria-labelledby` unchanged. The `.notice` class is dropped.
- **States.** Each row is a 44 px tile plus text; the text stays in the existing `role="status"` / `role="alert"` paragraphs.
  - `loading`: neutral `clock` tile with `breathe`.
  - `queued`: neutral `clock` tile with `breathe`.
  - `reading`: peach `ocr` tile with `pulse` and an indeterminate 6 px accent bar below it.
  - `read`: success tile with pop and the drawn `check`; `readingReadOn` text with a muted secondary line.
  - `failed`: danger-bg tile with `alertCircle`. The message `reading_failed` is in `--color-danger-text`, and the failure detail is a muted second line.
- **Panel messages:**
  - `readingRetryAfter`: `<Alert variant="info" compact role={null}>`.
  - `readingPollingStopped`: `<Alert variant="info" compact role={null}>`.
  - `retryError`: `<Alert variant="error">` (`role="alert"`, as today).
  - `readingAccountChanged`: `<Alert variant="warning" role="alert">`.
  - `readingStatusUnavailable`: `<Alert variant="warning" role="status">`.
- **Buttons.**
  - Retry: secondary with the `refresh` icon, which rotates on hover.
  - Refresh: secondary.
  - Disabled logic is unchanged.
- `onStateChange` is called with the current `status.state` when `view === 'ready'`, and with `null` otherwise.

### Success Criteria:

#### Automated Verification:

- Type check passes: `npm run check` in `apps/web`
- Build passes: `npm run build` in `apps/web`
- No Unicode icon glyphs remain in `DocumentDetails.tsx` and `DocumentReadingStatus.tsx`: `git grep -nP "[‹▤✓ⓘ]" -- apps/web/src/components/DocumentDetails.tsx apps/web/src/components/DocumentReadingStatus.tsx` returns nothing

#### Manual Verification:

- Each reading state (queued, reading, read, failed with a detail, retry-limited, polling stopped, status unavailable) shows its own tile and copy; the scan line appears only while reading; Retry still works once and then shows the limit message
- A 3-photo document: the thumbnails switch the large page, the selected thumbnail is marked (border and `aria-current`), Open and Download open the selected page, and keyboard Tab/Enter works on the thumbnails
- A 1-photo document shows no thumbnail strip; a PDF document shows the file card with Open PDF / Download PDF
- At 1280 px the viewer and the details sit in two columns; at 390 px everything stacks with the details first and the originals reachable even when the reading status is unavailable
- With reduced motion enabled, nothing pulses, scans or shakes, and the read check is fully drawn

**Implementation Note**: After automated verification passes, pause for human confirmation of the manual checks before Phase 6.

---

## Phase 6: Cleanup, documentation and full review

### Overview

Remove what the redesign made obsolete, gate the result mechanically, record the new visual system in the foundation docs, and review every screen at both widths.

### Changes Required:

#### 1. Dead CSS and glyph gate

**File**: `apps/web/src/styles/tokens.css`, `apps/web/src/components/*.tsx`

**Intent**: Leave one coherent stylesheet with no rules for removed classes and no leftover glyph icons.

**Contract**:

- Remove the rules for `.error`, `.notice`, `.notice-actions`, `.paw`, `.chevron`, `.stored-notice`, `.list-error`, `.shell-error`, `.status-pill.inactive` (if replaced) and any other selector no longer referenced by a `.tsx` / `.astro` file.
- Every class used in TSX is defined, with three exceptions that are intentionally unstyled hooks: `animal-name-form`, `chip-group`, `replace-notice`. Either style them or drop them from the markup.
- No hard-coded colour outside the token block, except `rgba()` overlays derived from the tokens' RGB values.

#### 2. Foundation docs

**File**: `context/foundation/ui-mockup/README.md`

**Intent**: The README's token table and "single accent" rule describe the M-1 mockup and are now superseded. Readers need to know where the current visual system lives.

**Contract**:

- Add a short "Superseded (2026-10)" note at the top of the Tokens section. It points to `apps/web/src/styles/tokens.css` and `context/changes/ui-redesign/design/Main.dc.html`, and states that state colours, fonts and the icon set were replaced by the ui-redesign change.
- Do not rewrite the historical table.

### Success Criteria:

#### Automated Verification:

- Type check passes: `npm run check` in `apps/web`
- Build passes: `npm run build` in `apps/web`
- No Unicode icon glyphs remain anywhere in components: `git grep -nP "[⌕♧↪ⓘ›‹▤✓◉＋↑↓✕]" -- apps/web/src/components` returns nothing
- No `.error` or `.notice` class usage remains: `git grep -nE "className=\"(error|notice)( |\")" -- apps/web/src` returns nothing
- No Polish outside `pl.ts`: `git grep -nP "[ąćęłńóśźżĄĆĘŁŃÓŚŹŻ]" -- apps/web/src ':!apps/web/src/i18n/pl.ts'` returns nothing
- No Google Fonts reference in the build: `grep -rE "fonts\.(googleapis|gstatic)" apps/web/dist` returns nothing
- The reduced-motion block exists: `grep -n "prefers-reduced-motion: reduce" apps/web/src/styles/tokens.css` returns a match

#### Manual Verification:

- Every screen (sign-in, registration, onboarding, animals, profile active and inactive, add document, document photos and PDF, search placeholder) reviewed in PL and EN at 390 px and 1280 px against the design files, with no horizontal scroll and nothing hidden behind the bottom bar
- Keyboard-only pass on every screen: visible focus, logical order, every target at least 44 px
- With OS reduced motion enabled, no screen animates
- Owner approves the new PL strings (`authTagline`, `authPrivacyNote`, `originalPagesLabel`)

**Implementation Note**: After automated verification passes, pause for the human's full review before closing the change.

---

## Testing Strategy

### Unit Tests:

- None added. The repo has no frontend test framework (`docs/testing.md:30`), and adding one is out of scope. The pure helpers introduced here are deliberately small and side-effect free, so a later test phase (test-plan Phase 2) can cover them directly:
  - the error-code → zone lookup;
  - the avatar colour hash;
  - month grouping.

### Integration Tests:

- `npm run check` (types, including the i18n catalogue parity) and `npm run build` in every phase. CI runs the same pair.

### Manual Testing Steps:

1. Run the API and web locally (`docs/local-development.md`). Register a fresh account and go through onboarding.
2. Add three animals, then mark one inactive. Check the list at 390 px and 1280 px (Chrome DevTools device toolbar).
3. Add a 3-photo document and a PDF document. Trigger each field error zone (no files, a future date, a HEIC file).
4. Open each document and step through the reading states (queued and reading appear right after upload; failed via an empty or encrypted PDF).
5. Force a conflict: rename the same animal in two tabs.
6. Toggle OS reduced motion and repeat a quick pass.
7. Switch the language on every screen and confirm the query string is kept.

## Performance Considerations

- Fonts: two variable woff2 files per used subset (latin and latin-ext), `font-display: swap`. Bricolage is heavier than Figtree, but it is used only for headings. Do not preload in this change.
- All motion is transform/opacity keyframes, with no layout-affecting animation in loops. Looping animations exist only in the in-progress states (reading pulse, scan, shimmer).

## Migration Notes

None. There is no data change. The two new font packages are the only dependency change, and rollback is reverting the change's commits.

## References

- Research: `context/changes/ui-redesign/research.md`
- Design reference: `context/changes/ui-redesign/design/` (Main, Messages, Mobile-*, Desktop-*)
- Earlier visual system: `context/foundation/ui-mockup/README.md:100-131`
- Shell contract: `context/archive/2026-09-28-account-and-first-animal/plan.md:331-338`
- Language-switch behaviour: `apps/web/src/components/LanguageSwitch.tsx:10-29`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Foundation: fonts, tokens, primitives, Icon and Alert

#### Automated

- [x] 1.1 Type check passes: `npm run check` in `apps/web` — bed2f3b
- [x] 1.2 Build passes: `npm run build` in `apps/web` — bed2f3b
- [x] 1.3 Font files are emitted locally and no Google Fonts URL is referenced — bed2f3b
- [x] 1.4 The smoke marker is still present on both sign-in pages — bed2f3b

#### Manual

- [ ] 1.5 Every existing screen still renders and works in the new palette and fonts at 390 px and 1280 px
- [ ] 1.6 Polish diacritics render in Bricolage and Figtree

### Phase 2: Shell, language switch, sign-in, onboarding, gate and placeholder

#### Automated

- [x] 2.1 Type check passes: `npm run check` in `apps/web`
- [x] 2.2 Build passes: `npm run build` in `apps/web`
- [x] 2.3 Every new message key exists in both catalogues
- [x] 2.4 No Unicode icon glyphs remain in the files changed in this phase
- [x] 2.5 No Polish outside `pl.ts`

#### Manual

- [ ] 2.6 Floating bottom bar at 390 px and dark green sidebar at 1280 px
- [ ] 2.7 Language switch keeps behaviour and query string on every screen, 44 px links
- [ ] 2.8 Sign-in and registration: sliding tabs, field-level and form-level errors
- [ ] 2.9 Onboarding: empty-name error under the field, linked by aria-describedby
- [ ] 2.10 Sign-out failure Alert next to the sign-out control
- [ ] 2.11 Keyboard order and visible focus on light and green backgrounds

### Phase 3: Animal list and profile with history timeline

#### Automated

- [ ] 3.1 Type check passes: `npm run check` in `apps/web`
- [ ] 3.2 Build passes: `npm run build` in `apps/web`
- [ ] 3.3 No Unicode icon glyphs remain in `AnimalsList.tsx` and `AnimalDocuments.tsx`

#### Manual

- [ ] 3.4 Animals rows at 390 px, card grid at 1280 px, inactive section, stable avatar colours
- [ ] 3.5 Profile rename success, focus return, and conflict warning with kept draft
- [ ] 3.6 Inactive hero and warning Alert with Mark as active; focus after activation
- [ ] 3.7 History month grouping with one heading per month after Show more
- [ ] 3.8 Unavailable profile shows the page-level state with a back link

### Phase 4: Add document

#### Automated

- [ ] 4.1 Type check passes: `npm run check` in `apps/web`
- [ ] 4.2 Build passes: `npm run build` in `apps/web`
- [ ] 4.3 No Unicode icon glyphs remain in `DocumentCaptureForm.tsx`

#### Manual

- [ ] 4.4 Field errors appear in the files, animal and date zones and are linked to their inputs
- [ ] 4.5 Duplicate photo shows a warning and keeps the selection
- [ ] 4.6 Reorder and remove work with 44 px buttons
- [ ] 4.7 Progress shimmer, saved check and redirect; static with reduced motion
- [ ] 4.8 PDF-over-photos replace prompt works as before
- [ ] 4.9 Layout works at 390 px and 1280 px

### Phase 5: Document details, reading status and originals viewer

#### Automated

- [ ] 5.1 Type check passes: `npm run check` in `apps/web`
- [ ] 5.2 Build passes: `npm run build` in `apps/web`
- [ ] 5.3 No Unicode icon glyphs remain in `DocumentDetails.tsx` and `DocumentReadingStatus.tsx`

#### Manual

- [ ] 5.4 Every reading state shows its own tile and copy; scan line only while reading; retry works
- [ ] 5.5 Thumbnail viewer switches pages with marked selection and keyboard support
- [ ] 5.6 Single photo shows no strip; PDF shows the file card
- [ ] 5.7 Two columns at 1280 px, stacked at 390 px, originals reachable when status is unavailable
- [ ] 5.8 Reduced motion stops pulse, scan and shake and shows the drawn check

### Phase 6: Cleanup, documentation and full review

#### Automated

- [ ] 6.1 Type check passes: `npm run check` in `apps/web`
- [ ] 6.2 Build passes: `npm run build` in `apps/web`
- [ ] 6.3 No Unicode icon glyphs remain anywhere in components
- [ ] 6.4 No `.error` or `.notice` class usage remains
- [ ] 6.5 No Polish outside `pl.ts`
- [ ] 6.6 No Google Fonts reference in the build
- [ ] 6.7 The reduced-motion block exists

#### Manual

- [ ] 6.8 Every screen reviewed in PL and EN at 390 px and 1280 px against the design
- [ ] 6.9 Keyboard-only pass on every screen with 44 px targets
- [ ] 6.10 No screen animates with OS reduced motion enabled
- [ ] 6.11 Owner approves the new PL strings
