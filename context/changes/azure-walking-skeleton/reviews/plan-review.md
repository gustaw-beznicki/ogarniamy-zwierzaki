<!-- PLAN-REVIEW-REPORT -->
# Plan Review: Azure Walking Skeleton Implementation Plan

- **Plan**: context/changes/azure-walking-skeleton/plan.md
- **Mode**: Deep
- **Date**: 2026-09-27
- **Verdict**: REVISE (SOUND after triage fixes)
- **Findings**: 1 critical, 4 warnings, 2 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| End-State Alignment | PASS |
| Lean Execution | PASS |
| Architectural Fitness | PASS |
| Blind Spots | WARNING |
| Plan Completeness | FAIL |

## Grounding
Grounding: 7/7 paths ✓, 4/4 symbols ✓, brief↔plan ✓, Progress↔Phase ✓

## Findings

### F1 — Bootstrap role-assignment name won't compile

- **Severity**: ❌ CRITICAL
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Completeness
- **Location**: Phase 2 §1 — infra/bootstrap/main.bicep
- **Detail**: The contract names the role assignment `guid(subscription().id, identity principalId, roleId)`, and principalId comes from a module output. Bicep rejects this as BCP120 (Azure/bicep#6093). `principalType: 'ServicePrincipal'` is also missing, so a new identity can hit PrincipalNotFound.
- **Fix**: Seed guid() from strings known at deployment start and set principalType: 'ServicePrincipal'.
- **Decision**: FIXED

### F2 — Budget with a fixed startDate may break deploys from October

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Blind Spots
- **Location**: Critical Implementation Details; Phase 2 §2 budget.bicep
- **Detail**: CI re-applies the budget on every merge. startDate cannot be updated, and a past date may be rejected outside the current month (azure-quickstart-templates#7095).
- **Fix A ⭐ Recommended**: Move the budget into the one-time bootstrap template.
  - Strength: CI never re-PUTs the budget, which also fixes F3.
  - Tradeoff: Budget changes require a manual bootstrap re-run.
  - Confidence: HIGH — the bootstrap is already applied locally by the Owner.
  - Blind spot: A bootstrap re-run in a later month has the same startDate constraint (documented in Migration Notes).
- **Fix B**: deploy.sh reads the live startDate and passes it as an override.
  - Strength: The budget stays in CI.
  - Tradeoff: More script logic, and it relies on unconfirmed ARM behaviour.
  - Confidence: MED — the sources conflict.
  - Blind spot: The behaviour on a re-PUT with a past date is unverified.
- **Decision**: FIXED (Fix A)

### F3 — Alert email is printed in the public Actions logs

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Blind Spots
- **Location**: Phase 3 §2 ci.yml, §3 deploy.yml; Critical Implementation Details (Public repo)
- **Detail**: `az deployment sub what-if` (FullResourcePayloads) prints contactEmails. The repo's Actions logs are public, and a repository variable is not masked.
- **Fix A ⭐ Recommended**: The same fix as F2 Fix A, so the email never enters CI.
  - Strength: One change fixes two findings.
  - Tradeoff: As in F2 Fix A.
  - Confidence: HIGH.
  - Blind spot: None significant.
- **Fix B**: Store the email as a secret and mask it with ::add-mask::.
  - Strength: The budget stays in CI.
  - Tradeoff: Breaks "no repository secrets" and relies on masking.
  - Confidence: MED.
  - Blind spot: Other encodings of the value may slip past the mask.
- **Decision**: FIXED (Fix A)

### F4 — PR identity has Contributor, which bypasses the production gate

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Blind Spots
- **Location**: Phase 2 §1 federated credentials; brief → Open Risks
- **Detail**: The `pull_request` and `environment:production` subjects shared one identity with subscription Contributor. A branch PR could edit ci.yml and mutate Azure without approval.
- **Fix A ⭐ Recommended**: Accept the risk explicitly (solo repo).
  - Strength: Zero work.
  - Tradeoff: The hole opens as soon as another collaborator gets push access.
  - Confidence: HIGH.
  - Blind spot: None significant.
- **Fix B**: A separate PR identity with Reader plus a custom what-if role.
  - Strength: The PR path cannot mutate anything.
  - Tradeoff: More bootstrap work, and a custom role definition needs Owner.
  - Confidence: MED — the exact what-if permission set is unverified.
  - Blind spot: What-if may need extra actions (Migration Notes cover adding them).
- **Decision**: FIXED (Fix B)

### F5 — The email leak check passes without checking anything

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Completeness
- **Location**: Phase 2 → Automated Verification (2.5)
- **Detail**: `git grep` skips untracked files, and infra/ is untracked when the check runs.
- **Fix**: Use `! grep -rnE '<regex>' infra/`.
- **Decision**: FIXED

### F6 — Idempotency of re-PUTting linkedBackends is unverified

- **Severity**: OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Blind Spots
- **Location**: Phase 2 §2 static-web-app.bicep; step 3.9
- **Detail**: The predecessor resource userProvidedFunctionApps was not idempotent (static-web-apps#659, #458). Nothing was found either way for linkedBackends.
- **Fix**: Add a Migration Notes fallback (a `linkBackend` bool param).
- **Decision**: FIXED

### F7 — .http contract uses a different variable name

- **Severity**: OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Completeness
- **Location**: Phase 1 §1 — ogarniamy-zwierzaki-api.http
- **Detail**: The contract says `{{host}}`, but the file uses `@ogarniamy_zwierzaki_api_HostAddress`.
- **Fix**: Keep the existing variable and change only its value and the path.
- **Decision**: FIXED
