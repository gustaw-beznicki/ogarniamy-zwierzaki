# UI redesign - design reference

Visual reference for the `ui-redesign` change. Exported from the design canvas on 2026-10-12.

Each `.dc.html` file is one screen: plain HTML with inline CSS and a small logic class at the bottom (the design editor's format). Treat it as a visual reference, not code to copy. The `<script src="./support.js">` line, `<x-dc>`, `<helmet>`, `<sc-if>`, `<sc-for>` and `{{...}}` holes belong to the editor and do not exist in this repo.

| File | What it shows |
| --- | --- |
| `Main.dc.html` | Visual system: palette, typography, icon set (SVG paths), motion spec, before/after icon mapping |
| `Messages.dc.html` | Info, warning, error and success messages; toasts; field error; confirmation dialog; page-level error |
| `Mobile-Signin.dc.html` | Sign-in and registration (390 px) |
| `Mobile-Animals.dc.html` | Animal list with the inactive section |
| `Mobile-Profile.dc.html` | Animal profile and document history |
| `Mobile-Add.dc.html` | Document capture, upload progress and saved state |
| `Mobile-Document.dc.html` | Document details, reading (OCR) status in four states, originals |
| `Mobile-Messages.dc.html` | Messages in context: inactive animal warning and success toast |
| `Mobile-Search.dc.html` | Search reference for S-04 / S-05 (idle, loading, results, empty, unavailable) |
| `Desktop-Animals.dc.html` | Animal list with the sidebar shell |
| `Desktop-Document.dc.html` | Document details on desktop |
| `Desktop-Search.dc.html` | Search reference for S-04 / S-05 with a preview pane |

Mock names, dates and medical fragments in these files are sample data. All interface copy that already exists in `apps/web/src/i18n/pl.ts` is used verbatim; anything else is new and needs both `pl.ts` and `en.ts` entries if implemented.
