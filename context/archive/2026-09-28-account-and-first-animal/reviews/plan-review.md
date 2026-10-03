<!-- PLAN-REVIEW-REPORT -->
# Plan Review: Account and First Animal Implementation Plan

- **Plan**: context/changes/account-and-first-animal/plan.md
- **Mode**: Deep
- **Date**: 2026-09-28
- **Verdict**: REVISE (SOUND after triage fixes)
- **Findings**: 0 critical, 4 warnings, 3 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| End-State Alignment | PASS |
| Lean Execution | PASS |
| Architectural Fitness | PASS |
| Blind Spots | WARNING |
| Plan Completeness | WARNING |

## Grounding
Grounding: 10/10 paths ✓, 5/5 symbols ✓ (deployContributor, budgetAmount, uniqueString(subscription().id), id="api-status", deploy.yml output keys), brief↔plan ✓, Progress↔Phase ✓. Risky claims were checked directly against the repo and Microsoft Learn (role-assignment name uniqueness, PostgreSQL public-schema privileges, SWA linked backend).

## Findings

### F1 — The moved role assignment will clash with the old one by name

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Completeness
- **Location**: Phase 1, change 1
- **Detail**: Role assignment names must be unique across the Entra tenant, even at a narrower scope. Reusing `guid(subscription().id, cicdResourceGroupName, deployIdentityName, contributorRoleId)` at resource-group scope fails the bootstrap re-apply while the old assignment still exists.
- **Fix**: Require a name that includes the scope, e.g. `guid(appResourceGroup.id, deployIdentityName, contributorRoleId)`.
- **Decision**: FIXED

### F2 — The $30 budget leaves almost no room above expected spend

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Blind Spots
- **Location**: Phase 1, change 2; brief
- **Detail**: SWA Standard (about $9) plus the database (about $19) is about $28, which is 93 % of $30, so the 50/80 % and forecast alerts would fire every month.
- **Fix**: Budget $40 with the cost breakdown written into the plan, the brief and the infrastructure.md contract.
- **Decision**: FIXED

### F3 — The riskiest assumption (cookie through the SWA proxy) is checked last

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Blind Spots
- **Location**: Phase 3 Manual Verification / Phase 5
- **Detail**: The cookie transport through the SWA linked backend was first checked in Phase 5, after the Phase 4 UI is built on it.
- **Fix**: Manual criterion 3.8: a deployed curl round-trip (register into a cookie jar, then `/api/me` returns 200).
  - Strength: Catches a transport failure before Phase 4, for two curl commands.
  - Tradeoff: One production test account created a phase earlier.
  - Confidence: HIGH — Phase 3 is deployed on its own.
  - Blind spot: curl won't catch browser-only issues such as SameSite handling.
- **Decision**: FIXED

### F4 — Phase 4 removes smoke check 1, but its file list doesn't include smoke.sh

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Completeness
- **Location**: Phase 4, change 5 vs Phase 5, change 1
- **Detail**: The Phase 4 deploy runs `smoke.sh`, which still greps for `id="api-status"`, while Phase 5 claimed to replace it.
- **Fix**: New Phase 4 change 6 replaces check 1 (plus Progress 4.10); Phase 5 only adds the `/api/me` → 401 check.
- **Decision**: FIXED

### F5 — The lockout test boundary contradicts how Identity behaves

- **Severity**: OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Blind Spots
- **Location**: Testing Strategy — Login; Phase 3, change 1
- **Detail**: Identity locks the account on the 5th failure itself; the plan expected `locked_out` from the 6th attempt and said nothing about unknown emails.
- **Fix**: Attempts 1–4 return `invalid_credentials`; the 5th and later return `locked_out`; an unknown email returns `invalid_credentials`.
- **Decision**: FIXED

### F6 — PR what-if cannot list firewall rules built from a runtime value

- **Severity**: OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Completeness
- **Location**: Phase 2 Automated Verification (2.2)
- **Detail**: The firewall-rule loop count comes from the App Service's runtime outbound IPs, which what-if cannot expand.
- **Fix**: 2.2 checks the server, database, administrator and identity; the firewall rules move to manual check 2.8.
- **Decision**: FIXED

### F7 — Data-protection keys: fine on App Service, not for self-hosting

- **Severity**: OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Blind Spots
- **Location**: Critical Implementation Details — Data-protection keys
- **Detail**: The `/home` key ring survives only on App Service; a self-hosted container would sign every owner out on restart.
- **Fix**: `PersistKeysToDbContext<AppDbContext>()` with `IDataProtectionKeyContext` in the Phase 3 migration; the Phase 5 restart check stays.
- **Decision**: FIXED
