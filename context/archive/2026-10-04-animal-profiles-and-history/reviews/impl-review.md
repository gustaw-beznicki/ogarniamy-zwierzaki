<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Animal profiles and history

- **Plan**: context/changes/animal-profiles-and-history/plan.md
- **Scope**: Full plan
- **Reviewed phases**: 1, 2, 3, 4
- **Date**: 2026-10-12
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 3 warnings, 5 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | WARNING |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | WARNING |
| Success Criteria | PASS |

Automated checks re-run on 2026-10-12: API restore/build OK, `dotnet test` 216/216, `npm run check` 0 errors, `npm run build` OK, `dotnet ef migrations has-pending-model-changes` reports no changes. All manual items have evidence in `notes/lifecycle-verification.md` (including the production two-account run).

## Findings

### F1 — Onboarding create can duplicate animals

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: apps/web/src/components/FirstAnimalForm.tsx:19
- **Detail**: `finally { setBusy(false) }` runs right after `window.location.assign(...)`, so the button is enabled again while the page navigates and a second click creates a duplicate animal. A lost create response (5xx/network) shows `request_failed` and allows an immediate resubmit. Plan Phase 1 §3 requires "a lost create response offers refresh before another explicit submission" and Phase 3 requires disabling duplicate clicks. `AnimalsList.tsx` already implements this (`createOutcomeUnknown`, busy kept during navigation).
- **Fix**: Reuse the AnimalsList pattern: clear `busy` only in `catch`, and block resubmission after an outcome-unknown create until the owner refreshes.
- **Decision**: FIXED — busy kept during navigation; outcome-unknown create blocks resubmit until a refresh check (reuses AnimalsList pattern)

### F2 — Animal edits do not detect an account switch

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: apps/web/src/lib/api.ts:265-267, apps/web/src/components/AnimalDocuments.tsx:121-122
- **Detail**: `renameAnimal`/`setAnimalActivity` use `accountMutation`, which has no session-account check. If the user signed into another account in another tab, the PUT returns 404 and the profile shows the generic "unavailable" state, with no hint that the account changed. The plan calls for an "account-aware" mutation helper; S-03's `ownerMutation` (api.ts:226) solves exactly this via `/api/me` and `account_changed`. Not a data leak (the server isolates correctly), but a confusing UX for an isolation-related case.
- **Fix**: Route both PUTs through `ownerMutation` with the profile's `accountEmail` and handle `account_changed` in AnimalDocuments like the reading Retry flow.
  - Strength: Reuses an existing, tested helper; consistent account-switch handling across owner routes.
  - Tradeoff: AnimalDocuments must get the session email; one extra `/api/me` call only on 404.
  - Confidence: HIGH — identical pattern already used for `retryDocumentReading`.
  - Blind spot: Have not checked whether AnimalDocuments already receives `accountEmail` from its page.
- **Decision**: FIXED — rename/activity PUTs go through ownerMutation with the profile's session email (loaded via /api/me); account_changed shows the 'signed in to a different account' state

### F3 — Files hidden when the last active animal goes inactive during capture

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Plan Adherence
- **Location**: apps/web/src/components/DocumentCaptureForm.tsx:394-400
- **Detail**: `recoverFromInactiveAnimal` keeps files/date in state, but if the refreshed defaults are empty, the `animals.length === 0 && !attempt` branch replaces the whole form with the "no active animals" guidance. The selected files and the `animal_inactive` message become invisible, and following the guidance link to Animals discards them. Plan Phase 2 §2: "animal_inactive refreshes defaults, preserves files/date and asks for an eligible selection". Manual check 2.3 was evidenced with another active animal still present.
- **Fix**: Only show the full-page guidance on initial load; after a rejected capture, keep the form with files visible, show the `animal_inactive` message plus a "create/reactivate in Animals (opens in new tab)" link, and let the user retry once an animal is active again.
  - Strength: Fulfils the plan's "preserves files" contract in the all-inactive edge case.
  - Tradeoff: Small branching in the render; a new-tab link or a "reload animals" button is needed to avoid losing state.
  - Confidence: MED — edge case (two tabs, last active animal), not covered by an automated test.
  - Blind spot: No frontend tests; needs a manual two-tab check.
- **Decision**: FIXED — full-page guidance skipped after an animal_inactive rejection; form keeps files and shows a notice with an Animals new-tab link and a 'refresh animal list' button (new en/pl keys). Two-tab check passed locally 2026-10-12 (Playwright)

### F4 — Malformed rename body returns a generic 400 without a code

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: services/api/Animals/AnimalEndpoints.cs:68-70
- **Detail**: `SetActivityAsync` reads the body manually and returns `invalid_activity`; `RenameAsync` binds `RenameAnimalRequest`, so malformed JSON yields a framework 400 without a `code`. No test covers a malformed rename body.
- **Fix**: Either parse the rename body like the activity route (e.g. `name_required` for malformed body) or document the difference in docs/architecture.md.
- **Decision**: FIXED — rename body read by hand; malformed/wrong-type body answers 400 name_required; test A_malformed_rename_body_gets_400_with_a_code added (AnimalVersioningTests 25/25)

### F5 — Architecture doc describes the update as a "single conditional write"

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: docs/architecture.md:34
- **Detail**: The code does `SELECT … FOR UPDATE`, compare, then write. Also, two same-version no-op edits both succeed, so "exactly one succeeds" is not strictly true.
- **Fix**: Reword to "serialized by a row lock; the first changing edit gets a new version and later edits with the old version answer 412".
- **Decision**: FIXED — reworded to row-lock serialization; also documented the F4 rename code and the F2 account-changed behaviour

### F6 — Animal ownership queries do not check the Owner role

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: services/api/Animals/OwnedAnimals.cs:110-118, 132-137
- **Detail**: `ForUser` and `LockOwnedAsync` filter `animal_members` by `user_id` only, while `OwnedDocuments.OwnerMemberships` also requires `Role == Owner`. Harmless today (only the Owner role exists), but a future caretaker role would be able to rename/inactivate and start uploads.
- **Fix**: Add the owner-role predicate in both places.
- **Decision**: FIXED — owner-role predicate added to ForUser (LINQ) and LockOwnedAsync (FOR UPDATE SQL); animal tests 56/56

### F7 — Mutation response re-read after commit

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: services/api/Animals/OwnedAnimals.cs:103-104
- **Detail**: `MutateAsync` returns `FindAsync` after `CommitAsync`, outside the lock; a concurrent edit committed in between appears in the response. No update is lost.
- **Fix**: Optional: project the result inside the transaction before committing.
- **Decision**: FIXED — response projected before CommitAsync, under the row lock; animal tests 56/56

### F8 — No translated message for new error codes / token rejection

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: apps/web/src/i18n/en.ts, apps/web/src/i18n/pl.ts
- **Detail**: `version_required`, `invalid_version`, `invalid_activity` and `invalid_antiforgery_token` fall back to `request_failed`. After a token rejection on a profile edit, nothing tells the user that clicking again works (the reading flow has `readingTokenRejected`).
- **Fix**: Add a short "try again" message for the token-rejected case in en/pl.
- **Decision**: FIXED — invalid_antiforgery_token message added in en/pl (shown by AnimalNameForm.errorMessage on create/rename/activity); version_required/invalid_version/invalid_activity left on the generic fallback (client never sends them malformed)
