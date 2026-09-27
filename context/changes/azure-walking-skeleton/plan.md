# Azure Walking Skeleton Implementation Plan

## Overview

Deliver roadmap foundation F-01: the empty Astro front end and the ASP.NET Core API run on Azure, the web page reaches the API through the Static Web Apps `/api` proxy, every merge to `main` re-applies infrastructure and redeploys both components behind a human approval gate, and a subscription budget alert guards against unexpected spend. Nothing product-specific (auth, data, storage) is added; those belong to S-01..S-03.

## Current State Analysis

- `services/api/Program.cs` is the untouched `webapi` template: OpenAPI in Development, `UseHttpsRedirection`, and the sample `GET /weatherforecast` (`services/api/Program.cs:17-35`).
- `services/api/Properties/launchSettings.json` binds dynamic ports (`http://localhost:0`), so a front-end dev proxy has no stable target.
- `apps/web` is the Astro 7 minimal starter: `astro.config.mjs` is empty and `src/pages/index.astro` is a placeholder "Astro" page.
- There is no `infra/`, no `.github/workflows/`, no `scripts/`.
- `context/foundation/infrastructure.md` prescribes Bicep as the sole IaC source, lint + `what-if` before every mutation, human approval, GitHub OIDC with no stored secrets, App Service for the API and Static Web Apps for Astro. Its layout (`infra/main.bicep`, `infra/modules/*`, `infra/environments/mvp.bicepparam`) is adopted here, limited to the modules F-01 needs.
- Live subscription checks (2026-09-27, `az` as `gustaw.beznicki@gmail.com`, subscription `Basic subscription`):
  - Offer is `PayAsYouGo_2014-09-01` with spending limit **Off**, not a Free Trial. The budget alert is the only spend guard.
  - No `sys.blockwesteurope` or allowed-locations policy assignment (only `SecurityCenterBuiltIn`).
  - `Microsoft.Web/staticSites` locations: Central US, East US 2, West US 2, West Europe, East Asia.
  - Linux F1 is offered in swedencentral, westeurope, polandcentral and more. `DOTNETCORE|10.0` (LTS) is an active Linux runtime.
  - swedencentral has PostgreSQL `Standard_B1ms` (unrestricted), Document Intelligence (`FormRecognizer` F0/S0) and regional `text-embedding-3-large`. polandcentral has **no** `FormRecognizer`/`ComputerVision` SKU, so it cannot host S-03 OCR.
  - All required resource providers are registered.
- GitHub: `gustaw-beznicki/ogarniamy-zwierzaki` is **public**, the default branch is `main`, and there are no environments or variables yet. `gh` is authenticated.

## Desired End State

- `https://<swa-default-host>/` serves the Astro page, which calls `/api/health` from the browser and shows `API: ok`.
- `https://<swa-default-host>/api/health` returns `200 {"status":"ok"}`, proxied to the App Service linked backend.
- `https://<api-app>.azurewebsites.net/api/health` does **not** return 200, because the linked backend accepts only SWA-proxied traffic.
- A pull request runs builds, Bicep lint and a subscription `what-if`. A merge to `main` waits for approval in the `production` environment, then applies Bicep, deploys API and web, and runs the smoke test.
- A monthly subscription budget of 20 USD, created by the one-time bootstrap, notifies the configured address at 50/80/100 % actual and 100 % forecasted spend.
- No credentials or personal data are in tracked files or CI logs. GitHub stores only non-secret identifiers as repository variables.
- The PR identity can only read and run `what-if`; only the approved `production` job can change Azure.

Verify with `scripts/smoke.sh`, the green `deploy` workflow run, and a browser visit on phone and desktop.

### Key Discoveries:

