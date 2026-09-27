# Azure Walking Skeleton — Plan Brief

> Full plan: `context/changes/azure-walking-skeleton/plan.md`
> What was built differs in a few places; see the plan's `## Implementation Addendum (2026-09-27)`.

## What & Why

This is roadmap foundation F-01. It puts the empty web front end and API on Azure, deploys them automatically after each merge to `main`, and adds a spending alert. The owner wants to deploy to the real hosting target in week one. Every later slice (S-01 onwards) is then verified on Azure with a smoke test, not only locally.

## Starting Point

`apps/web` is the Astro minimal starter with a placeholder page. `services/api` is the ASP.NET Core template with `/weatherforecast`. There is no `infra/`, no CI and no Azure resource. The subscription is **Pay-As-You-Go with no spending limit** (not a Free Trial), and the GitHub repo is **public**.

## Desired End State

Opening the Static Web Apps URL on a phone or desktop shows the app page with `API: ok`, fetched from `/api/health` through the SWA proxy. The API cannot be reached directly. A PR shows the infrastructure `what-if`. A merge waits for the owner's approval, then applies Bicep, deploys both parts and runs the smoke test. A 20 USD/month budget, created once by the local bootstrap, emails the owner at 50/80/100 % actual and 100 % forecasted spend.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) |
| --- | --- | --- |
| API hosting tier | Linux App Service **F1**, SKU as parameter (B1 fallback) | Zero cost for a skeleton; `alwaysOn` follows the SKU, so moving to B1 for S-03's worker is one line. |
| Front-end hosting | **SWA Standard + linked backend** (`/api/*` proxied) | One origin, no CORS, simpler cookie auth in S-01; about 9 USD/month is accepted. |
| Regions | Backend + future data/AI in **swedencentral**; SWA in **eastus2** | Live checks: only swedencentral has F1, PG B1ms, Document Intelligence and regional embeddings together; polandcentral lacks OCR; SWA cannot be created in swedencentral, and westeurope rejected it with `RequestDisallowedByAzure` (not accepting new customers). |
| Who applies infra | **CI**: `what-if` on PR, apply after merge behind the `production` environment reviewer | Fully automated from day one; human approval is kept as the environment gate. |
| CI identities | Two user-assigned managed identities + GitHub OIDC, created by a one-time local bootstrap: deploy identity with **Contributor** (trusts only the `production` environment), PR identity with **Reader + what-if custom role** (trusts only `pull_request`) | CI cannot create its own identity; no secrets are stored in GitHub; a branch PR cannot bypass the approval gate. |
| Smoke test | Page + `/api/health` via SWA, and the direct API URL must be refused | Proves the whole browser → proxy → API path S-01 will use, plus the linked-backend lock. |
| Budget | Subscription scope, 20 USD/month, 50/80/100 % actual + 100 % forecast, deployed only by the bootstrap | PAYG with no spending limit makes the alert the only spend guard; the start date is immutable and a past date may be rejected, so CI never re-applies it. |
| Alert email location | Local environment variable `BUDGET_ALERT_EMAIL` during the bootstrap only, read with `readEnvironmentVariable` | The repo and its Actions logs are public, and `what-if` prints property values, so the email never enters CI. |

## Scope

**In scope:**
- `/api/health` and an API status page
- Astro dev proxy
- Bicep: bootstrap (two CI identities, what-if custom role, budget), resource group, F1 plan + web app, SWA Standard + linked backend
- `infra/deploy.sh`
- `ci.yml` and `deploy.yml`
- `scripts/smoke.sh`
- README and `infrastructure.md` updates

**Out of scope:**
- Database, storage, queues, Key Vault, OCR and embeddings
- Application Insights
- Authentication and CORS
- Staging slots and PR preview environments
- Custom domain
- Role assignments in `main.bicep`
- Unit test project, Docker, Terraform

## Architecture / Approach

```
Browser ──▶ SWA Standard (eastus2, global static) ──/api/*──▶ App Service F1 Linux .NET 10 (swedencentral)
GitHub PR ──▶ ci.yml: build web+api, bicep lint, what-if (OIDC)
merge main ──▶ deploy.yml [env: production, reviewer] : bicep apply → az webapp deploy → SWA upload → smoke.sh
bootstrap (once, local): rg-ogarniamy-cicd / id-ogarniamy-github (production, Contributor) + id-ogarniamy-github-pr (pull_request, Reader + what-if) + budget
```

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Health endpoint and API status page | `/api/health`, a page that shows API status, local proxy on port 5180 | Low; only the dev-port change touches existing config |
| 2. Bicep infrastructure and bootstrap identity | Linted templates, local `what-if`, OIDC identities + budget + GitHub variables/environment | F1 quota or SWA region restrictions only show at create time (westeurope was rejected; eastus2 used) |
| 3. CI/CD pipeline, smoke test and documentation | PR validation, gated deploy with smoke test, updated docs | Linked-backend propagation delay; link lock surviving re-applies |

**Prerequisites:**
- `az login` as Owner of the subscription (done 2026-09-27)
- `gh` authenticated as the repo owner
- Bicep CLI installed via `az bicep install`

**Estimated effort:** about 2–3 evening sessions across 3 phases.

## Open Risks & Assumptions

- The F1 quota in swedencentral could not be read in advance. If creation fails with `SubscriptionIsOverQuotaForSku`, switch to B1 (about 13 USD/month).
- West Europe refused new SWA customers at create time, so `swaLocation = 'eastus2'` is used. It is unverified whether the SWA → backend proxy hops through the SWA resource region.
- The deploy identity holds subscription Contributor, reachable only from the `production` environment behind the reviewer. The PR identity is read-only plus `what-if`; the PR `what-if` runs with `--validation-level ProviderNoRbac`, so it needs no write permission. Never add write actions to the `Ogarniamy What-If` role or give the PR identity Contributor; add read-level actions only if a read is genuinely missing. Later slices that need RBAC will require an explicit permission expansion.
- Re-applying `linkedBackends` on every deploy is not confirmed idempotent; step 3.9 checks it and the plan's Migration Notes give the fallback.
- The roadmap assumed trial credits, but the subscription is PAYG. Real charges (about 9 USD/month for SWA Standard) start immediately.

## Success Criteria (Summary)

- The deployed page shows `API: ok` on phone and desktop, and the direct API URL is refused.
- A merge to `main`, once approved, redeploys everything and passes the smoke test with no manual Azure commands.
- The subscription budget with four notifications is visible in Cost Management.
