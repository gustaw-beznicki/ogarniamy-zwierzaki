<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Capture document original

- **Plan**: context/changes/capture-document-original/plan.md
- **Scope**: Phase 4 of 5
- **Reviewed phases**: 4
- **Date**: 2026-10-04
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 3 warnings, 6 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | WARNING |
| Success Criteria | PASS |

Plan adherence: every planned item matches the Phase 3 contract (routes, JSON shapes, `file` multipart field, `X-CSRF-TOKEN`, problem codes). Unplanned `i18n/format.ts`, the `AppShell.tsx` locale-query edit and "Change selection" are justified. Success criteria: astro check (0 errors), build (16 pages) and dotnet test (87/87) pass; manual 4.3–4.5 are pending, deferred by the owner to post-deployment testing.

## Findings

### F1 — "Change selection" after an ambiguous failure can create a duplicate document

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: apps/web/src/components/DocumentCaptureForm.tsx:285-290, 386-389
- **Detail**: "Change selection" is shown next to Retry even for retryable errors. If `complete` succeeded but its response was lost (`upload_interrupted`/`storage_unavailable`), `startOver()` drops the operation ID but keeps the files; saving again creates a second Stored document, which the MVP cannot delete. Contradicts the plan's "retry … never duplicates it" intent within the open form.
- **Fix A ⭐ Recommended**: Show "Change selection" only for non-retryable errors; retryable errors offer Retry only.
  - Strength: Removes the duplicate path entirely; matches the plan's same-operation retry model.
  - Tradeoff: A user stuck on a persistent outage must leave the page to start over.
  - Confidence: HIGH — two-line change at the render site.
  - Blind spot: Depends on F2 so that permanent errors are not classified as retryable.
- **Fix B**: Keep the button, but clear the selection in `startOver()` so the user must re-pick files.
  - Strength: Always leaves an escape route.
  - Tradeoff: Duplicates remain possible if the user re-picks the same files.
  - Confidence: MED — reduces, not removes, the risk.
  - Blind spot: None significant.
- **Decision**: FIXED (Fix A)

### F2 — Permanent errors are shown as retryable; three API codes lack messages

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: apps/web/src/components/DocumentCaptureForm.tsx:42, 279
- **Detail**: Unknown codes and bare `request_failed` collapse into `upload_interrupted`, which is retryable. `unsupported_media_type` (415), `invalid_upload_body` (400) and `invalid_pagination` have no en/pl message, so a permanent 4xx yields a Retry button that always fails.
- **Fix**: Treat only status 0, 5xx and the explicit retryable codes as retryable; map other 4xx to a non-retryable generic message and add the missing keys to en.ts/pl.ts.
- **Decision**: FIXED (non-transient 4xx → new non-retryable `upload_rejected`; `unsupported_media_type` → existing `unsupported_file_type`; `invalid_pagination` left unmapped because only the list uses it and the list sends valid values)

### F3 — Polish word in a non-translation file

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: apps/web/src/i18n/format.ts:12
- **Detail**: Comment example `"5 plików"` violates the lesson "Write context and code in English; Polish only in translation files".
- **Fix**: Rewrite the comment with an English-only example (e.g. "1 file" / "5 files").
- **Decision**: FIXED

### F4 — Insecure context is reported as an unreadable file

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: apps/web/src/components/DocumentCaptureForm.tsx:61, 78, 148-150, 229-233
- **Detail**: `crypto.subtle`/`crypto.randomUUID` exist only in secure contexts; over plain HTTP (phone on a LAN IP) the throw is shown as `file_unreadable`. Production HTTPS is unaffected.
- **Fix**: Check `window.isSecureContext && crypto.subtle` once and show a distinct message.
- **Decision**: FIXED (`secureContext()` check before file selection and Save; new `insecure_context` message)

### F5 — "One type per file" not applied to TypeScript

- **Severity**: 💬 OBSERVATION
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Pattern Consistency
- **Location**: apps/web/src/lib/api.ts; apps/web/src/components/DocumentCaptureForm.tsx:13-44
- **Detail**: api.ts now holds 10+ interfaces plus `ApiError`; the form declares `SelectionError` and local interfaces. Pre-existing web code already kept `MeResponse`, `Animal` and `ApiError` together, so AGENTS.md's rule is ambiguous for TS.
- **Fix A ⭐ Recommended**: Clarify in AGENTS.md that the rule covers C# types; TS modules may group related types.
  - Strength: Matches how apps/web has always been written; no churn.
  - Tradeoff: Two conventions across the repo.
  - Confidence: HIGH — existing code is evidence of practice.
  - Blind spot: Owner's original intent for the rule.
- **Fix B**: Split TS types and classes into their own files.
  - Strength: Literal compliance.
  - Tradeoff: Large churn in api.ts and imports for little gain.
  - Confidence: MED.
  - Blind spot: None significant.
- **Decision**: FIXED (Fix A — AGENTS.md rule scoped to C#)

### F6 — "Add document" on an animal's list does not preselect that animal

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: apps/web/src/components/AnimalDocuments.tsx:75
- **Detail**: Links to plain `/add/`; the form preselects the account default, which may be a different animal than the one being viewed — a mis-assignment risk.
- **Fix**: Pass `?animalId=` and prefer it when it is among the eligible animals.
- **Decision**: FIXED (list links to `/add/?animalId=`; form prefers an eligible requested animal over the default)

### F7 — Small hardening: API-supplied URLs and prototype-key lookup

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: apps/web/src/components/DocumentDetails.tsx:99-101, 152-165; DocumentCaptureForm.tsx:279, 309
- **Detail**: `originalUrl` goes straight into `href`/`img src` (server builds it relative today, so low risk). `code in messages` is true for prototype keys like `toString`.
- **Fix**: Accept only URLs starting with `/api/documents/`; use `Object.hasOwn(messages, code)`.
- **Decision**: FIXED (document with a non-`/api/documents/` original URL shows the error view; capture form uses `Object.hasOwn`)

### F8 — No warning when leaving the page mid-upload

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: apps/web/src/components/DocumentCaptureForm.tsx:245-283
- **Detail**: Navigating away while running silently abandons the operation (orphaned Uploading row). Resuming is out of scope, but a warning is not.
- **Fix**: Register a `beforeunload` prompt while status is `running`.
- **Decision**: FIXED (`beforeunload` prompt while running; the post-completion redirect is exempt)

### F9 — `loadingContent` vs existing `loading` message

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: apps/web/src/components/AnimalDocuments.tsx, DocumentDetails.tsx
- **Detail**: New components use `messages.loadingContent`; AnimalsList and SessionGate use `messages.loading`.
- **Fix**: Reuse `messages.loading` if the texts mean the same; otherwise leave as is.
- **Decision**: DISMISSED (`loading` is "Loading your account…", `loadingContent` is generic; different meanings)
