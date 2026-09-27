# Ogarniamy Zwierzaki

> A private, searchable archive for veterinary documents.

[![Astro](https://img.shields.io/badge/Astro-7-BC52EE?logo=astro&logoColor=white)](https://astro.build/)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Project status](https://img.shields.io/badge/status-early_development-orange)](#current-status)

Ogarniamy Zwierzaki helps pet owners keep veterinary records from different clinics in one place and find the right document by meaning, not only by filename or exact wording. The planned MVP accepts photos and PDFs, keeps each original as the source of truth, and returns matching source fragments instead of generating medical advice.

> [!IMPORTANT]
> The repository currently contains a verified frontend and API scaffold, wired into an Azure walking skeleton (a health check end to end) with automated CI/CD. Authentication, document storage, OCR, semantic search, and the remaining product flows described below are planned but not implemented yet.

## Planned MVP

- Owner accounts with strict isolation between users.
- Multiple animal profiles and per-animal document views.
- Photo and PDF upload with an editable veterinary-event date.
- Private storage of the original document.
- Text extraction from digital PDFs and OCR for scans or photos.
- Semantic search across document content, optionally filtered by animal.
- Search results that show the matching fragment and open the original.

The product deliberately returns documents and quoted source fragments—not generated diagnoses, summaries, or other medical conclusions.

## Current status

| Area | Location | State |
| --- | --- | --- |
| Web | [`apps/web`](apps/web) | Astro 7 page with strict TypeScript that calls `/api/health` and shows the API status |
| API | [`services/api`](services/api) | ASP.NET Core 10 Web API exposing `GET /api/health`, with development OpenAPI output |
| Infrastructure | [`infra`](infra) | Bicep for the Azure walking skeleton (App Service F1, Static Web Apps Standard with a linked backend) and a one-time bootstrap for CI identities and the budget |
| CI/CD | [`.github/workflows`](.github/workflows) | Pull-request builds, Bicep lint and `what-if`; gated deployment on merge to `main`, ending with a smoke test ([`scripts/smoke.sh`](scripts/smoke.sh)) |
| Product definition | [`context/foundation/prd.md`](context/foundation/prd.md) | MVP requirements, guardrails, non-goals, and open questions documented |
| Technical direction | [`context/foundation/tech-stack.md`](context/foundation/tech-stack.md) | Astro + ASP.NET Core split, with Azure App Service recorded as the deployment target |
| Scaffold verification | [`context/changes/bootstrap-verification/verification.md`](context/changes/bootstrap-verification/verification.md) | Both components scaffolded and build-verified |

The frontend reaches the API through the Static Web Apps `/api` proxy (and the Astro dev proxy locally). No database, document storage, authentication, or product feature is present yet.

## Architecture

The current source layout establishes two independently runnable components. The downstream services are the planned direction from the project documents.

```text
apps/web (Astro + TypeScript)        -> Azure Static Web Apps (Standard)
              |  /api/* (same-origin proxy, linked backend)
              v
services/api (ASP.NET Core)          -> Azure App Service (Linux)
              |
              +-- private original-document storage    [planned]
              +-- PDF text extraction / OCR            [planned]
              +-- PostgreSQL + pgvector                 [planned]
              +-- background indexing                  [planned]
```

## Repository structure

```text
.
├── apps/
│   └── web/                         # Astro frontend
├── services/
│   └── api/                         # ASP.NET Core API
├── infra/
│   ├── main.bicep                   # Subscription-scope entry point (resource group, API, web)
│   ├── modules/                     # App Service, Static Web App, and budget modules
│   ├── environments/mvp.bicepparam  # Non-secret region and SKU parameters
│   ├── bootstrap/                   # One-time, hand-applied CI identities and budget
│   └── deploy.sh                    # lint / what-if / apply, shared by local runs and CI
├── .github/
│   └── workflows/                   # ci.yml (pull requests) and deploy.yml (main)
├── scripts/
│   └── smoke.sh                     # End-to-end smoke test of the deployed skeleton
├── context/
│   ├── foundation/                  # PRD, stack decisions, and scaffold adapters
│   └── changes/                     # Change-scoped plans and verification records
├── AGENTS.md                        # Repository guidance for coding agents
├── CLAUDE.md                        # Claude-specific project guidance
└── README.md
```

## Getting started

### Prerequisites

- [Node.js](https://nodejs.org/) 22.12 or newer and npm
- [.NET SDK](https://dotnet.microsoft.com/download) 10
- [Git](https://git-scm.com/)

Clone the repository:

```bash
git clone git@github.com:gustaw-beznicki/ogarniamy-zwierzaki.git
cd ogarniamy-zwierzaki
```

### Run the web app

```bash
cd apps/web
npm ci
npm run dev
```

Astro serves the app at `http://localhost:4321` by default. In development, Astro proxies `/api/*` to the local API at `http://localhost:5180`, mirroring the Static Web Apps proxy; set `API_PROXY_TARGET` to point it elsewhere. Without a running API the page shows `API: unavailable`.

### Run the API

From another terminal at the repository root:

```bash
dotnet restore services/api/ogarniamy-zwierzaki-api.csproj
dotnet run --project services/api/ogarniamy-zwierzaki-api.csproj
```

The development launch profile listens on the fixed port `http://localhost:5180`, which the Astro dev proxy targets. The API exposes `GET /api/health`, which returns `{"status":"ok"}`; its OpenAPI document is available in development mode.

## Verification

Build both components independently:

```bash
npm ci --prefix apps/web
npm run build --prefix apps/web
dotnet restore services/api/ogarniamy-zwierzaki-api.csproj
dotnet build services/api/ogarniamy-zwierzaki-api.csproj --no-restore
```

Automated application tests have not been added yet. The deployed skeleton is checked by `scripts/smoke.sh <swa-host> <api-host>` (see [Deployment](#deployment)).

## Deployment

Deployment to Azure is automated through GitHub Actions and declared in Bicep under [`infra/`](infra). No credentials are stored in the repository or in GitHub secrets: CI authenticates to Azure with GitHub OIDC, and GitHub holds only non-secret identifiers as repository variables.

### Architecture

```text
Browser -> Azure Static Web Apps, Standard (eastus2; static content served globally)
              |  /api/* via linked backend
              v
           Azure App Service, Linux F1 (swedencentral), rg-ogarniamy-mvp
```

- The API is linked to the Static Web App as its backend, so the page and `/api` share one origin and no CORS is configured. Linking also makes the App Service reject direct requests to its own `*.azurewebsites.net` host.
- The Static Web App resource is in `eastus2`: `westeurope` was attempted first and rejected new Static Web Apps customers (`RequestDisallowedByAzure`). The region only affects resource metadata; static content is served globally.
- F1 has no Always On, so the first request after idle is slow, and a daily CPU quota applies. The SKU is the `appServiceSku` parameter in `infra/environments/mvp.bicepparam`.
- Static Web Apps pull-request preview environments are disabled, because linked backends do not work there.

### One-time bootstrap

[`infra/bootstrap/main.bicep`](infra/bootstrap/main.bicep) is applied once by hand, never by CI. It creates `rg-ogarniamy-cicd` with two user-assigned identities trusted by GitHub OIDC, plus a subscription budget:

- **Deploy identity** (`id-ogarniamy-github`): subscription Contributor; its federated credential trusts only jobs in the `production` environment.
- **PR identity** (`id-ogarniamy-github-pr`): Reader plus the `Ogarniamy What-If` custom role; its federated credential trusts only `pull_request` jobs, so it can preview changes but not make them.
- **Budget** (`budget-ogarniamy-monthly`): 20 per month in the billing currency, alerting at 50 %, 80 % and 100 % actual and 100 % forecasted spend.

The repository is public, so the alert recipient is never written to a tracked file or to GitHub. Set it only in your local shell when applying the bootstrap:

```bash
export BUDGET_ALERT_EMAIL='<your alert address>'
az deployment sub create \
  --location swedencentral \
  --name ogarniamy-bootstrap \
  --template-file infra/bootstrap/main.bicep \
  --parameters infra/bootstrap/bootstrap.bicepparam
```

The budget start date (`budgetStartDate`) cannot be changed after the budget is created. Re-running the bootstrap later keeps the original value; if Azure rejects the past date, delete and recreate the budget with the current month's first day rather than moving it into CI.

### GitHub configuration

- Repository variables: `AZURE_CLIENT_ID` (deploy identity), `AZURE_PR_CLIENT_ID` (PR identity), `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID`. Their values come from the bootstrap outputs and the subscription.
- Environment `production` with a required reviewer. The deploy job runs in this environment, and the deploy identity trusts no other subject.

### Workflows

- **Pull requests** ([`ci.yml`](.github/workflows/ci.yml)): builds the web app and the API, lints the Bicep templates, then signs in with the read-only PR identity and runs `infra/deploy.sh what-if` so the infrastructure diff is visible in the run log. The Azure steps are skipped for pull requests from forks, which receive no OIDC token.
- **Merge to `main`** ([`deploy.yml`](.github/workflows/deploy.yml)): a single `deploy` job waits for approval in the `production` environment, then builds both components, applies `infra/deploy.sh apply`, deploys the API zip with `az webapp deploy`, uploads `apps/web/dist` to Static Web Apps with a deployment token fetched at runtime, and runs `scripts/smoke.sh`. Deployments are serialized and never cancelled mid-run. The workflow can also be started manually.

The same infrastructure commands run locally after `az login`:

```bash
infra/deploy.sh lint
infra/deploy.sh what-if
```

### Smoke test

`scripts/smoke.sh <swa-host> <api-host>` checks that the page is served, that `https://<swa-host>/api/health` returns `{"status":"ok"}` (retried for up to 10 minutes to cover cold starts and link propagation), and that the API's direct host refuses the request.

### Rollback

Revert the offending commit on `main` and approve the resulting deploy, which redeploys the previous build, or re-run an earlier successful `deploy` workflow run. The walking skeleton has no data, so no data rollback is involved.

## Project documentation

- [Product requirements](context/foundation/prd.md)
- [Shaping notes](context/foundation/shape-notes.md)
- [Technology stack decision](context/foundation/tech-stack.md)
- [Scaffold adapter manifest](context/foundation/scaffold-adapters/manifest.md)
- [Bootstrap verification log](context/changes/bootstrap-verification/verification.md)
- [AI agent and 10xDevs workflow](docs/10xdevs-agent-workflow.md)

## Course history

This project is developed as part of 10xDevs. Course milestones are marked with Git tags in the form `m<module>l<lesson>`, for example `m1l1`.