- SWA linked backends require the SWA **Standard** plan, support all App Service plans (including F1), proxy only `/api/*` with the path unchanged, are not available in PR environments, and cap requests at 45 s (learn.microsoft.com/azure/static-web-apps/apis-app-service, /apis-overview).
- Linking adds an "Azure Static Web Apps (Linked)" identity provider to the App Service, which then rejects direct traffic. Unlinking does not remove it.
- Bicep resource: `Microsoft.Web/staticSites/linkedBackends` with `properties.backendResourceId` and `properties.region` (backend region).
- The SWA resource region only affects managed Functions; static content is global. The linked backend may be in another region.
- F1: 60 CPU-min/day, 1 GB memory, 165 MB/day bandwidth, no Always On (cold starts), one free Linux plan per region per subscription. When quota is exceeded the app returns 403 until the daily reset.
- The Linux runtime string uses a pipe: `DOTNETCORE|10.0`.
- `az webapp up` is deprecated in Azure CLI 2.90. Use explicit deploy commands (`infrastructure.md`).

## What We're NOT Doing

- No database, Blob/Queue Storage, Key Vault, OCR or embeddings resources (S-01..S-04).
- No Application Insights or Log Analytics; diagnostics use `az webapp log tail`. Observability is added when S-03 introduces background work.
- No authentication, no CORS configuration (same origin through the SWA proxy).
- No staging slots, no SWA PR preview environments (disabled; linked backends do not work there anyway).
- No custom domain.
- No role assignments inside `infra/main.bicep`; the CI identity is Contributor and cannot grant roles. RBAC for managed identities arrives with the first slice that needs it, together with an explicit permission expansion.
- No automated test project (xUnit/Vitest); the deployed smoke test is F-01's test. The user-perspective test belongs to S-04.
- No Docker, no Terraform, no cost optimisation beyond the budget alert.
- No change to strict TypeScript, nullable reference types or implicit usings.

## Implementation Approach

Build inside-out so each phase is verifiable on its own:

1. Make the application prove connectivity: a health endpoint under `/api` and a page that calls it, working locally through an Astro dev proxy that mirrors the SWA proxy.
2. Declare the infrastructure in Bicep, with a separate one-time bootstrap template for the GitHub OIDC identities and the budget (the identity CI uses cannot create itself, and the budget must not be re-applied by CI). Validate it locally with lint and `what-if`; the bootstrap is the only thing applied by hand.
3. Wire GitHub Actions: `what-if` on PRs, and one gated `deploy` job on `main` that applies infra, deploys both artifacts and runs the smoke test. Update README and `infrastructure.md` to match the decisions.

All infrastructure commands live in `infra/deploy.sh`, so a local run and CI execute the same steps.

## Critical Implementation Details

