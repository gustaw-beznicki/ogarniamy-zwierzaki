# Ogarniamy Zwierzaki

> A private, searchable archive for veterinary documents.

[![Astro](https://img.shields.io/badge/Astro-7-BC52EE?logo=astro&logoColor=white)](https://astro.build/)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Project status](https://img.shields.io/badge/status-early_development-orange)](#status)

Ogarniamy Zwierzaki helps pet owners keep veterinary records from different clinics in one place and find the right document by meaning, not only by filename or exact wording. Owners upload photos and PDFs; the original always stays the source of truth, and search returns the matching source fragments, never generated medical advice.

## Status

The project is in early development. What runs today:

- An Astro frontend and an ASP.NET Core API, deployed to Azure by CI/CD.
- A PostgreSQL database that the API migrates at startup; `GET /api/health` is healthy only when the database is reachable.
- An integration test suite that runs against a real PostgreSQL container.

Everything else in the MVP is planned, not built yet:

- Owner accounts with strict isolation between users.
- Animal profiles and per-animal document views.
- Photo and PDF upload with an editable veterinary-event date, stored privately.
- Text extraction from PDFs and OCR for scans and photos.
- Semantic search across document content, optionally filtered by animal, showing the matching fragment and opening the original.

The requirements, guardrails and non-goals are in the [PRD](context/foundation/prd.md); the delivery order is in the [roadmap](context/foundation/roadmap.md).

## Architecture

```text
Browser
   |
   v
Azure Static Web Apps (Standard)        apps/web      Astro, static output
   |  /api/*  same-origin proxy (linked backend)
   v
Azure App Service (Linux)               services/api  ASP.NET Core 10
   |  Entra token of the App Service managed identity
   v
Azure Database for PostgreSQL           Flexible Server 17, Entra-only auth
```

- The page and `/api/*` share one origin, so there is no CORS configuration. The App Service accepts traffic only through the Static Web App.
- The API never stores a database password in Azure: it signs in with its managed identity. Locally and in tests it uses an ordinary password connection string.
- Planned additions: private blob storage for originals, OCR, pgvector search, and background indexing.

## Repository layout

```text
apps/web/            Astro frontend (strict TypeScript)
services/api/        ASP.NET Core API; EF Core model and migrations in Data/
services/api.Tests/  xUnit integration tests (WebApplicationFactory + Testcontainers)
infra/               Bicep templates and deploy.sh (lint / what-if / apply)
  bootstrap/         One-time, hand-applied setup: CI identities, resource group, budget
scripts/smoke.sh     End-to-end check of a deployed environment
compose.yaml         Local PostgreSQL
context/             Product and planning documents (PRD, stack, roadmap, change plans)
.github/workflows/   ci.yml (pull requests), deploy.yml (main)
```

## Local development

### Prerequisites

- [Node.js](https://nodejs.org/) 22.12 or newer, with npm
- [.NET SDK](https://dotnet.microsoft.com/download) 10
- [Docker](https://docs.docker.com/get-docker/) with Compose (local database and tests)
- EF Core CLI, only for creating migrations: `dotnet tool install --global dotnet-ef`

### 1. Start PostgreSQL

```bash
cp .env.example .env          # then set POSTGRES_PASSWORD to any local password
docker compose up -d
```

If port 5432 is already taken on your machine, set `POSTGRES_PORT` in `.env` (for example `5433`) and use that port in the connection string below.

### 2. Point the API at it

The connection string is kept in [.NET user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets), outside the repository. Use the password from `.env`:

```bash
dotnet user-secrets set ConnectionStrings:Default \
  "Host=localhost;Port=5432;Database=ogarniamy;Username=postgres;Password=<password from .env>" \
  --project services/api
```

### 3. Run the API and the web app

```bash
dotnet run --project services/api       # http://localhost:5180, applies migrations on start
```

```bash
cd apps/web
npm ci
npm run dev                             # http://localhost:4321
```

The Astro dev server proxies `/api/*` to `http://localhost:5180`, just like Static Web Apps does in Azure. Set `API_PROXY_TARGET` to proxy elsewhere. Check the stack with `curl http://localhost:5180/api/health`, which should return `{"status":"ok"}`.

### Configuration

| Key | Values | Purpose |
| --- | --- | --- |
| `ConnectionStrings:Default` | Npgsql connection string | Required. Includes a password only in `Password` mode. |
| `Database:Auth` | `Password` (default), `AzureManagedIdentity` | How the API authenticates to PostgreSQL. Azure sets `AzureManagedIdentity` through App Service settings. |

Secrets never go into tracked files: local passwords live in `.env` (gitignored) and user secrets; Azure uses managed identities.

### Database migrations

The API applies pending migrations at startup and will not start without a reachable database. After changing the EF Core model, add a migration:

```bash
dotnet ef migrations add <Name> --project services/api --output-dir Data/Migrations
dotnet ef database update --project services/api     # optional: apply without starting the API
```

Migrations are forward-only in practice: deployed data is not rolled back with code, so a schema change must stay compatible with the previous release (expand first, contract later).

## Tests and checks

```bash
dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj   # needs Docker running
dotnet build services/api/ogarniamy-zwierzaki-api.csproj
npm run build --prefix apps/web
infra/deploy.sh lint                                                  # needs the Azure CLI with Bicep
```

The tests boot the real API against a throwaway PostgreSQL 17 container, so no mocks or local database are involved. Pull-request CI runs all of the above plus an infrastructure `what-if`.

## Deployment

Everything in Azure is declared in Bicep under [`infra/`](infra) and deployed by GitHub Actions. CI signs in to Azure with GitHub OIDC; the repository and GitHub hold no credentials, only non-secret identifiers.

- **Pull request** ([`ci.yml`](.github/workflows/ci.yml)): builds and tests both components, lints Bicep, and runs `infra/deploy.sh what-if` with a read-only identity so the infrastructure diff is visible in the run log. Pull requests from forks skip the Azure steps.
- **Merge to `main`** ([`deploy.yml`](.github/workflows/deploy.yml)): after approval in the `production` environment, runs the tests, applies the Bicep, deploys the API and the web app, and finishes with [`scripts/smoke.sh`](scripts/smoke.sh). Deployments run one at a time and are never cancelled mid-run.

The same infrastructure commands work locally after `az login`: `infra/deploy.sh lint`, `infra/deploy.sh what-if`.

### Environment

| Resource | Details |
| --- | --- |
| Resource group | `rg-ogarniamy-mvp` in `swedencentral` |
| API | App Service Linux, F1 by default (`appServiceSku` in [`mvp.bicepparam`](infra/environments/mvp.bicepparam)); F1 has cold starts and a daily CPU quota |
| Web | Static Web Apps Standard; resource in `eastus2`, content served globally; PR preview environments disabled because linked backends do not support them |
| Database | PostgreSQL Flexible Server 17, Burstable B1ms, 32 GiB; Entra-only authentication with the API identity as administrator; firewall open only to the App Service outbound IPs |

### Setting up a new Azure environment

These steps are done once by a subscription owner, because CI deliberately lacks the permissions for them.

1. **Apply the bootstrap.** [`infra/bootstrap/main.bicep`](infra/bootstrap/main.bicep) creates `rg-ogarniamy-cicd` with two GitHub OIDC identities, the application resource group `rg-ogarniamy-mvp`, the role assignments and the monthly budget. The budget alert address comes only from your shell, because the repository is public:

   ```bash
   export BUDGET_ALERT_EMAIL='<your alert address>'
   az deployment sub create \
     --location swedencentral \
     --name ogarniamy-bootstrap \
     --template-file infra/bootstrap/main.bicep \
     --parameters infra/bootstrap/bootstrap.bicepparam
   ```

2. **Register resource providers** that the resource-group-scoped deploy identity cannot register:

   ```bash
   az provider register --namespace Microsoft.DBforPostgreSQL
   ```

3. **Configure GitHub.** Add repository variables `AZURE_CLIENT_ID` (deploy identity), `AZURE_PR_CLIENT_ID` (PR identity), `AZURE_TENANT_ID` and `AZURE_SUBSCRIPTION_ID` from the bootstrap outputs. Create the `production` environment with a required reviewer and a branch policy allowing only `main`.

What the bootstrap grants:

- **Deploy identity** `id-ogarniamy-github`: Contributor on `rg-ogarniamy-mvp` only, trusted only for jobs in the `production` environment.
- **PR identity** `id-ogarniamy-github-pr`: Reader plus the custom `Ogarniamy What-If` role at subscription scope, trusted only for `pull_request` jobs. `what-if` runs with `--validation-level ProviderNoRbac`, so this identity never needs write access; do not add write actions to fix a `what-if` error.
- **Budget** `budget-ogarniamy-monthly`: 40 per month, alerts at 50 %, 80 % and 100 % actual and 100 % forecasted spend. Its start date cannot be changed after creation.

The federated credentials use GitHub's immutable OIDC subject (`owner@id/name@id`).

### Smoke test

`scripts/smoke.sh <swa-host> <api-host>` checks that the page is served, that `https://<swa-host>/api/health` returns `{"status":"ok"}` (retried for up to 10 minutes to cover cold starts), and that the API's own `*.azurewebsites.net` host refuses direct requests.

### Rollback

Revert the offending commit on `main` and approve the resulting deploy, or re-run an earlier successful `deploy` run. This rolls back code only; the database keeps its current schema, which is why migrations must stay backward compatible.

## Contributing

- Keep browser concerns in `apps/web` and application logic in `services/api`.
- Code, comments and documentation are written in English; Polish text lives only in translation files.
- Never commit credentials or connection strings with passwords.
- Coding-agent guidance is in [`AGENTS.md`](AGENTS.md); the AI-assisted workflow is described in [`docs/10xdevs-agent-workflow.md`](docs/10xdevs-agent-workflow.md).

The project is developed as part of the 10xDevs course; course milestones are tagged `m<module>l<lesson>` (for example `m1l1`).

## Further reading

- [Product requirements](context/foundation/prd.md)
- [Roadmap](context/foundation/roadmap.md)
- [Technology stack](context/foundation/tech-stack.md)
- [Infrastructure decisions and risks](context/foundation/infrastructure.md)
