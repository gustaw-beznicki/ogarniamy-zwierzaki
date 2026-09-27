---
project: "Ogarniamy zwierzaki"
researched_at: 2026-09-21
recommended_platform: "Azure App Service"
runner_up: "Render"
context_type: mvp
infrastructure_as_code: "Bicep"
tech_stack:
  language: "C# 14 / TypeScript"
  framework: "ASP.NET Core 10 / Astro 7.3.3"
  runtime: ".NET SDK 10.0.112 / Node.js >=22.12.0"
---

## Recommendation

**Deploy on Azure App Service, with Azure Static Web Apps for the Astro frontend and Azure managed data services.**

Azure is the best fit for the .NET-first stack, the required continuously running background processing, and the requirement to keep original documents private and durable. It tied with Render on the agent-friendly criteria, then won on the existing Azure direction in `tech-stack.md`, the developer's Azure experience, and the explicit decision to treat the MVP as a low-cost learning window; the subscription is Pay-As-You-Go with no spending limit, so the budget alert is the spend guard and costs must be reassessed from measured spend. All Azure resources and RBAC assignments will be declared in Bicep so agents can inspect, preview, and modify infrastructure from the repository and CLI.

### Decisions recorded while planning F-01 (azure-walking-skeleton, 2026-09-27)

- **Subscription**: the subscription is Pay-As-You-Go with the spending limit off, not a Free Trial. Nothing caps spend automatically; the subscription budget alert is the only spend guard.
- **API tier**: the walking skeleton runs on App Service Linux **F1**, set by the `appServiceSku` parameter. F1 has no Always On (cold starts) and a daily CPU quota. Switch the parameter to **B1** when S-03 introduces background work that needs Always On; `alwaysOn` follows the SKU automatically.
- **Frontend**: Static Web Apps **Standard** with the App Service API as its **linked backend**. The page and `/api/*` share one origin, so no CORS is configured, and the linked App Service rejects direct traffic. SWA pull-request preview environments are disabled, because linked backends do not work there, so there are no PR backends.
- **Regions**: `swedencentral` for the backend and for future data and AI resources (Poland Central lacks Document Intelligence). The Static Web App resource is in **`eastus2`**: `westeurope` was attempted first and rejected with `RequestDisallowedByAzure` (not accepting new customers), so the planned fallback was applied. The SWA region only affects resource metadata; static content is served globally.
- **Deploy identity scope (accepted risk, F-01 review F3)**: `id-ogarniamy-github` is Contributor on the whole subscription, because `main.bicep` creates `rg-ogarniamy-mvp` at subscription scope. That scope includes `rg-ogarniamy-cicd`, so a compromised deploy job could add federated credentials to either identity, change or delete the budget, or create resources anywhere. Accepted for the walking skeleton: every use goes through the `production` reviewer and main-only branch policy. Follow-up for S-01, when RBAC is first needed: create the application resource group in the bootstrap, scope Contributor to it, and keep only a deployment-only custom role at subscription scope.
- **OIDC subject**: the repository issues immutable OIDC subject claims (`use_immutable_subject: true`), so the federated credentials trust `repo:<owner>@<owner-id>/<name>@<repo-id>:pull_request` and `…:environment:production`, and `githubRepo` in `infra/bootstrap/bootstrap.bicepparam` holds the `owner@id/name@id` form. The legacy `owner/name` subject failed with AADSTS700213.
- **PR `what-if` validation level**: `infra/deploy.sh what-if` passes `--validation-level ProviderNoRbac`, because the default `Provider` level checks write permission on every resource and the read-only PR identity failed with `AuthorizationFailed`; `apply` keeps the default level. Never fix a PR `what-if` `AuthorizationFailed` by granting write actions to the `Ogarniamy What-If` role, because that gives the PR identity write access; add read-level actions only if a read is genuinely missing.

## Research Constraints

- Persistent server-side processes are required for OCR and embedding work.
- Monthly cost and developer experience carry approximately equal weight.
- The developer has practical experience with Azure and Cloudflare.
- A single region is sufficient for the MVP.
- Co-location of database, object storage, and queues is undecided rather than mandatory.
- The PRD expects medium user scale, low QPS, small initial data volume, and perceived search latency of about two seconds or less.

## Platform Comparison

