# Manual test guide — ui-redesign

Covers every pending Manual row in `plan.md` → `## Progress` (IDs in brackets). About 60–90 minutes in total. Report results as "all pass" or the failing IDs with a short note and a screenshot.

## 0. Setup (once)

1. Start the stack as in `docs/local-development.md`:
   ```bash
   docker compose up -d
   dotnet run --project services/api          # http://localhost:5180
   cd apps/web && npm ci && npm run dev       # http://localhost:4321
   ```
2. Use **Chrome**. Open DevTools → **Device toolbar** (Ctrl+Shift+M). Save two sizes: **390 × 844** ("phone") and **1280 × 800** ("desktop").
3. Prepare test files in one folder:
   - 3 different JPG/PNG photos under 10 MB (`a.jpg`, `b.jpg`, `c.jpg`)
   - 1 small PDF (`doc.pdf`)
   - 1 photo over 10 MB (`big.jpg`), and 1 `.heic` file (any; even a renamed file works)
4. Reduced motion toggle (used several times): DevTools → ⋮ → **More tools → Rendering** → "Emulate CSS media feature prefers-reduced-motion" → `reduce`. Set it back to "No emulation" afterwards.
5. Screen reader (optional but recommended for the aria checks): Orca on Linux (`Super+Alt+S`) or NVDA on Windows. Without one, check the attributes in DevTools → **Elements** / **Accessibility** pane instead — this guide says what to look for.

Polish pages are at `/…`, English at `/en/…`.

---

## 1. Sign-in and registration — `/signin/`

**Phone and desktop:**

- [2.6/1.5] Green hero with the orange paw tile, app name, tagline; white card overlaps the hero; privacy line with a green shield under the card. At desktop the card is centred and at most ~460 px wide.
- [1.6] Zoom to 200 % on the title and tagline: Polish letters (ą, ę, ł, ó, ś, ż) use the same font as the other letters (no thinner/different glyphs).
- [2.7] Language switch (top right) is a translucent pill on the green hero; click EN ↔ PL, each link's box (Elements → hover) is at least 44 px tall.
- [2.8] Click the **Register** tab and back: the orange indicator **slides** (does not jump).

**Field errors [2.8]:**

1. Register with a fresh email and a 10+ character password → you land in onboarding. Sign out (see §3), go back to `/signin/`.
2. Register again with **the same email** → message appears **directly under the email field**, the field gets a red border. In Elements, the email `<input>` has `aria-invalid="true"` and `aria-describedby="…"` pointing at the message's `id`. A screen reader announces the message immediately.
3. Password too short: the browser blocks it (minlength 10). To reach the API message, in Elements delete the `minlength` attribute on the password input, submit a 4-char password on **Register** → message under the **password** field, same aria checks.
4. **Sign in** tab with a wrong password → error appears **above the Submit button** (not under a field).
5. Switch tab after an error → the error disappears.

## 2. Onboarding — `/onboarding/` (fresh account)

- [2.9] White card with paw tile and title. Click Save with an empty name → error **under the name field**; input has `aria-invalid="true"` + `aria-describedby` matching the error's `id`.
- [2.7] Language switch here has the normal (white pill) style.
- Create the first animal, e.g. **Burek**.

## 3. Shell — `/animals/`

**Phone [2.6]:** floating white bottom bar with a ~16 px gap to the screen edges; active tab green with white text; "Add" icon in an orange circle; sign-out icon on the right. Scroll to the very bottom: the last row is fully visible above the bar.

**Desktop [2.6]:** dark green sidebar, active item orange with dark text, sign-out at the bottom. The language switch sits top right of the content and never overlaps the sidebar.

**Sign-out failure [2.10]:** stop the API (Ctrl+C in its terminal), click Sign out.
- Desktop: red Alert inside the sidebar **directly above** Sign out.
- Phone: red Alert at the top of the content.
Start the API again.

