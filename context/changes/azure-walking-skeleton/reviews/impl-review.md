<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Azure Walking Skeleton

- **Plan**: context/changes/azure-walking-skeleton/plan.md
- **Scope**: Full plan (Phase 3 reviewed before merge; its manual checks are still pending)
- **Reviewed phases**: 1, 2, 3
- **Date**: 2026-09-27
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 6 warnings, 3 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | WARNING |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | WARNING |

Notes:
- **Plan adherence**: the code matches every contract. The four deviations (SWA in eastus2, `identities.bicep`, the immutable OIDC subject, what-if with `ProviderNoRbac`) are documented in `change.md` and applied consistently across the Bicep, parameters, scripts and user-facing docs. The drift is in the documentation (F6).
- **Success criteria**: all automated criteria were re-run on 2026-09-27 and pass (1.1–1.4, 2.1–2.5, 3.1–3.4; PR #1 checks web/api/infra are green). Manual checks 3.5–3.9 are still pending.

## Findings

### F1 — `production` environment accepts any branch

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: GitHub environment `production` (used by .github/workflows/deploy.yml:24)
- **Detail**: The deploy identity (subscription Contributor) trusts `…:environment:production`, and the environment has `deployment_branch_policy: null`. There are two ways to reach Contributor with code that is not on main: `workflow_dispatch` of a modified `deploy.yml` on any branch, or a same-repo PR branch that adds `environment: production` to a job. The only thing that stops either is the reviewer approval. The approval screen shows the environment name, not the branch diff.
- **Fix**: Restrict `production` to deployments from `main` (custom branch policy `main`, no tags) through the environments API, and document it in the README next to the reviewer requirement.
- **Decision**: FIXED — `production` limited to branch `main` (custom deployment branch policy, reviewer kept); README line 173 updated

### F2 — Personal email address in a public tracked file

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: context/changes/azure-walking-skeleton/plan.md:14
- **Detail**: The Current State Analysis recorded the `az` login address (a personal address). It is committed (866c034) and pushed to the public branch `azure-walking-skeleton-p3`. It is not the budget address, but it conflicts with the plan's own rule "No credentials or personal data are in tracked files". The CI email grep does not catch it because that grep only covers `infra/`. Redacting it now keeps it out of `main`'s tip, but it stays in the history unless the history is rewritten.
- **Fix**: Replace it with "the signed-in `az` account" in plan.md:14.
- **Decision**: FIXED — plan.md:14 redacted; the address remains in the history of commit 866c034

### F3 — Deploy identity has subscription-wide Contributor, including over its own trust

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: infra/bootstrap/main.bicep:63-70
- **Detail**: Contributor at subscription scope also covers `rg-ogarniamy-cicd`. A compromised deploy job could therefore add federated credentials (new trusted subjects) to either identity, change or delete the budget, and create resources anywhere in the subscription. The plan chose subscription Contributor deliberately, because `main.bicep` creates the resource group at subscription scope.
- **Fix A ⭐ Recommended**: Accept for F-01 and record the trade-off in infrastructure.md, with a follow-up to narrow the scope when S-01 adds RBAC.
  - Strength: No change to working CI; the risk is bounded by the `production` reviewer gate, and after F1 also by main-only deployments.
  - Tradeoff: The persistence path through the federated credentials remains until the follow-up.
  - Confidence: HIGH — the plan explicitly defers RBAC work to the first slice that needs it.
  - Blind spot: Collaborators who might be added later.
- **Fix B**: Create `rg-ogarniamy-mvp` in the bootstrap, scope Contributor to that group, and give the subscription only a custom role for `Microsoft.Resources/deployments/*` and `resourceGroups/read`.
  - Strength: Removes the self-escalation path and the budget tampering now.
  - Tradeoff: Requires a bootstrap re-run, and main.bicep becomes resource-group scoped or must stop creating the group. The Phase 3 CI would have to be verified again.
  - Confidence: MED — resource-group creation and SWA linking permissions need a real what-if and apply to confirm.
  - Blind spot: Whether the linked backend needs permissions across the resource group.
- **Decision**: FIXED via Fix A — trade-off recorded in infrastructure.md; S-01 narrowing queued in follow-ups/review-fixes.md

### F4 — Budget alert email is stored in deployment history and readable by the PR identity

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: infra/bootstrap/main.bicep:16-17, infra/modules/budget.bicep:10-11
- **Detail**: `budgetContactEmail` is a plain string parameter, so its value is kept in the `ogarniamy-bootstrap` deployment history. The PR identity has subscription Reader. A same-repo PR that edits `ci.yml` could print it into a public Actions log (`az deployment sub show`, or the budget's `contactEmails`). Only people with write access can do this, so the risk is low for a solo repo.
- **Fix**: Mark `budgetContactEmail` `@secure()` in both files and re-run the bootstrap once. The budget resource itself stays readable by Reader; accept that.
- **Decision**: FIXED — `@secure()` on budgetContactEmail in bootstrap/main.bicep and modules/budget.bicep; bootstrap re-run by the user replaces the stored deployment record

### F5 — jq failures hidden when parsing deploy outputs

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: .github/workflows/deploy.yml:69-73
- **Detail**: `echo "key=$(jq -er …)"` returns echo's exit status (0), so a missing output writes an empty value to `GITHUB_OUTPUT`. The failure then shows up later as a confusing `az webapp deploy` or smoke error instead of at the parse step.
- **Fix**: Assign each value first (`rg="$(jq -er …)"`, which fails under `bash -e`), then echo the variables into `GITHUB_OUTPUT`.
- **Decision**: FIXED — outputs assigned to variables before writing GITHUB_OUTPUT

### F6 — The plan, the brief and infrastructure.md contradict what was implemented

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Plan Adherence
- **Location**: context/changes/azure-walking-skeleton/plan.md:246, 257, 348, 363, 417; plan-brief.md:23, 53, 78; context/foundation/infrastructure.md:18, 117, 141, 170-191, 203; .github/workflows/ci.yml:50
- **Detail**:
  - plan.md and plan-brief.md still say:
    - SWA runs in westeurope.
    - `githubRepo` has the form owner/name.
    - What-if runs without `ProviderNoRbac`.
  - Most misleading: the plan's Migration Note (plan.md:417, repeated in plan-brief.md:78) tells a future agent to fix an `AuthorizationFailed` by adding actions to the what-if role. This change showed that doing so gives the PR identity write access.
  - infrastructure.md keeps these leftovers from the research phase:
    - trial credits and the trial subscription
    - budgets placed in `monitoring.bicep`
    - a local `--confirm-with-what-if` apply flow
    - "CI/CD out of scope"
  - The README does not mention the immutable subject or `ProviderNoRbac`, so someone could "simplify" them away.
  - The ci.yml:50 comment still shows the subject as owner/name.
- **Fix**: Add an "Implementation addendum" section to plan.md and plan-brief.md that lists the four adaptations and replaces the Migration Note. Clean up the stale infrastructure.md paragraphs. Add a short "why" note in the README Deployment section for the immutable subject and `ProviderNoRbac`. Correct the ci.yml comment.
  - Strength: Future `/10x-plan` and `/10x-implement` runs read these files as ground truth, and the addendum keeps the reviewed phase blocks intact.
  - Tradeoff: Documentation edits in 4 files; the ci.yml comment change re-runs CI.
  - Confidence: HIGH — every stale line was checked against the implemented files.
  - Blind spot: None significant.
- **Decision**: FIXED — Implementation Addendum in plan.md (supersedes the Migration Note), plan-brief.md, infrastructure.md leftovers and README "why" corrected, ci.yml comment fixed

### F7 — Workflow hardening gaps

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: .github/workflows/ci.yml:9-11; .github/workflows/deploy.yml:79-105; all `uses:` lines
- **Detail**:
  - `id-token: write` is granted at workflow level, so the `web` and `api` jobs, which run npm and dotnet code from the PR, could mint PR-identity tokens.
  - `${{ steps.*.outputs.* }}` is expanded directly inside `run:` scripts. The values come from trusted deployment outputs, but GitHub advises against the pattern.
  - An empty SWA token is not checked.
  - The actions are pinned to tags, including `azure/login` and `static-web-apps-deploy`, which run with deploy credentials.
- **Fix**: Move `id-token: write` to the `infra` job, pass step outputs through `env:`, fail on an empty token, and pin `azure/login` and `Azure/static-web-apps-deploy` to commit SHAs.
- **Decision**: FIXED — id-token only on the ci.yml infra job; deploy.yml step outputs via env:; empty-token guard; azure/login (7184910) and static-web-apps-deploy (4d27395) pinned to SHAs

### F8 — Smoke check 3 accepts any non-200 response

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Success Criteria
- **Location**: scripts/smoke.sh:57-60, 29
- **Detail**: "Direct access refused" also passes on 000 (DNS or TLS failure), 404 and 5xx, so a stopped or broken backend looks like a correct lock. `2>/dev/null` also cancels out `--show-error`, so the reasons for curl failures never reach the log.
- **Fix**: Require 401 or 403 for check 3 (once 3.7 confirms the actual code) and let curl's stderr through to the log.
- **Decision**: FIXED — check 3 requires 401/403; curl stderr is no longer discarded (to be confirmed against the real code in manual check 3.7)

### F9 — Idempotency of the linkedBackends re-PUT is unverified

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Success Criteria
- **Location**: infra/modules/static-web-app.bicep:24-31
- **Detail**: Every deploy re-PUTs `linkedBackends/api`. It is unknown whether Azure treats an identical PUT as a no-op or as a relink that briefly resets the App Service auth config. The plan's Migration Notes already describe the fallback (`linkBackend` param). Manual check 3.9 covers this.
- **Fix**: Confirm during 3.9; if the second apply fails or the direct URL stops being refused, apply the `linkBackend` fallback.
- **Decision**: FIXED — verification queued in follow-ups/review-fixes.md, tied to manual check 3.9