- **Federated credential ordering**: Azure rejects concurrent writes of federated identity credentials on one managed identity. Each bootstrap identity has exactly one credential; if a second one is ever added to the same identity, chain them with `dependsOn` or `@batchSize(1)`.
- **Two CI identities**: the deploy identity (Contributor) trusts only the `production` environment subject; the PR identity (Reader + what-if custom role) trusts only `pull_request`. A branch PR that edits a workflow therefore cannot mutate Azure.
- **F1 constraints in Bicep**: the plan must be `kind: 'linux'` with `reserved: true`, and `siteConfig.alwaysOn` must be `false` whenever the SKU is F1 (Free rejects Always On). Derive `alwaysOn` from the SKU parameter so switching to B1 turns it on.
- **Budget start date**: `Microsoft.Consumption/budgets` needs `timePeriod.startDate` on the first day of a month, the value cannot be changed after creation ("Budget start date cannot be updated"), and a past start date may be rejected once it is outside the current month (azure-quickstart-templates#7095). Use a fixed parameter value (`2026-09-01`), not `utcNow()`, and deploy the budget only from the one-time bootstrap, never from CI.
- **Deploy order**: SWA resolves `/api` only after an app has been deployed to it, and link propagation can take minutes. The job order is apply → API → web → smoke, and the smoke test retries `/api/health` for up to 10 minutes before failing.
- **Public repo**: Actions logs are public and `what-if` prints property values, so the budget email must never enter CI. It is read only during the local bootstrap (`readEnvironmentVariable('BUDGET_ALERT_EMAIL')` in `bootstrap.bicepparam`) and is never written to a tracked file or a GitHub variable. Fork PRs get no OIDC token, so the `what-if` job must be skipped for them.

## Phase 1: Health endpoint and API status page

### Overview

The API exposes `GET /api/health` and the web page calls it and renders the status. Locally, Astro's dev server proxies `/api` to the API, like SWA will in Azure.

### Changes Required:

#### 1. API health endpoint

**File**: `services/api/Program.cs`

**Intent**: Replace the template's sample weather endpoint with a minimal health endpoint under the `/api` prefix that SWA proxies. Keep OpenAPI for Development.

**Contract**: `GET /api/health` → `200`, `application/json`, body `{"status":"ok"}`. `/weatherforecast` and the `WeatherForecast` record are removed. No other routes.

**File**: `services/api/ogarniamy-zwierzaki-api.http`

**Intent**: Point the sample request at the new endpoint.

**Contract**: Keep the existing `@ogarniamy_zwierzaki_api_HostAddress` variable and set it to `http://localhost:5180`. One request, `GET {{ogarniamy_zwierzaki_api_HostAddress}}/api/health`.

#### 2. Fixed local API port

**File**: `services/api/Properties/launchSettings.json`

**Intent**: Give the `http` profile a stable port so the web dev proxy has a target. The `https` profile stays unchanged.

**Contract**: `profiles.http.applicationUrl` = `http://localhost:5180`.

#### 3. Web dev proxy and status page

**File**: `apps/web/astro.config.mjs`

**Intent**: Proxy `/api` in `astro dev` to the local API so the page uses the same relative path it will use behind SWA.

**Contract**: `vite.server.proxy['/api']` targets `process.env.API_PROXY_TARGET ?? 'http://localhost:5180'`. It has no effect on `astro build` output.

**File**: `apps/web/src/pages/index.astro`

**Intent**: Replace the placeholder with a minimal product page that shows whether the API is reachable. This is the in-browser half of the smoke test. The UI copy is English (see `context/foundation/lessons.md`).

**Contract**:
- `<title>` and `<h1>` read `Ogarniamy Zwierzaki`.
- An element with `id="api-status"` and `data-state` of `loading`, `ok` or `unavailable` shows `API: checking…`, `API: ok` or `API: unavailable`.
- A client-side script calls `fetch('/api/health')` with a timeout of about 10 s. It sets `ok` only for HTTP 200 with `status === "ok"`, and `unavailable` otherwise.
- The markup includes `<meta name="viewport" content="width=device-width, initial-scale=1">` so it renders correctly on a phone.

### Success Criteria:

#### Automated Verification:

- Web build passes: `npm ci --prefix apps/web && npm run build --prefix apps/web`
- API build passes: `dotnet restore services/api/ogarniamy-zwierzaki-api.csproj && dotnet build services/api/ogarniamy-zwierzaki-api.csproj --no-restore`
- Local API returns `200 {"status":"ok"}` on `http://localhost:5180/api/health` and `404` on `/weatherforecast` (API started with `dotnet run --launch-profile http`, then stopped)
- Built page contains the status element: `grep -q 'id="api-status"' apps/web/dist/index.html`

#### Manual Verification:

- With `dotnet run --launch-profile http` and `npm run dev` both running, `http://localhost:4321` shows `API: ok`
- After stopping the API and reloading, the page shows `API: unavailable`

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Bicep infrastructure and bootstrap identity

### Overview

Declare all F-01 Azure resources in Bicep. A separate bootstrap template creates the two GitHub OIDC identities and the subscription budget, which the user applies once locally. The main template is validated locally with lint and `what-if` only; its first apply happens in CI (Phase 3).

### Changes Required:

#### 1. Bootstrap identity (applied once, by hand)

**File**: `infra/bootstrap/main.bicep`

**Intent**: Create the identities GitHub Actions uses to reach Azure via OIDC, grant each one just enough for its path, and create the subscription budget. It is kept separate because CI cannot create its own identity. The budget lives here, not in `main.bicep`, so CI never re-PUTs it (its `startDate` cannot be changed and may be rejected once it lies in a past month) and the alert email never reaches the public Actions logs.

**Contract**:
- `targetScope = 'subscription'`.
- Creates resource group `rg-ogarniamy-cicd` in `location`.
- In that group, creates two user-assigned managed identities:
  - `id-ogarniamy-github` (deploy path)
  - `id-ogarniamy-github-pr` (PR `what-if` path)
- Each identity gets one federated identity credential. Both use issuer `https://token.actions.githubusercontent.com` and audience `api://AzureADTokenExchange`:
  - `id-ogarniamy-github`: `repo:<githubRepo>:environment:production`
  - `id-ogarniamy-github-pr`: `repo:<githubRepo>:pull_request`
- Role assignments at subscription scope, all with `principalType: 'ServicePrincipal'` (avoids `PrincipalNotFound` from replication delay). Their names are seeded only from values known at deployment start, e.g. `guid(subscription().id, 'rg-ogarniamy-cicd', '<identity name>', roleId)`, never from a `principalId` (BCP120):
  - `id-ogarniamy-github`: built-in `Contributor` (`b24988ac-6180-42a0-ab88-20f7382dd24c`)
  - `id-ogarniamy-github-pr`: built-in `Reader` (`acdd72a7-3385-48ef-bd42-f606fba81ae7`) plus a custom role `Ogarniamy What-If` (`Microsoft.Authorization/roleDefinitions`, assignable at the subscription, actions `Microsoft.Resources/deployments/validate/action` and `Microsoft.Resources/deployments/whatIf/action`). The PR identity cannot write resources, so a branch PR cannot bypass the `production` gate.
- Calls `../modules/budget.bicep` at subscription scope.
- Params:
  - `location` (string)
  - `githubRepo` (string, `owner/name`)
  - `budgetAmount` (int)
  - `budgetStartDate` (string, `yyyy-MM-01`)
  - `budgetContactEmail` (string, no default)
- Outputs: `clientId`, `prClientId`, `tenantId`.

**File**: `infra/bootstrap/bootstrap.bicepparam`

**Intent**: Bootstrap values. The email is read from the local environment because the repo is public.

**Contract**:
- `location = 'swedencentral'`, `githubRepo = 'gustaw-beznicki/ogarniamy-zwierzaki'`
- `budgetAmount = 20`, `budgetStartDate = '2026-09-01'`
- `budgetContactEmail = readEnvironmentVariable('BUDGET_ALERT_EMAIL')`

#### 2. Main template and modules

**File**: `infra/main.bicep`

**Intent**: Subscription-scope entry point that creates the application resource group and calls the modules. The budget is not here (see the bootstrap).

**Contract**:
- `targetScope = 'subscription'`.
- Params:
  - `location` (backend region)
  - `swaLocation` (`@allowed(['westeurope','eastus2','centralus','westus2','eastasia'])`)
  - `appServiceSku` (`@allowed(['F1','B1','S1'])`)
- Creates resource group `rg-ogarniamy-mvp` in `location`. Calls `modules/app-service.bicep` and `modules/static-web-app.bicep` at the group.
- Outputs: `resourceGroupName`, `apiAppName`, `apiDefaultHostName`, `staticWebAppName`, `staticWebAppDefaultHostName`.
- No role assignments.

**File**: `infra/modules/app-service.bicep`

**Intent**: Linux App Service plan and the API web app.

**Contract**:
- Plan `asp-ogarniamy-mvp`: `kind: 'linux'`, `reserved: true`, `sku.name = appServiceSku`.
- Site `app-ogarniamy-api-${uniqueString(subscription().id)}`:
  - `httpsOnly: true`
  - `siteConfig.linuxFxVersion: 'DOTNETCORE|10.0'`
  - `siteConfig.alwaysOn: appServiceSku != 'F1'`
  - `siteConfig.minTlsVersion: '1.2'`, `siteConfig.ftpsState: 'Disabled'`
  - app setting `ASPNETCORE_ENVIRONMENT=Production`
- Outputs: `id`, `name`, `defaultHostName`.

**File**: `infra/modules/static-web-app.bicep`

**Intent**: SWA Standard resource for the Astro build, and the link to the API as its backend.

**Contract**:
- `Microsoft.Web/staticSites` named `swa-ogarniamy-web` in `swaLocation`, with `sku: { name: 'Standard', tier: 'Standard' }`, `properties.stagingEnvironmentPolicy: 'Disabled'` and no repository integration.
- Child `Microsoft.Web/staticSites/linkedBackends` named `api`, with `backendResourceId` set to the API site id and `region` set to the backend `location`.
- Outputs: `name`, `defaultHostName`.

**File**: `infra/modules/budget.bicep`

**Intent**: Subscription-wide monthly cost budget with email notifications. Called only from `infra/bootstrap/main.bicep`.

**Contract**:
- `targetScope = 'subscription'`.
- `Microsoft.Consumption/budgets` named `budget-ogarniamy-monthly`, with `category: 'Cost'`, `timeGrain: 'Monthly'`, `amount = budgetAmount` and `timePeriod.startDate = budgetStartDate`.
- Four notifications with `contactEmails: [budgetContactEmail]`:
  - `Actual` at `GreaterThan` 50 %
  - `Actual` at `GreaterThan` 80 %
  - `Actual` at `GreaterThan` 100 %
  - `Forecasted` at `GreaterThan` 100 %

#### 3. Environment parameters

**File**: `infra/environments/mvp.bicepparam`

**Intent**: Every value for the MVP environment, in one reviewable file. It contains nothing personal, so CI needs no email.

**Contract**:
- `using '../main.bicep'`
- `location = 'swedencentral'`, `swaLocation = 'westeurope'`, `appServiceSku = 'F1'`

#### 4. Shared deploy script

**File**: `infra/deploy.sh`

**Intent**: A single entry point for infrastructure commands, used by the user locally and by CI.

**Contract**:
- `infra/deploy.sh <lint|what-if|apply>`, run with `set -euo pipefail`.
- `lint` runs `az bicep lint` on `infra/main.bicep` and `infra/bootstrap/main.bicep`. It needs no Azure login.
- `what-if` runs `az deployment sub what-if` at location `swedencentral`, with deployment name `ogarniamy-mvp`, `infra/main.bicep` and `infra/environments/mvp.bicepparam`.
- `apply` runs `az deployment sub create` with the same arguments and prints `properties.outputs` as JSON on stdout. Apply is non-interactive; approval is the GitHub environment gate.

### Success Criteria:

#### Automated Verification:

- Bicep CLI available: `az bicep install && az bicep version`
- Both templates lint clean: `infra/deploy.sh lint`
- Parameter file compiles: `az bicep build-params --file infra/environments/mvp.bicepparam --stdout > /dev/null && BUDGET_ALERT_EMAIL=ci@example.invalid az bicep build-params --file infra/bootstrap/bootstrap.bicepparam --stdout > /dev/null`
- Deploy script is syntactically valid: `bash -n infra/deploy.sh`
- No email address or credential in tracked infra files (plain `grep`, because the files are still untracked before the phase commit): `! grep -rnE '[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[a-z]{2,}' infra/`

#### Manual Verification:

- The user applies the bootstrap once: `BUDGET_ALERT_EMAIL=<address> az deployment sub create --location swedencentral --template-file infra/bootstrap/main.bicep --parameters infra/bootstrap/bootstrap.bicepparam --confirm-with-what-if`, then records the `clientId` and `prClientId` outputs
- The user sets repository variables `AZURE_CLIENT_ID`, `AZURE_PR_CLIENT_ID`, `AZURE_TENANT_ID` and `AZURE_SUBSCRIPTION_ID` (`gh variable set …`), and creates the GitHub environment `production` with themself as required reviewer
- `infra/deploy.sh what-if` lists only creates (resource group, plan, site, static site, linked backend), with no deletes or modifications

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: CI/CD pipeline, smoke test and documentation

### Overview

GitHub Actions validates every PR (builds, lint, `what-if`) and deploys every merge to `main` through one gated job that ends with the smoke test. The docs are updated to describe the deployed reality. This phase lands through a branch and PR so `ci.yml` runs before the first deploy.

### Changes Required:

#### 1. Smoke test

**File**: `scripts/smoke.sh`

**Intent**: Prove the deployed path end to end, including the security property of the linked backend. Used by CI and runnable locally.

**Contract**:
- `scripts/smoke.sh <swa-host> <api-host>`, exits non-zero with a message naming the failed check.
- Check 1: `https://<swa-host>/` returns 200 and its body contains `id="api-status"`.
- Check 2: `https://<swa-host>/api/health` returns 200 with `"status":"ok"`, retried every 20 s for up to 10 min (F1 cold start, link propagation).
- Check 3: `https://<api-host>/api/health` returns a status other than 200 (expected 401/403).

#### 2. Pull-request validation

**File**: `.github/workflows/ci.yml`

**Intent**: Catch build and infrastructure problems before merge, and show the infrastructure diff for review.

**Contract**:
- Triggers on `pull_request` to `main`, with `permissions: { contents: read, id-token: write }`.
- Job `web`: Node 24, `npm ci` and `npm run build` in `apps/web`.
- Job `api`: .NET 10 SDK, `dotnet restore` and `dotnet build` on the csproj.
- Job `infra`:
  - runs `infra/deploy.sh lint`
  - then `azure/login@v2` with OIDC, using the read-only PR identity (`client-id: ${{ vars.AZURE_PR_CLIENT_ID }}`, `tenant-id` and `subscription-id` from `vars.*`); the job has no `environment`, so its subject is `:pull_request`
  - then `infra/deploy.sh what-if`
  - the login and `what-if` steps run only when `github.event.pull_request.head.repo.full_name == github.repository`

#### 3. Gated deployment

**File**: `.github/workflows/deploy.yml`

**Intent**: After a merge, apply infrastructure and release both components in one approved job, then prove the result.

**Contract**:
- Triggers on `push` to `main` and `workflow_dispatch`.
- Uses `concurrency: { group: deploy-production, cancel-in-progress: false }` and `permissions: { contents: read, id-token: write }`.
- A single job `deploy` with `environment: production` runs, in order:
  1. checkout
  2. build web (`apps/web/dist`)
  3. `dotnet publish -c Release`, zipped into an artifact under `$RUNNER_TEMP`
  4. `azure/login@v2` via OIDC
  5. `az bicep install`
  6. `infra/deploy.sh apply`, parsing the outputs
  7. `az webapp deploy --type zip` to the API app
  8. fetch the SWA deployment token at runtime with `az staticwebapp secrets list … --query properties.apiKey -o tsv` and mask it with `::add-mask::`
  9. `Azure/static-web-apps-deploy@v1` with `action: upload`, `app_location: apps/web/dist`, `skip_app_build: true`, `api_location: ''`
  10. `scripts/smoke.sh <staticWebAppDefaultHostName> <apiDefaultHostName>`
- No repository secrets are used.

#### 4. Documentation

**File**: `README.md`

**Intent**: Stop saying no cloud resources or CI exist, and document how deployment works.

**Contract**:
- The Current status table and the "No database, cloud resources, CI workflow…" sentence reflect the deployed skeleton.
- The API section documents `/api/health` instead of `/weatherforecast`, and local dev notes cover the fixed port 5180 and the Astro proxy.
- A new `## Deployment` section covers:
  - the architecture: SWA Standard (westeurope) → linked backend → App Service F1 (swedencentral)
  - the one-time bootstrap (two CI identities and the budget, with `BUDGET_ALERT_EMAIL` set only locally)
  - the required repository variables and the `production` environment
  - PR `what-if` with the read-only PR identity, and the gated deploy on merge
  - rollback, done by reverting the commit and letting it redeploy, or re-running a previous successful `deploy` run
  - the budget alert, and that its start date cannot change on a bootstrap re-run
- The repository structure lists `infra/`, `.github/workflows/` and `scripts/`.

**File**: `context/foundation/infrastructure.md`

**Intent**: Evolve the foundation document in place so it matches the decisions made while planning F-01.

**Contract**: The Recommendation/Operational Story sections record:
- F1 for the walking skeleton, as a parameter, with B1 when S-03 needs Always On
- SWA Standard with a linked backend, so no CORS and no PR backends
- regions: swedencentral for the backend and future data/AI (Poland Central lacks Document Intelligence), westeurope for SWA with eastus2 as the fallback
- infrastructure applied by CI behind the `production` environment reviewer, with `what-if` on PRs, replacing the local-apply flow
- the one-time bootstrap: deploy identity with subscription Contributor (production environment only), read-only PR identity with Reader + what-if custom role, and the subscription budget
- the subscription is PAYG with no spending limit, not a Free Trial
- the planned layout marked as partially implemented

### Success Criteria:

#### Automated Verification:

- Smoke script is syntactically valid: `bash -n scripts/smoke.sh`
- Workflows parse as YAML: `python3 -c "import yaml,sys;[yaml.safe_load(open(f)) for f in sys.argv[1:]]" .github/workflows/ci.yml .github/workflows/deploy.yml`
- The PR's `ci.yml` run is green for `web`, `api` and `infra`, and the `infra` log shows a `what-if` with only creates: `gh pr checks <pr> --watch`
- Web and API builds still pass locally (same commands as Phase 1)

#### Manual Verification:

- After merge, the user approves the `production` deployment and the `deploy` run completes, with the smoke step passing
- `https://<swa-default-host>/` shows `API: ok` on a phone and on a desktop browser
- `https://<api-app>.azurewebsites.net/api/health` opened directly in a browser is refused (401/403)
- Cost Management shows budget `budget-ogarniamy-monthly` (20 USD) with four notifications to the configured address
- A second `workflow_dispatch` run of `deploy` succeeds with an unchanged infrastructure result and a passing smoke test (idempotency, link lock preserved)

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Testing Strategy

### Unit Tests:

- None in F-01. There is no logic beyond a constant health response. A test project is introduced by the first slice with real behaviour.

### Integration Tests:

- `scripts/smoke.sh` against the deployed environment: page served, `/api/health` through the SWA proxy, direct backend access refused. It runs on every deploy and becomes the verification path reused by later slices.

### Manual Testing Steps:

1. Locally: run the API and `npm run dev`, confirm `API: ok`; stop the API and confirm `API: unavailable`.
2. Open a PR and read the `what-if` output in the `infra` job; confirm it contains only expected resources.
3. Merge, approve the `production` deployment, and watch the run through the smoke step.
4. Open the SWA URL on a phone and on a desktop; open the direct API URL and confirm it is refused.
5. Check the budget and its notifications in Cost Management.

## Performance Considerations

F1 has no Always On. The first request after idle can take several seconds, so the page's fetch timeout and the smoke retries allow for that. F1 daily CPU (60 min) and bandwidth (165 MB) quotas are ample for a skeleton. If the app returns 403 because of quota, the fix is `appServiceSku = 'B1'` in `mvp.bicepparam`, which S-03 will need anyway for the background worker.

## Migration Notes

- Switching to B1/S1 means changing one parameter. `alwaysOn` follows automatically.
- If westeurope rejects SWA creation for this subscription ("not accepting new customers"), set `swaLocation = 'eastus2'`. Static content stays global; only the SWA resource metadata moves.
- If the F1 quota is 0 in swedencentral (`SubscriptionIsOverQuotaForSku`), use B1.
- If the PR `what-if` fails with `AuthorizationFailed`, add the missing action named in the error to the `Ogarniamy What-If` custom role and re-run the bootstrap. Do not give the PR identity Contributor.
- Re-running the bootstrap in a later month keeps `budgetStartDate` unchanged. If ARM rejects the past date, delete and recreate the budget with the current month's first day; do not move the budget into CI.
- If the second `deploy` apply fails on `linkedBackends` ("preexisting Azure Static Web Apps configuration"), add a `linkBackend` bool param to `main.bicep` (the linked backend is deployed only when it is `true`), set it to `false` in `mvp.bicepparam` after the first successful link, and confirm the direct-URL refusal still holds.
- Rollback of application code: revert the merge commit (redeploys the previous build) or re-run an earlier successful `deploy` run. F-01 has no data, so no data rollback is involved.

## References

- Roadmap item: `context/foundation/roadmap.md` (F-01)
- Infrastructure direction: `context/foundation/infrastructure.md`
- Stack: `context/foundation/tech-stack.md`
- Rules: `context/foundation/lessons.md` (non-login shell; English in code and context)
- SWA + App Service backend: https://learn.microsoft.com/azure/static-web-apps/apis-app-service
- SWA API constraints: https://learn.microsoft.com/azure/static-web-apps/apis-overview
- Linked backend Bicep: https://learn.microsoft.com/azure/templates/microsoft.web/staticsites/linkedbackends
- App Service limits: https://learn.microsoft.com/azure/azure-resource-manager/management/azure-subscription-service-limits#azure-app-service-limits
- Region access errors: https://learn.microsoft.com/azure/azure-resource-manager/troubleshooting/error-region-access-policy
- Current API template: `services/api/Program.cs:17-35`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Health endpoint and API status page

#### Automated

- [x] 1.1 Web build passes — 866c034
- [x] 1.2 API build passes — 866c034
- [x] 1.3 Local API returns 200 on /api/health and 404 on /weatherforecast — 866c034
- [x] 1.4 Built page contains the status element — 866c034

#### Manual

- [x] 1.5 Local page shows "API: ok" with API running — 866c034
- [x] 1.6 Local page shows "API: unavailable" with API stopped — 866c034

### Phase 2: Bicep infrastructure and bootstrap identity

#### Automated

- [x] 2.1 Bicep CLI available — b9940dd
- [x] 2.2 Both templates lint clean — b9940dd
- [x] 2.3 Parameter file compiles — b9940dd
- [x] 2.4 Deploy script is syntactically valid — b9940dd
- [x] 2.5 No email address or credential in tracked infra files — b9940dd

#### Manual

- [x] 2.6 Bootstrap identity applied once and clientId recorded — 5aa7749
- [x] 2.7 Repository variables and production environment configured — 5aa7749
- [x] 2.8 Local what-if lists only expected creates — 5aa7749

### Phase 3: CI/CD pipeline, smoke test and documentation

#### Automated

- [x] 3.1 Smoke script is syntactically valid
- [x] 3.2 Workflows parse as YAML
- [ ] 3.3 PR ci.yml run is green with create-only what-if
- [x] 3.4 Web and API builds still pass locally

#### Manual

- [ ] 3.5 Approved deploy run completes with passing smoke step
- [ ] 3.6 Deployed page shows "API: ok" on phone and desktop
- [ ] 3.7 Direct API URL is refused
- [ ] 3.8 Budget with four notifications visible in Cost Management
- [ ] 3.9 Repeat deploy run is idempotent and keeps the link lock