**Keyboard [2.11]:** click the address bar, press Tab repeatedly: language switch → brand → nav items → sign-out → page content (order may put content before the bar on phone; it must just be logical). Every focused element has a clearly visible ring — **white on the green sidebar**, green on light backgrounds.

## 4. Animals list — `/animals/`

Add two more animals (**Mruczek**, **Azor**), then open Azor → mark inactive (§5).

- [3.4] Phone: white rows with a coloured initial tile, name, document count and a chevron bubble. Desktop: card grid.
- [3.4] Note each animal's tile colour. Reload; mark another animal inactive and active again → colours of the **active** animals do not change. Inactive animals are grey.
- [3.4] "Inactive" section: click it → opens/closes, the chevron rotates. Inactive rows have a dashed border and an "Inactive" pill.
- Add-animal form: submit an empty name → error under the field (same as onboarding).

## 5. Profile and history — click an animal

- Back link has a white chevron bubble; green hero with avatar, name, status pill with dot, document count.
- [3.5] **Rename:** Edit name → change → Save → green success message under the hero; focus returns to "Edit name" (the ring is on it).
- [3.5] **Conflict (412):** open the same profile in a second tab. In tab 2 rename to "X" and save. In tab 1 click Edit name, type "Y", Save → orange warning with a **Refresh** button; your typed "Y" is still in the field. Click Refresh → updated, the info "draft kept" note appears in the form.
- [3.6] **Mark inactive** → confirm card (archive tile, heading, buttons right-aligned, primary last) → confirm. Hero turns grey; orange warning Alert below it contains **Mark as active**. Click it → green hero returns, focus lands on the activity button.
- [3.8] Unavailable profile: open `/animal-documents/?animalId=00000000-0000-0000-0000-000000000000` → page-level card with an error badge and a working "back to animals" link.

**History [3.7]** — needs **21+ documents** for one animal (one page = 20). Event dates are set in the add form; make the 20th and 21st newest documents share the same month. Quickest: add documents reusing `a.jpg` with dates spread across 3 months, e.g. 10 × this month, 11 × last month. Check:
- Rows grouped under month headings ("październik 2026", "wrzesień 2026" in PL — nominative form, no "września").
- After **Show more**, the new rows join the existing last month: **only one heading per month**.
- Each row: document tile, event date, file count, "Uploaded: <date time>" line.