Scores use Pass = 2, Partial = 1, and Fail = 0. Hard stack compatibility is evaluated before the score; a high-scoring platform cannot enter the shortlist if it cannot run ASP.NET Core 10 and persistent background work.

| Platform | Stack eligibility | CLI-first | Managed / serverless | Agent-readable docs | Stable deployment API | MCP / agent integration | Score |
|---|---|---|---|---|---|---|---:|
| Azure App Service | Pass | Pass | Pass | Pass | Pass | Pass | 10/10 |
| Render | Pass via Docker | Pass | Pass | Pass | Pass | Pass | 10/10 |
| Cloudflare Workers + Containers | Pass with material caveats | Pass | Partial | Pass | Pass | Pass | 9/10 |
| Fly.io | Pass via Docker | Pass | Partial | Pass | Pass | Pass | 9/10 |
| Railway | Pass via Docker | Partial | Partial | Pass | Partial | Pass | 7/10 |
| Netlify | Rejected | Partial | Pass | Pass | Partial | Pass | n/a |
| Vercel | Rejected | Pass | Pass | Pass | Pass | Pass | n/a |

**Azure App Service.** Native .NET 10 support, continuous WebJobs, deterministic Azure CLI and REST operations, Azure MCP tools, managed PostgreSQL with `pgvector`, Blob Storage, and Queue Storage cover the complete stack in one provider. A Linux B1 plan is about USD 13.14/month and PostgreSQL B1ms about USD 12.41/month before database storage, observability, OCR, and embedding usage. B1 supports Always On but not deployment slots; Standard or higher is required for the preferred slot-based release flow. Sources: [App Service pricing](https://azure.microsoft.com/en-us/pricing/details/app-service/linux/), [WebJobs](https://learn.microsoft.com/en-us/azure/app-service/overview-webjobs), [App Service deployment guidance](https://learn.microsoft.com/en-us/azure/app-service/deploy-best-practices), [PostgreSQL pricing](https://azure.microsoft.com/en-us/pricing/details/postgresql/flexible-server/), [Azure MCP for App Service](https://learn.microsoft.com/en-us/azure/developer/azure-mcp-server/services/azure-mcp-server-for-app-service).

**Render.** Render has strong CLI, API, Markdown documentation, managed PostgreSQL with `pgvector`, and first-class persistent workers. ASP.NET Core must be deployed through Docker, and Render has no native private object storage suitable for the system-of-record originals. The lower-bound configuration is about USD 20/month for a small API, worker, and 256 MB PostgreSQL, rising to about USD 33/month with 1 GB PostgreSQL and before a queue or external object storage. Full PR preview environments require a Pro workspace. Sources: [Docker](https://render.com/docs/docker), [Background Workers](https://render.com/docs/background-workers), [Postgres](https://render.com/docs/postgresql), [Preview Environments](https://render.com/docs/preview-environments), [Pricing](https://render.com/pricing).

**Cloudflare Workers + Containers.** Cloudflare Containers became GA on 2026-04-13 and can run the .NET API, while Astro can target Workers. The combination is operationally less conventional for this .NET-first stack: container routing and scaling require additional design, the container filesystem is ephemeral, and PostgreSQL remains external through Hyperdrive. Workers, R2, Queues, the CLI, documentation, and MCP support are strong, but this path adds platform-specific integration risk. Sources: [Containers GA](https://developers.cloudflare.com/changelog/post/2026-04-13-containers-sandbox-ga/), [container scaling and routing](https://developers.cloudflare.com/containers/configuration/scaling-and-routing/), [Hyperdrive limitations](https://developers.cloudflare.com/hyperdrive/reference/supported-databases-and-features/).

**Fly.io.** Fly Machines can run pinned .NET container images and separate API and worker process groups. The platform has capable CLI and MCP tooling, but the team owns more container operations and the managed PostgreSQL baseline is comparatively expensive: approximately USD 52/month for two small Machines plus the smallest managed PostgreSQL configuration with 10 GB, before object storage and transfer. Some managed PostgreSQL operational features are still documented as under development. Sources: [Fly.io .NET](https://fly.io/docs/languages-and-frameworks/dotnet/), [process groups](https://fly.io/docs/launch/processes/), [Managed Postgres](https://fly.io/docs/mpg/), [pricing](https://fly.io/docs/about/pricing/).

**Railway.** Railway provides persistent services, private networking, buckets, PostgreSQL templates, CLI, and hosted MCP. Its current documentation conflicts on whether Railpack supports .NET automatically, making a pinned Docker build the safe choice. PostgreSQL templates, including `pgvector`, are explicitly customer-managed, and arbitrary rollback remains dashboard-oriented. Sources: [ASP.NET Core](https://docs.railway.com/guides/aspnet-core), [Railpack .NET](https://railpack.com/languages/dotnet), [databases](https://docs.railway.com/databases), [rollback](https://docs.railway.com/guides/roll-back-bad-deploy).

**Netlify.** Rejected as the platform for the complete application. It supports Astro well, but its first-party server runtimes do not include ASP.NET Core, and Background Functions have finite execution limits rather than providing a persistent .NET worker. It would host only the frontend and add another vendor without solving the backend requirement. Sources: [Astro on Netlify](https://docs.netlify.com/build/frameworks/framework-setup-guides/astro/), [Background Functions](https://docs.netlify.com/build/functions/background-functions/).

**Vercel.** Rejected as the platform for the complete application. Its first-party runtimes exclude .NET, Functions do not provide an always-running server, and they cannot act as a WebSocket server. Vercel remains a possible frontend-only host but does not satisfy the hard API and worker constraints. Sources: [Function runtimes](https://vercel.com/docs/functions/runtimes), [backend limitations](https://vercel.com/docs/frameworks/backend).

### Shortlisted Platforms

#### 1. Azure App Service (Recommended)

Azure covers the ASP.NET Core API, continuous background work, managed PostgreSQL with vector extensions, private Blob Storage, queues, identity, and observability without introducing a second infrastructure provider. Existing Azure familiarity breaks the criteria tie with Render, and the learning cost is acceptable for the MVP under a budget alert. The subscription is Pay-As-You-Go with no spending limit, so the selection is conditional on a regular cost review from measured spend and a downsizing plan.

#### 2. Render

Render is the clearest fallback if Azure's operational surface or steady-state cost becomes unacceptable. It provides a simpler service model and managed PostgreSQL, but it requires Docker for .NET and a separate private object-storage provider, weakening the end-to-end operational story for original documents.

#### 3. Railway

Railway offers attractive developer experience and co-located buckets, but its PostgreSQL templates are not a managed database service in the same sense as Azure or Render. That transfers backup, security, upgrade, and reliability work to a solo developer and outweighs its convenience for this archive.

## Anti-Bias Cross-Check: Azure App Service

### Devil's Advocate — Weaknesses

1. The inexpensive B1 plan provides Always On for the worker but not deployment slots. Safe staging and instant swap-back require Standard or higher, materially increasing steady-state cost.
2. Co-locating the API and continuous WebJob on a small App Service plan makes them compete for CPU and memory. A burst of OCR work can increase API and search latency.
3. The application spans App Service, Static Web Apps, PostgreSQL, Blob and Queue Storage, identity, Key Vault, and monitoring. Misaligned regions, RBAC, or configuration can break the flow even when each individual service is healthy.
4. Application rollback does not reverse PostgreSQL migrations, queue messages, generated embeddings, or Blob state. An incompatible data migration can make a successful code rollback unusable.
5. The skeleton's low price hides the steady-state price. On a Pay-As-You-Go subscription with no spending limit, App Service tier, database compute and storage, Application Insights, OCR, embeddings, and egress can produce a cost cliff that only the budget alert catches.

### Pre-Mortem — How This Could Fail

The team assumed that Azure familiarity and native .NET support removed most operational risk. To save money, the API and OCR worker shared a small plan, and releases went directly to production because the selected tier had no deployment slots. Small test files worked, but a bulk archive upload saturated CPU and memory. API latency rose above the two-second search target, worker restarts left jobs half-processed, and retrying non-idempotent work created duplicate embeddings.

A later release changed both the database schema and embedding format. The application package was rolled back, but the database and already-processed records were not, so the old build could no longer read part of the archive. At the same time, broad storage credentials had replaced managed identity during debugging, weakening owner isolation. Monitoring showed high consumption but did not connect cost and failures to individual documents. When real usage grew, the team discovered that the safer tier, database, telemetry, OCR, and embeddings cost much more than the initial B1 estimate. Azure remained technically capable, but unsafe release practices, shared resource contention, non-reversible migrations, and missing cost controls made every change slow and risky.

### Unknown Unknowns

- Always On and deployment slots have different tier thresholds: B1 is enough for a continuous WebJob, whereas slots require Standard or higher.
- Azure recommends evaluating Functions or Container Apps Jobs for new independently scaled background workloads; WebJobs are best when the worker deliberately shares the web app's lifecycle and scale.
- Azure CLI 2.90.0 confirms the Linux runtime identifier `DOTNETCORE:10.0`; runtime availability must still be checked in the selected region with `az webapp list-runtimes` before provisioning.
- `az webapp up` is deprecated in Azure CLI 2.90.0. The supported workflow is explicit `az webapp create` followed by `az webapp deploy`.
- Azure MCP Server provides App Service management tools, but App Service's built-in exposure of an application API as MCP is a separate **preview** feature and is not required by this MVP.

## Infrastructure as Code

**Bicep is the sole source of truth for Azure infrastructure.** Portal and ad-hoc CLI changes are permitted only for emergency diagnosis; any retained change must be represented in Bicep immediately afterward. Bicep is native to Azure Resource Manager, is idempotent, provides type checking and modules, and does not require the team to operate a separate state backend. Every proposed deployment must pass a local lint and an Azure `what-if` review before a human approves the mutation. Sources: [Bicep overview](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/overview), [Bicep CLI](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/bicep-cli), [what-if](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/deploy-what-if).

The planned repository layout is:

```text
infra/
├── main.bicep                       # subscription-scope entry point and resource group
├── modules/
│   ├── app-service.bicep            # plan, API app, identity, settings, optional staging slot
│   ├── static-web-app.bicep         # Astro hosting resource
│   ├── postgres.bicep               # Flexible Server and configuration
│   ├── storage.bicep                # private Blob container and Queue Storage
│   ├── key-vault.bicep              # secret references and access policy/RBAC surface
│   ├── budget.bicep                 # subscription budget, called only from infra/bootstrap
│   ├── monitoring.bicep             # Application Insights, logs, and alerts
│   └── rbac.bicep                   # least-privilege role assignments
└── environments/
    └── mvp.bicepparam               # non-secret names, region, SKUs, and feature switches
```

This layout is **partially implemented**. F-01 added `infra/main.bicep`, `infra/modules/app-service.bicep`, `infra/modules/static-web-app.bicep`, `infra/modules/budget.bicep` (called only from the bootstrap), `infra/environments/mvp.bicepparam`, a one-time `infra/bootstrap/` template, and `infra/deploy.sh`, the single entry point for `lint`, `what-if` and `apply`. The PostgreSQL, Storage, Key Vault, monitoring and RBAC modules remain planned for the slices that need them. Modules should expose typed inputs and only the outputs required by another module or the deployment process. Resource names, region, SKUs, retention periods, allowed origins, identities, and role assignments belong in code or non-secret parameter files. Passwords, tokens, connection strings, and deployment credentials must never appear in `.bicep` or `.bicepparam` files; use managed identities and Key Vault references, with any unavoidable bootstrap secret supplied securely at deployment time.

Terraform will not manage the same resources in parallel. It can be reconsidered only if the project becomes multi-cloud or adopts an organization-wide Terraform standard; that would require an explicit ownership migration rather than two overlapping IaC definitions. Helm is not applicable because the selected platform is App Service rather than Kubernetes. It becomes relevant only after an intentional migration to AKS or another Kubernetes platform.

## Operational Story

- **Preview deploys**: Not used for the walking skeleton. SWA preview environments are disabled because linked backends do not work there, and F1/B1 have no staging slots. Pull requests are validated by builds, Bicep lint and `what-if` only. If previews are added later, preview data must be synthetic and must never copy production veterinary documents.
- **Infrastructure changes**: CI applies infrastructure; the earlier local-apply flow is replaced. An agent edits Bicep and runs `infra/deploy.sh lint` and `infra/deploy.sh what-if` locally. On every pull request, `.github/workflows/ci.yml` lints and runs `what-if` with the read-only PR identity so the diff is reviewable. After merge to `main`, `.github/workflows/deploy.yml` runs one `deploy` job in the `production` environment, which waits for a required reviewer, then runs `infra/deploy.sh apply`, releases both components and runs `scripts/smoke.sh`. The reviewer checks the PR `what-if` for deletions, replacements, RBAC expansion, networking changes, and recurring-cost changes before approving.
- **One-time bootstrap**: `infra/bootstrap/main.bicep` is applied by hand once, never by CI. It creates `rg-ogarniamy-cicd` with two GitHub OIDC identities and the subscription budget. The deploy identity has subscription Contributor and trusts only the `production` environment subject. The PR identity has Reader plus the `Ogarniamy What-If` custom role (deployment validate and what-if actions) and trusts only the `pull_request` subject. The budget (`budget-ogarniamy-monthly`, 20 per month, alerts at 50/80/100 % actual and 100 % forecasted) is created only here; its start date cannot be changed after creation, and the alert recipient comes from the local `BUDGET_ALERT_EMAIL` variable, never from a tracked file or CI.
- **Secrets**: Runtime credentials live in Key Vault and are referenced by App Service settings through a system-assigned managed identity; application code receives references, not stored credentials. Deployment identities use GitHub OIDC with no stored secrets; GitHub holds only non-secret identifiers as repository variables. For the walking skeleton the deploy identity is subscription Contributor, gated by the `production` environment, and cannot grant roles. Only designated subscription/resource-group administrators may read or rotate secrets, and rotation is followed by a controlled app restart and smoke test.
- **Rollback**: For the walking skeleton, revert the commit on `main` and approve the resulting deploy, or re-run an earlier successful `deploy` workflow run; there is no data to roll back. On Standard or higher, run `az webapp deployment slot swap --resource-group <rg> --name <api-app> --slot staging --target-slot production` again to swap the previous production build back, normally within minutes. On B1, redeploy the previous immutable ZIP artifact with `az webapp deploy`. Database migrations, queue effects, and Blob writes are not rolled back and require forward-compatible migrations plus a separate recovery procedure.
- **Approval**: An agent may build, deploy to preview/staging, run smoke tests, and read logs with least-privilege credentials. A human approves the production slot swap, primary-secret rotation, database restoration or deletion, storage deletion, and any tier change that materially changes recurring cost.
- **Logs**: Read live App Service logs with `az webapp log tail --resource-group <rg> --name <api-app>` and deployment history with `az webapp log deployment list`. Use Azure Monitor/Application Insights read-only queries for correlated API and worker telemetry; Azure MCP tools may retrieve App Service deployment and diagnostic information under read-only RBAC.

## Risk Register

| Risk | Source | Likelihood | Impact | Mitigation |
|---|---|---:|---:|---|
| Pay-As-You-Go with no spending limit creates an unexpected recurring bill | Research finding | H | H | The subscription budget alert (bootstrap only) is the spend guard from day one; review measured monthly cost regularly and before each tier change, and compare Azure against Render again. |
| API and worker contend on a shared App Service plan | Devil's advocate | M | H | Measure CPU, memory, queue delay, and search latency with a real binder-sized import; move the worker to a separate plan or job service if thresholds are exceeded. |
| Code rollback is incompatible with migrated data | Pre-mortem | M | H | Use expand-and-contract migrations, version embedding formats, keep old readers compatible for one release, and test rollback before production. |
| Duplicate or lost OCR/embedding work after restart | Pre-mortem | M | H | Make jobs idempotent, persist job state, use visibility timeouts, and acknowledge queue messages only after all derived records are committed. |
| Blob access exposes another owner's original | Devil's advocate | L | H | Keep containers private, authorize through managed identity, issue short-lived user-scoped download URLs only after owner checks, and test cross-account denial. |
| Stored original becomes unreachable while derived data remains | Research finding | L | H | Treat Blob Storage as the system of record, store immutable blob identifiers in PostgreSQL, enable redundancy appropriate to the region, and continuously test retrieval independently of OCR/search. |
| Secrets or deployment identity receive excessive permissions | Devil's advocate | M | H | Use resource-scoped RBAC and GitHub OIDC; prohibit subscription-owner credentials in CI; review role assignments before production. |
| B1 lacks deployment slots and encourages direct production deploys | Unknown unknowns | H | M | Use Standard when the budget allows or provision a separate staging app; never represent B1 as having slot rollback. |
| `pgvector` or B1ms memory is insufficient for the real archive | Research finding | M | M | Benchmark with several dozen real documents, capture query plans and latency, and resize only from measured evidence. |
| OCR, embedding, and telemetry usage dominate infrastructure cost | Research finding | M | M | Tag resources, set per-service budgets, record cost per processed document, sample verbose telemetry, and cap retry counts. |
| Runtime or CLI behavior changes between planning and deployment | Unknown unknowns | M | M | Pin tool versions in the deployment plan, query supported runtimes immediately before provisioning, and use explicit create/deploy commands instead of deprecated `az webapp up`. |
| Portal or ad-hoc CLI changes drift from Bicep | Research finding | M | H | Make Bicep the sole source of truth, run `what-if` before every deployment, and translate every retained emergency change back into code immediately. |
| Bicep and another IaC tool compete for resource ownership | Research finding | L | H | Do not introduce Terraform for the same Azure resources; require an explicit migration plan if the ownership model ever changes. |
| Single-region outage makes the MVP unavailable | Research finding | L | M | Accept explicitly for MVP, keep portable backups and infrastructure definitions, and do not imply multi-region availability. |

## Getting Started

These commands were checked against the repository's .NET SDK `10.0.112`, Node.js `24.21.0`, npm `12.0.2`, Astro `7.3.3`, and locally installed Azure CLI `2.90.0`. They are the starting point for the deployment plan, not authorization to modify Azure now.

1. Verify the current repository and Azure runtime before provisioning:

   ```bash
   npm run build --prefix apps/web
   dotnet restore services/api/ogarniamy-zwierzaki-api.csproj
   dotnet build services/api/ogarniamy-zwierzaki-api.csproj --no-restore
   az version
   az webapp list-runtimes --os-type linux --runtime dotnet --support supported --output table
   ```

2. Authenticate, select the subscription explicitly, install or update the Bicep CLI through Azure CLI, and verify it:

   ```bash
   az login
   az account set --subscription <subscription-id>
   az bicep install
   az bicep version
   ```

3. Implement the planned `infra/` layout. The subscription-scope `main.bicep` creates the regional resource group and calls modules for App Service, Static Web Apps, PostgreSQL, Storage, Key Vault, monitoring, and alerts. It contains no budget and, for F-01, no RBAC: the budget lives in `infra/modules/budget.bicep`, called only from the one-time `infra/bootstrap` template, and role assignments are added only when a later slice needs them. Use B1 for cost-oriented validation or S1 when staging slots are required; model that choice as a parameter rather than editing resource definitions.

4. Validate locally and preview the exact Azure changes with `infra/deploy.sh lint` and `infra/deploy.sh what-if` (equivalent to the commands below). Save the machine-readable `what-if` result as deployment evidence; do not deploy when it contains an unexplained delete, replacement, RBAC expansion, or paid-tier change:

   ```bash
   az bicep lint --file infra/main.bicep
   az deployment sub what-if --location <region> --template-file infra/main.bicep --parameters infra/environments/mvp.bicepparam --no-pretty-print
   ```

5. After human review, provision or update infrastructure through Bicep, then deploy immutable application artifacts separately. Bicep owns resources and configuration; `az webapp deploy` and the Static Web Apps workflow own application releases. **Replaced for F-01:** the local apply below is no longer used; `.github/workflows/deploy.yml` runs `infra/deploy.sh apply` and the releases behind the `production` environment reviewer. The commands are kept for reference:

   ```bash
   az deployment sub create --location <region> --template-file infra/main.bicep --parameters infra/environments/mvp.bicepparam --confirm-with-what-if
   dotnet publish services/api/ogarniamy-zwierzaki-api.csproj --configuration Release --output <publish-dir>
   az webapp deploy --resource-group <resource-group> --name <api-app> --type zip --src-path <immutable-api-artifact.zip>
   ```

   The later deployment plan must supply final resource names, repository URL, region, RBAC scopes, secure bootstrap-secret flow, database migration flow, and the exact continuous WebJob packaging layout before any production action.

## Out of Scope

The following were not evaluated or implemented in this research:

- Docker image configuration
- CI/CD pipeline setup (since implemented by F-01: `.github/workflows/ci.yml` and `.github/workflows/deploy.yml`)
- Production-scale architecture such as multi-region availability, high availability, or disaster recovery