(Ask me for a small script if you'd rather not click 21 uploads.)

## 6. Add document — `/add/`

**Layout [4.9]:** phone — three source tiles in one row, no horizontal scroll (try scrolling sideways); desktop — looks balanced.

**Error zones [4.4]:**
1. Click Save with nothing selected → `files_required` appears **directly under the source tiles**.
2. Add Photos → `big.jpg` → "too large" error in the same place. Then `.heic` → same place.
3. In Elements, the selection `<section>` has `aria-describedby` = that error's `id`.
4. Pick a photo, set the date to **tomorrow** (type it if the picker blocks it) → error **under the date field**; the date input has `aria-invalid="true"` and `aria-describedby`. A screen reader reads the message when focusing the date input.
5. With no animal selected (if more than one active animal) → error **under the animal chips**.

**Duplicate [4.5]:** Add Photos → `a.jpg`, then Add Photos → `a.jpg` again → **orange warning** (not red), the first photo stays selected.

**Reorder/remove [4.6]:** select `a`, `b`, `c` → up/down/remove buttons are 44 px; first item's "up" and last item's "down" look visibly disabled; reorder and remove work.

**Replace prompt [4.8]:** with photos selected, Choose PDF → `doc.pdf` → orange prompt with **Replace** and **Keep**. Keep → photos stay; repeat and Replace → only the PDF remains.

**Progress [4.7]:** select `a`, `b`, `c`, an animal, Save → white card with a bar with a moving shimmer, then a green tile with a drawn check, then the document page opens. Repeat with **reduced motion = reduce** → no shimmer, check appears already drawn.

## 7. Document page

Open the 3-photo document from §6.

- [5.5] Large page plus a thumbnail strip. Click thumbnail 2 → large image changes; thumbnail 2 has a green border and in Elements `aria-current="true"`. Tab to a thumbnail, press Enter → switches too. **Open**/**Download** open the selected page.
- [5.6] Open a 1-photo document → no thumbnail strip. Open the PDF document → file card with Open PDF / Download PDF.
- [5.7] Desktop: viewer and details side by side. Phone: details → stored pill → reading status → originals, stacked.
- [5.7] Stop the API, reload the document page → the reading status shows a warning but the originals section is still there (start the API again).

**Reading states [5.4]** — locally reading is off, so a new document stays **queued** (grey clock tile, gently breathing). Other states via Chrome **Local Overrides**:

1. DevTools → Network → reload → right-click the `…/reading` request → **Override content** (choose a local folder once and allow access).
2. Replace the body with one of the JSON objects below, save (Ctrl+S), reload the page. Polling every 5 s keeps picking up the override.

```json
{"state":"reading","failureCode":null,"canRetry":false,"retryAfter":null,"completedAt":null}
{"state":"read","failureCode":null,"canRetry":false,"retryAfter":null,"completedAt":"2026-10-12T10:00:00Z"}
{"state":"failed","failureCode":"empty_document","canRetry":true,"retryAfter":null,"completedAt":"2026-10-12T10:00:00Z"}
{"state":"failed","failureCode":"empty_document","canRetry":false,"retryAfter":"2026-10-13T23:00:00Z","completedAt":"2026-10-12T10:00:00Z"}
```

Check per state:
- **reading** — orange OCR tile pulsing, thin indeterminate bar below it, and a **scan line moving over the photo** (only in this state).
- **read** — green tile with a check drawing itself; main line plus a muted date line.
- **failed (canRetry true)** — red tile, red message, muted detail line, **Retry** button (icon rotates on hover). Clicking Retry hits the real API, which refuses for a non-failed document → red error Alert in the panel; that's the expected look.
- **failed (retryAfter set)** — blue info Alert "You can retry again after …".
- **status unavailable** — remove the override, stop the API, reload → orange warning; originals still visible.
- **polling stopped** — leave a queued document open 10+ minutes → blue info "stopped checking" Alert plus Refresh.

Delete the overrides afterwards (Sources → Overrides → clear).

**Reduced motion [5.8]:** with `reduce`, repeat **reading** and **read** overrides → nothing pulses or scans, the check is fully drawn immediately; the failed row does not shake.

## 8. Search placeholder — `/search/`

- Page-level info card with a search badge and "coming soon" text, both widths.

## 9. Full pass [6.8–6.10, 1.5]

Go through every screen in **PL and EN** at **both widths**: sign-in, registration, onboarding, animals, profile (active + inactive), add document, document (photos + PDF), search.

- [6.8/1.5] Matches `context/changes/ui-redesign/design/*.dc.html` (open them in the browser side by side); no horizontal scroll; nothing hidden behind the bottom bar; no unreadable text.
- [6.9] Keyboard only (no mouse): every control reachable, visible focus, logical order, targets at least 44 px (hover elements in DevTools to see the size).
- [6.10] Reduced motion `reduce`: reload each screen — nothing animates (no rise-in of rows, no drifting paws on sign-in, no shimmer).

## 10. Polish copy [6.11]

Approve or correct in `apps/web/src/i18n/pl.ts`:

| Key | PL | EN |
| --- | --- | --- |
| `authTagline` | Prywatne archiwum dokumentów weterynaryjnych | Private archive of veterinary documents |
| `authPrivacyNote` | Oryginały widzi tylko właściciel konta | Only the account owner can see the originals |
| `originalPagesLabel` | Strony | Pages |

## Known, not bugs

- Firefox may show the capture progress bar without the shimmer.
- The "Animal profile" eyebrow above the name is hidden by design of this change (pending owner decision).
- The onboarding title wraps to three lines in Polish at 390 px.
