# Account and First Animal Implementation Plan

## Overview

S-01 turns the walking skeleton into a product with real users. Owners register and sign in with an email and password, set up their first animal (name only) on an onboarding screen, and see only their own animals. The slice introduces the first database (Azure PostgreSQL Flexible Server), the first test project, the real app shell with a Polish (default) and English UI, and the person–animal relationship carrying a role that the PRD makes binding from day one. Before any personal data lands, it also closes the F-01 follow-up F3 by narrowing the CI deploy identity from subscription Contributor to the application resource group.

## Current State Analysis

- **API**: `services/api/Program.cs:17-18` exposes only `GET /api/health`; no auth, no database package, no DbContext. The csproj (`services/api/ogarniamy-zwierzaki-api.csproj:4-11`) targets `net10.0` with Nullable and ImplicitUsings enabled and references only `Microsoft.AspNetCore.OpenApi`. No `ConnectionStrings` in appsettings. No test project and no solution file anywhere (README.md:129).
- **Web**: `apps/web` is Astro `^7.3.3` in static output with no adapter and no integrations (`apps/web/astro.config.mjs`, `apps/web/package.json:15`). The only page is `src/pages/index.astro`, whose `#api-status` element (index.astro:16) is what `scripts/smoke.sh:33-38` checks. The dev server proxies `/api` to `http://localhost:5180` (`astro.config.mjs:6-15`). TypeScript extends `astro/tsconfigs/strict`.
- **Infra**: `infra/main.bicep:2` is subscription-scoped and creates `rg-ogarniamy-mvp` (:25-28), the App Service (`modules/app-service.bicep`, F1, no managed identity, only `ASPNETCORE_ENVIRONMENT` set, :38-43) and the Static Web App linked to it (`modules/static-web-app.bicep:24-31`). No PostgreSQL, Key Vault, managed identity or RBAC in the app template. `infra/deploy.sh:24-41` runs `az deployment sub what-if|create`.
- **Bootstrap**: `infra/bootstrap/main.bicep` (hand-applied) creates `rg-ogarniamy-cicd`, two OIDC identities, the `Ogarniamy What-If` custom role, and assigns the deploy identity **Contributor on the whole subscription** (`deployContributor`), which is the risk accepted in F-01 and deferred to S-01 as F3 (archive `2026-09-27-azure-walking-skeleton/follow-ups/review-fixes.md:5`, `reviews/impl-review.md:48-65`). The budget is $20 (`bootstrap.bicepparam:6`).
- **CI/CD**: `.github/workflows/ci.yml` builds web and API and runs Bicep lint and PR `what-if`; there is no `dotnet test`. `.github/workflows/deploy.yml:64-80` parses `deploy.sh apply` outputs (`resourceGroupName`, `apiAppName`, `apiDefaultHostName`, `staticWebAppName`, `staticWebAppDefaultHostName`), zip-deploys the API, uploads `apps/web/dist` to SWA and runs `scripts/smoke.sh`.
- **Hosting shape**: SWA Standard with the App Service as linked backend, so the page and `/api/*` share one origin. F-01 chose this explicitly for "simpler cookie auth in S-01" (archive plan-brief.md:23). The App Service rejects direct traffic, which smoke check 3 enforces (`scripts/smoke.sh:56-61`).
- **UI reference**: mockup E01 (Sign in / Create account tabs, Email, Password, one primary button) and E02 (heading "First animal", one "Animal's name" field, "Save and continue"); shell with Search / Add / Animals as a bottom tab bar on phone and a 232px sidebar on desktop; tokens in `context/foundation/ui-mockup/README.md:99-127`. There is no sign-out, password reset or email verification anywhere in the mockup (README.md:137 leaves these to slices).

## Desired End State

On the deployed environment, a new visitor lands on the Polish sign-in screen, creates an account with an email and password, is sent to the "first animal" onboarding screen, saves a name, and lands on the Animals page inside the app shell, which lists only their own animals. They stay signed in for 14 days (sliding), can sign out, and can switch to English. A second account never sees the first account's animals through any endpoint. The API connects to Azure PostgreSQL with its managed identity and no stored password. Self-hosted or local, the same API connects with an ordinary password connection string. The CI deploy identity holds Contributor only on `rg-ogarniamy-mvp`. Verify with the Phase 5 manual end-to-end pass plus the automated test suite and extended smoke test.

### Key Discoveries:

- The same-origin SWA → App Service proxy (`modules/static-web-app.bicep:24-31`) is what makes an HttpOnly cookie session viable without CORS. Whether `Set-Cookie` survives the proxy has not yet been checked on the deployed environment (Phase 5).
- The linked backend adds an "Azure Static Web Apps (Linked)" identity provider (Easy Auth) to the App Service (archive `azure-walking-skeleton/plan.md:37-38`). The app therefore uses cookies, not `Authorization` bearer headers, so it does not compete with that layer.
- The deploy identity is Contributor and cannot create role assignments (`bootstrap/main.bicep` roles). Every new Azure-side permission in this slice is a resource property, not an RBAC assignment: the PostgreSQL Entra administrator and firewall rules. The only role changes happen in the hand-applied bootstrap.
- PostgreSQL Flexible Server network mode is fixed at creation; a public-access-mode server can later gain a private endpoint and have public access disabled in place, while a VNet-injected server can never return to public mode ([quickstart](https://learn.microsoft.com/azure/postgresql/configure-maintain/quickstart-create-server), [networking how-to](https://learn.microsoft.com/azure/postgresql/network/how-to-networking)). App Service VNet integration requires B1+ ([docs](https://learn.microsoft.com/azure/app-service/overview-vnet-integration)).
- The "allow Azure services" (0.0.0.0) rule admits other customers' Azure resources and is flagged High by Defender for Cloud; App Service outbound IPs are shared per deployment stamp ([firewall rules](https://learn.microsoft.com/azure/postgresql/network/how-to-networking-servers-deployed-public-access-add-firewall-rules), [outbound IPs](https://learn.microsoft.com/azure/app-service/overview-inbound-outbound-ips)).
- Lessons (`context/foundation/lessons.md`): all code, context and UI strings in English; Polish only in translation files. Run commands without a login shell.
- AGENTS.md forbids credentials and connection strings in tracked files; this plan establishes the convention `dotnet user-secrets` + untracked `.env` for local development.

## What We're NOT Doing

- No password reset, email verification, "remember me" checkbox, MFA, social or external sign-in, and no email-sending service. A forgotten password means a lost account until reset arrives. Reset and verification are planned as PRD US-03 (FR-015, FR-016); authenticator-app MFA and Google/Apple sign-in as US-04 (FR-017, FR-018). Both are parked in the roadmap for milestone M-2. S-01's custom auth endpoints and Identity store must not prevent adding them.
- No Entra External ID or SWA built-in auth for owners; owner accounts are portable ASP.NET Core Identity.
- No species or date of birth on the animal (FR-003); no editing, inactive flag or multiple-animal management UI beyond listing (S-06); no animal profile page.
- No caretaker role, sharing, invitations or expiring access — only the `animal_members.role` column with the single value `owner`.
- No document capture, Blob Storage, queues, OCR or search (S-02 to S-05); the Search and Add tabs are placeholders.
- No private endpoint, VNet or B1 tier in this slice; moving to a private endpoint with public access disabled is recorded for S-03, when the plan moves to B1.
- No Key Vault; no database password exists in Azure.
- No PostgreSQL row-level security and no EF global query filters for isolation.
- No Astro SSR adapter; the site stays static.
- No end-to-end browser test framework (the user-perspective test belongs to S-04 per the F-01 plan).
- No cleanup of test accounts created during manual verification beyond what is described in Phase 5.

## Implementation Approach

Work proceeds bottom-up, and each phase is merged and deployed on its own. First the deploy identity is narrowed while the app still has no data (Phase 1). Then the database exists and the API reaches it in both connection modes (Phase 2). Then accounts, animals and isolation are built and proven by integration tests against a real PostgreSQL (Phase 3). Then the UI (Phase 4). Finally the deployed environment proves the cookie crosses the SWA proxy and that isolation holds end to end (Phase 5).

Isolation is enforced in one place: an owner-scoped access component that every animal query goes through, joining via `animal_members` on the current user id. S-04's raw pgvector SQL will reuse the same component. The API returns stable error codes, never human-readable text, and the UI maps them to strings from the `pl`/`en` catalogues.

## Critical Implementation Details

- **Bicep cycle between app settings and the PostgreSQL admin.** The server's Entra administrator needs the App Service's managed identity principal id, and the App Service's connection setting needs the server host name. Compute the server name deterministically in `main.bicep` and pass it into both modules, so the app setting uses `<name>.postgres.database.azure.com` without referencing the server resource. Firewall rules come from the App Service's `possibleOutboundIpAddresses`, which is only known at runtime. Pass the split array into the PostgreSQL module as a parameter and loop inside the module: a `for` loop over a runtime value at the template level fails with BCP178.
- **Removing a role assignment from Bicep does not delete it.** Deployments are incremental. After the bootstrap re-apply in Phase 1, the old subscription-scope Contributor assignment must be deleted explicitly with `az role assignment delete`. Resource providers such as `Microsoft.DBforPostgreSQL` must also be registered by the owner, because an RG-scoped Contributor cannot register providers.
- **Migrations at startup run as the managed identity.** In Azure the API's identity is the server's Entra administrator, so it owns the schema it creates. Migrations run before the app starts serving. A failed migration stops the app, and smoke check 2 catches it within its 600 s retry window.
- **Cookie behaviour.** Configure the application cookie to return 401/403 instead of redirecting (the default cookie handler redirects to `/Account/Login`). Endpoints accept JSON bodies only, and together with `SameSite=Lax` that is the CSRF defence: a cross-site form cannot send `application/json` without a CORS preflight, and no CORS is configured.
- **Data-protection keys.** Cookie encryption keys must survive restarts, or every owner is signed out on each deploy or cold start. The default key ring under `/home` survives only on App Service, not in a self-hosted container. The keys are therefore stored in PostgreSQL through `PersistKeysToDbContext<AppDbContext>()` (Phase 3), which works the same in every hosting mode. Phase 5 still checks it with a restart.

## Phase 1: Deploy identity narrowing (F3) and budget

### Overview

Before any personal data exists, give the CI deploy identity Contributor on `rg-ogarniamy-mvp` only, make the application template resource-group scoped, raise the budget alert to $40, and prove the unchanged app still deploys.

### Changes Required:

#### 1. Bootstrap owns the application resource group and scoped roles

**File**: `infra/bootstrap/main.bicep`

**Intent**: Create (idempotently) the application resource group `rg-ogarniamy-mvp` in the bootstrap and assign the deploy identity Contributor on that resource group instead of the subscription. The PR identity keeps Reader and `Ogarniamy What-If` at subscription scope, which cover `az deployment group what-if` via inheritance.

**Contract**: New resource `appResourceGroup` (`rg-ogarniamy-mvp`, bootstrap `location`); `deployContributor` role assignment moves into a small RG-scoped module (for example `infra/bootstrap/app-rg-roles.bicep`) with scope `appResourceGroup`; the subscription-scope `deployContributor` resource is removed. The new assignment's name must include its scope, for example `guid(appResourceGroup.id, deployIdentityName, contributorRoleId)`. Role assignment names are unique across the whole Entra tenant, so reusing the current `guid(subscription().id, …)` name would fail the re-apply while the old assignment still exists. No other role gains or loses permissions. The `Ogarniamy What-If` description may say "deployments" instead of "subscription deployments".

#### 2. Budget amount

**File**: `infra/bootstrap/bootstrap.bicepparam`

**Intent**: Raise the monthly budget to 40 so the 50/80/100 % alerts still mean something. Expected spend is about $28 per month: SWA Standard about $9 (already billed since F-01) plus the database about $19 (B1ms compute, 32 GiB storage, backup within the free allowance). That puts normal spend at about 70 % of the budget. `budgetStartDate` must stay unchanged, because it cannot be changed after creation.

**Contract**: `param budgetAmount = 40`.

#### 3. Resource-group-scoped application template

**File**: `infra/main.bicep`, `infra/environments/mvp.bicepparam`

**Intent**: Switch the entry point to `targetScope = 'resourceGroup'`, deploy the modules into the current resource group, and drop the resource group resource. Resource names and outputs stay the same, so `deploy.yml` output parsing continues to work.

**Contract**: No `appResourceGroup` resource; modules have no `scope`; `location` defaults to `resourceGroup().location` or stays a parameter; output `resourceGroupName` becomes `resourceGroup().name`. The App Service name (`uniqueString(subscription().id)`) must not change.

#### 4. Deploy script

**File**: `infra/deploy.sh`

**Intent**: Use group-scoped deployments against `rg-ogarniamy-mvp` for `what-if` and `apply`, keeping `--validation-level ProviderNoRbac` for `what-if`, and keep linting both templates.

**Contract**: `az deployment group what-if|create --resource-group rg-ogarniamy-mvp ...`; same `--name`, template, parameters and `--query properties.outputs --output json` for `apply`. Remove `DEPLOYMENT_LOCATION` if unused.

#### 5. Runbook for the hand-applied steps

**File**: `README.md` (infrastructure / bootstrap section)

**Intent**: Document the one-time owner steps that CI cannot do. These are: re-apply the bootstrap, delete the old subscription-scope Contributor assignment, and register the `Microsoft.DBforPostgreSQL` resource provider (needed in Phase 2, which the RG-scoped identity cannot do).

**Contract**: Exact commands: the `az deployment sub create` bootstrap apply with `BUDGET_ALERT_EMAIL`; `az role assignment delete --assignee <deploy principal id> --role Contributor --scope /subscriptions/<id>`; `az provider register --namespace Microsoft.DBforPostgreSQL`. Placeholders only, no real ids.

#### 6. Infrastructure decisions record

**File**: `context/foundation/infrastructure.md`

**Intent**: Replace the "accepted risk, F-01 review F3" bullet with the narrowed scope and record the $40 budget with the cost breakdown above.

**Contract**: "Decisions recorded" gains an S-01 subsection; the F3 bullet states the resolved scope.

### Success Criteria:

#### Automated Verification:

- Bicep lint passes for both templates: `infra/deploy.sh lint`
- PR `what-if` in CI runs at resource-group scope with the read-only PR identity and shows no deletions or replacements
- API and web still build: `dotnet build services/api/ogarniamy-zwierzaki-api.csproj` and `npm run build --prefix apps/web`

#### Manual Verification:

- Owner re-applied the bootstrap, deleted the subscription-scope Contributor assignment and registered `Microsoft.DBforPostgreSQL`
- `az role assignment list --assignee <deploy principal id> --all` shows Contributor only at `rg-ogarniamy-mvp` scope
- After merge, the `deploy` workflow succeeds end to end, including `scripts/smoke.sh`
- Budget `budget-ogarniamy-monthly` shows amount 40 with unchanged start date and thresholds

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Database foundation

### Overview

Provision PostgreSQL in Azure with Entra-only authentication, connect the API to it in two configurable modes, apply migrations at startup, run PostgreSQL locally in Docker, and introduce the xUnit test project against a real PostgreSQL in CI.

### Changes Required:

#### 1. PostgreSQL module

**File**: `infra/modules/postgres.bicep` (new)

**Intent**: Declare a Flexible Server in public-access mode, with only the App Service's outbound IPs allowed through the firewall and the App Service's managed identity as the only (Entra) administrator. Also declare the application database.

**Contract**: `Microsoft.DBforPostgreSQL/flexibleServers`, PostgreSQL 17, SKU `Standard_B1ms` (Burstable), 32 GiB storage, 7-day backup, no HA, no geo-redundant backup; `authConfig.activeDirectoryAuth = 'Enabled'`, `passwordAuth = 'Disabled'`, `tenantId = tenant().tenantId`; `network.publicNetworkAccess = 'Enabled'`; child `administrators` resource with `principalType: 'ServicePrincipal'`, `principalName` = App Service name, key = App Service principal id; child `databases` `ogarniamy`; child `firewallRules` looped over an `allowedIpAddresses array` parameter (one rule per IP, start = end); no `0.0.0.0` rule. Inputs: `serverName`, `location`, `apiAppName`, `apiPrincipalId`, `allowedIpAddresses`. `require_secure_transport` stays at its default (on).

#### 2. App Service identity and database settings

**File**: `infra/modules/app-service.bicep`

**Intent**: Give the API a system-assigned managed identity and the non-secret settings that select managed-identity mode and point at the server.

**Contract**: `identity: { type: 'SystemAssigned' }`; new param `postgresServerName`; app settings `Database__Auth = AzureManagedIdentity` and `ConnectionStrings__Default = Host=<postgresServerName>.postgres.database.azure.com;Database=ogarniamy;Username=<site name>;Ssl Mode=Require` (no password); outputs `principalId` and `possibleOutboundIpAddresses`.

#### 3. Wiring in the entry point

**File**: `infra/main.bicep`

**Intent**: Compute the server name once, pass it to both modules, and feed the App Service's outbound IPs into the PostgreSQL module (see Critical Implementation Details).

**Contract**: `var postgresServerName = 'psql-ogarniamy-${uniqueString(resourceGroup().id)}'`; `module postgres 'modules/postgres.bicep'` with `allowedIpAddresses: split(appService.outputs.possibleOutboundIpAddresses, ',')`. Existing outputs are unchanged; add `postgresServerName` output.

#### 4. Data access in the API

**Files**: `services/api/ogarniamy-zwierzaki-api.csproj`, `services/api/Data/AppDbContext.cs` (new), `services/api/Data/DatabaseSetup.cs` (new), `services/api/Program.cs`, `services/api/appsettings.json`

**Intent**: Add EF Core with Npgsql and register one `NpgsqlDataSource`. Its password comes from the connection string in `Password` mode (the default, used locally, in tests and self-hosted) or from a periodically refreshed Entra token in `AzureManagedIdentity` mode. Nothing else in the API knows which mode is active. Migrations run at startup, and `/api/health` reports healthy only if the database is reachable.

**Contract**: Packages `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design` (private assets), `EFCore.NamingConventions` (snake_case), `Azure.Identity`, `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`. Configuration keys: `ConnectionStrings:Default` (required) and `Database:Auth` (`Password` | `AzureManagedIdentity`, default `Password`). `appsettings.json` gets `"Database": { "Auth": "Password" }` and no connection string. `AppDbContext` starts empty apart from the conventions (Identity and animals arrive in Phase 3), with an initial migration under `services/api/Data/Migrations/`. `/api/health` keeps its route and `{"status":"ok"}` body on success and returns 503 when the database check fails. The token provider is the one non-obvious call:

```csharp
builder.UsePeriodicPasswordProvider(async (_, ct) =>
    (await credential.GetTokenAsync(
        new TokenRequestContext(["https://ossrdbms-aad.database.windows.net/.default"]), ct)).Token,
    TimeSpan.FromMinutes(55), TimeSpan.FromSeconds(5));
```

#### 5. Local PostgreSQL and secrets convention

**Files**: `compose.yaml` (new, repo root), `.env.example` (new), `.gitignore`, `README.md`

**Intent**: Run PostgreSQL 17 locally with a password read from an untracked `.env`. The API reads its local connection string from `dotnet user-secrets`. This becomes the repository's secret-storage convention.

**Contract**: `compose.yaml` service `postgres` (`postgres:17`, port 5432, named volume, `POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}`, `POSTGRES_DB: ogarniamy`); `.env.example` holds only `POSTGRES_PASSWORD=` with no value; `.env` is gitignored; the csproj gets a `UserSecretsId`; README documents `docker compose up -d`, `dotnet user-secrets set ConnectionStrings:Default "..." --project services/api` and `dotnet ef` usage. No password literal in any tracked file.

#### 6. Test project and CI

**Files**: `services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj` (new), `services/api.Tests/ApiFactory.cs` (new), `services/api.Tests/HealthTests.cs` (new), `.github/workflows/ci.yml`, `.github/workflows/deploy.yml`

**Intent**: Add an xUnit project that boots the API with `WebApplicationFactory` against a throwaway PostgreSQL from Testcontainers, and run it in CI and before deploy.

**Contract**: The project references the API project and the packages `xunit`, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql` (`postgres:17`). The shared factory injects the container connection string, keeps `Database:Auth=Password`, and applies migrations through normal startup. The first test asserts that `GET /api/health` returns 200 `{"status":"ok"}`. The CI `api` job adds `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`, and `deploy.yml` runs the same before publishing. The API publish command stays scoped to the API csproj so tests are never deployed.

### Success Criteria:

#### Automated Verification:

- Bicep lint passes: `infra/deploy.sh lint`
- PR `what-if` shows the new server, database, administrator and App Service identity/settings, with no deletions (the firewall rules loop over a runtime value, so what-if cannot list them; manual check 2.8 covers them)
- API builds with the new packages: `dotnet build services/api/ogarniamy-zwierzaki-api.csproj`
- Tests pass against Testcontainers PostgreSQL: `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`
- No password or connection string with a password in tracked files: `git grep -nIiE "password=|pwd=" -- . ':!*.md'` returns nothing

#### Manual Verification:

- Locally, `docker compose up -d` plus user-secrets lets `dotnet run` apply the migration and `GET http://localhost:5180/api/health` return `{"status":"ok"}`
- After merge, deploy succeeds and `https://<swa>/api/health` returns `{"status":"ok"}`, proving the managed-identity connection
- In the portal or CLI, the server shows password authentication disabled, one Entra administrator (the API identity), one firewall rule per App Service outbound IP and no `0.0.0.0` firewall rule

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Accounts and animals API

### Overview

Add ASP.NET Core Identity with a cookie session, the person–animal data model with a role, the owner-scoped access component, and the endpoints the UI needs. Prove them with integration tests, including cross-account denial.

### Changes Required:

#### 1. Identity and cookie session

**Files**: `services/api/Program.cs`, `services/api/Auth/AppUser.cs` (new), `services/api/Auth/AuthEndpoints.cs` (new)

**Intent**: Store accounts in PostgreSQL through ASP.NET Core Identity and use an HttpOnly cookie session. Every endpoint requires authentication by default. Hand-written endpoints expose only register, sign-in and sign-out (not `MapIdentityApi`, which would also expose reset, confirmation and 2FA routes this slice does not support).

**Contract**:
- Identity: `AddIdentityCore<AppUser>` with a `SignInManager` and EF stores on `AppDbContext`. `RequireUniqueEmail`; password `RequiredLength = 10` and the digit/upper/lower/non-alphanumeric requirements off; lockout after 5 failed attempts for 5 minutes.
- Cookie: name `oz_session`, HttpOnly, `SameSite=Lax`, `SecurePolicy=Always` outside Development, `ExpireTimeSpan = 14 days`, `SlidingExpiration = true`. Redirect events return 401 or 403.
- Authorization: the fallback policy requires an authenticated user; `/api/health` and the auth endpoints use `AllowAnonymous`.
- Routes:
  - `POST /api/auth/register {email, password}` creates the user and signs them in persistently, returning 201. Errors: 409 `email_taken`; 400 `invalid_email` or `password_too_short`.
  - `POST /api/auth/login {email, password}` returns 204 and sets the cookie. Errors: 401 `invalid_credentials` (also for an unknown email); 401 `locked_out`. Identity locks the account on the 5th failure itself, so attempts 1–4 return `invalid_credentials` and the 5th and later return `locked_out`.
  - `POST /api/auth/logout` returns 204 and clears the cookie.
- Errors use `ProblemDetails` with a `code` extension and no user-facing text. JSON bodies only.
- Data protection: `AddDataProtection().PersistKeysToDbContext<AppDbContext>()` (package `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`), so cookie keys survive restarts in every hosting mode.

#### 2. Data model

**Files**: `services/api/Animals/Animal.cs` (new), `services/api/Animals/AnimalMember.cs` (new), `services/api/Data/AppDbContext.cs`, new migration

**Intent**: Create the animal and the person–animal relationship that carries a role, which the PRD requires from day one so caretakers can be added later without moving ownership.

**Contract**: `AppDbContext : IdentityDbContext<AppUser>, IDataProtectionKeyContext` (adds the `data_protection_keys` table to the same migration).
- `animals`: `id uuid` PK, `name varchar(100) not null`, `created_at timestamptz not null`.
- `animal_members`: `animal_id` FK to animals, `user_id` FK to the Identity users table, `role text not null` with a check constraint allowing only `'owner'` (C# enum `AnimalRole.Owner` stored as `"owner"`), `created_at`; PK `(animal_id, user_id)`; index on `user_id`.
- The migration is added with `dotnet ef migrations add AccountsAndAnimals`.

#### 3. Owner-scoped access component

**File**: `services/api/Animals/OwnedAnimals.cs` (new)

**Intent**: This is the only code allowed to read or create animals. Every query takes the current user id and joins through `animal_members`, so an endpoint cannot accidentally read another account's data. S-02 (documents through animals) and S-04 (vector SQL) extend the same component.

**Contract**: A scoped service with `ListAsync(userId)` (ordered by `created_at`), `FindAsync(userId, animalId)` (null when the animal is not the user's), `CountAsync(userId)`, and `CreateAsync(userId, name)`. `CreateAsync` inserts the animal and its `owner` member row in one `SaveChanges`. Endpoints never touch `DbSet<Animal>` or `DbSet<AnimalMember>` directly.

#### 4. Session and animal endpoints

**Files**: `services/api/Animals/AnimalEndpoints.cs` (new), `services/api/Auth/MeEndpoint.cs` (new), `services/api/ogarniamy-zwierzaki-api.http`

**Intent**: Give the UI what it needs for gating, onboarding and the Animals list.

**Contract**: All routes are authenticated.
- `GET /api/me` returns `{ email, hasAnimals }`, or 401 when there is no session.
- `GET /api/animals` returns `[{ id, name }]` for the owner only.
- `GET /api/animals/{id}` returns 200 `{ id, name }`, or 404 when the animal is missing or belongs to someone else.
- `POST /api/animals { name }`: the name is trimmed. Errors: 400 `name_required` when empty, 400 `name_too_long` when over 100 characters. Success returns 201 with `{ id, name }` and a `Location` header.
- Duplicate names are allowed.
- The `.http` file gains samples for these routes.

#### 5. Integration tests

**Files**: `services/api.Tests/AuthTests.cs` (new), `services/api.Tests/AnimalTests.cs` (new), `services/api.Tests/IsolationTests.cs` (new)

**Intent**: Prove the auth contract and FR-002 isolation against the real HTTP pipeline and real PostgreSQL, with a cookie-aware client per account.

**Contract**: The cases in the Testing Strategy section below. Each test uses unique emails, so tests do not depend on each other.

### Success Criteria:

#### Automated Verification:

- Migration is generated and applies from scratch in tests: `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`
- Auth tests pass (register, duplicate, weak password, login, wrong password, lockout, logout, 401 without redirect)
- Animal and onboarding tests pass (create, validation, list, `hasAnimals` flips)
- Cross-account isolation tests pass (list, get by id, count via `/api/me`)
- API builds without warnings introduced by this phase: `dotnet build services/api/ogarniamy-zwierzaki-api.csproj`

#### Manual Verification:

- Using the `.http` file against the local API, a register → me → create animal → list → logout → me (401) sequence behaves as specified
- Reviewer confirms that no endpoint reads `DbSet<Animal>`/`DbSet<AnimalMember>` outside `OwnedAnimals`
- After merge and deploy, the session cookie survives the SWA proxy: `curl -c jar -H 'Content-Type: application/json' -d '{"email":"...","password":"..."}' https://<swa>/api/auth/register` returns 201, then `curl -b jar https://<swa>/api/me` returns 200 with that email (record the account like the Phase 5 verification accounts)

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 4: Frontend — shell, sign-in, onboarding, Animals

### Overview

Add React islands and Astro i18n routing (Polish at `/`, English under `/en/`), the message catalogues, the app shell, the E01 and E02 screens, a minimal Animals list, placeholder Search and Add tabs, sign-out and a language switch, all gated on the session.

### Changes Required:

#### 1. React and i18n configuration

**Files**: `apps/web/astro.config.mjs`, `apps/web/package.json`, `apps/web/tsconfig.json` (only what `astro add react` requires)

**Intent**: Enable React components on static pages and built-in i18n routing with Polish as the default locale, keeping static output and the dev proxy. Add a type-check script so strict TypeScript is enforced in CI.

**Contract**: Add the integration with `npx astro add react` (it installs `@astrojs/react`, `react` and `react-dom`). Set `i18n: { locales: ['pl', 'en'], defaultLocale: 'pl', routing: { prefixDefaultLocale: false } }`. Add dev dependencies `@astrojs/check` and `typescript`, and a script `"check": "astro check"`. The strict tsconfig stays strict.

#### 2. Message catalogues

**Files**: `apps/web/src/i18n/en.ts` (new), `apps/web/src/i18n/pl.ts` (new), `apps/web/src/i18n/index.ts` (new)

**Intent**: Put all UI copy, including the text for each API error `code`, in typed catalogues. English is the source; Polish is its translation. Polish appears only in `pl.ts`.

**Contract**: `en.ts` exports a `messages` object and a `Messages` type; `pl.ts` exports an object that `satisfies Messages`, so a missing key fails the type check. `index.ts` exports `type Locale = 'pl' | 'en'`, `getMessages(locale)`, and a helper to build a locale-aware path (Astro's `getRelativeLocaleUrl`). The agent drafts `pl.ts`, and the human corrects it (manual criterion).

#### 3. Layouts and shell

**Files**: `apps/web/src/layouts/BaseLayout.astro` (new), `apps/web/src/components/AppShell.tsx` (new), `apps/web/src/styles/tokens.css` (new)

**Intent**: Turn the mockup's shell into the real layout: a bottom tab bar on phone and a sidebar on desktop, both with Search / Add / Animals, plus a sign-out control and a language switch that the mockup lacks. Styling uses the mockup tokens.

**Contract**:
- `BaseLayout` sets `<html lang>` from the locale and a stable marker `data-app="ogarniamy-zwierzaki"` that the smoke test reads, and loads `tokens.css`.
- Tokens follow `context/foundation/ui-mockup/README.md:99-127`: the palette, radii, and errors shown as text plus an alert icon.
- `AppShell` props: `locale`, `messages`, `active: 'search' | 'add' | 'animals'`, `alternateLocaleHref`. Sign-out calls `POST /api/auth/logout` and then goes to the sign-in page.
- Breakpoint: tab bar below 768px, 232px sidebar at 768px and above.
- The sign-in and onboarding screens render outside the shell.

#### 4. Session gate and screens

**Files**: `apps/web/src/lib/api.ts` (new), `apps/web/src/components/SessionGate.tsx` (new), `apps/web/src/components/AuthForm.tsx` (new), `apps/web/src/components/FirstAnimalForm.tsx` (new), `apps/web/src/components/AnimalsList.tsx` (new), `apps/web/src/components/Placeholder.tsx` (new)

**Intent**: Build a small typed client for the Phase 3 endpoints, a gate that routes owners by session state, and the screens.

**Contract**:
- `api.ts` wraps `fetch` for the Phase 3 routes, sends JSON, and maps a non-2xx response to its `ProblemDetails` `code`.
- `SessionGate` calls `/api/me`: 401 goes to sign-in, `hasAnimals=false` goes to onboarding, and otherwise it renders its children. The sign-in page sends an already-signed-in visitor to the entry route. Until `/api/me` resolves, the gate shows a neutral loading state and never flashes protected content.
- `AuthForm` is E01: Sign in / Create account tabs, Email, Password, and one primary button following the tab. Registration signs the owner in and then continues through the gate. Error codes are shown as catalogue text.
- `FirstAnimalForm` is E02: one name field, trimmed, 1–100 characters, validated on the client as well. On success it goes to Animals.
- `AnimalsList` shows the owner's animal names.
- `Placeholder` is used for Search and Add and says the feature is coming.

#### 5. Routes per locale

**Files**: `apps/web/src/pages/index.astro` (replaced), `apps/web/src/pages/{signin,onboarding,animals,search,add}.astro` (new), `apps/web/src/pages/en/{index,signin,onboarding,animals,search,add}.astro` (new)

**Intent**: Thin route files per locale that render the shared components with that locale's messages. `/` (and `/en/`) is the entry route, which only runs the gate and lands the owner on Animals.

**Contract**: Route set `/`, `/signin/`, `/onboarding/`, `/animals/`, `/search/`, `/add/`, and the same under `/en/`. Islands hydrate with `client:load`. The old `#api-status` markup is removed, and change 6 replaces its smoke check in the same phase.

#### 6. Smoke page check

**File**: `scripts/smoke.sh`

**Intent**: The Phase 4 deploy runs `smoke.sh`, whose check 1 greps for `id="api-status"`. Replace that check in this phase so the deploy neither fails nor ships without a page check.

**Contract**: Check 1: `https://<swa>/signin/` and `https://<swa>/en/signin/` return 200 and contain `data-app="ogarniamy-zwierzaki"`. Checks 2 and 3 are unchanged.

### Success Criteria:

#### Automated Verification:

- Type check passes: `npm run check --prefix apps/web`
- Build passes and emits every route in both locales: `npm run build --prefix apps/web` and `ls apps/web/dist/{signin,onboarding,animals,search,add} apps/web/dist/en/{signin,onboarding,animals,search,add}`
- CI `web` job runs `npm run check` in addition to the build
- No Polish diacritics outside `pl.ts`: `git grep -nP "[ąćęłńóśźżĄĆĘŁŃÓŚŹŻ]" -- apps/web/src ':!apps/web/src/i18n/pl.ts'` returns nothing
- Smoke test with the new page check passes in the deploy workflow after merge

#### Manual Verification:

- Locally (web dev server plus API plus Docker PostgreSQL): register → onboarding → Animals works on a phone-width and a desktop-width viewport, matching the E01/E02/shell mockups in layout
- Signing in with an account that has no animal lands on onboarding; with an animal it lands on Animals; visiting `/animals/` signed out lands on sign-in
- Sign-out returns to sign-in, and `/animals/` is no longer reachable
- The language switch moves between `/animals/` and `/en/animals/` and the text changes accordingly
- The human reviewed and corrected every string in `pl.ts`

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 5: Deployed verification

### Overview

Extend the smoke test for the new app and prove on Azure that the session cookie survives the SWA proxy, that sessions survive restarts, and that isolation holds end to end.

### Changes Required:

#### 1. Smoke test

**File**: `scripts/smoke.sh`

**Intent**: Add a check that the authenticated API is reachable through the proxy, without creating accounts in production. The page check was already replaced in Phase 4.

**Contract**:
- Check 1 (from Phase 4) is unchanged.
- Check 2: `/api/health` returns `{"status":"ok"}`, which now includes the database. It keeps the 600 s retry.
- New check: `GET https://<swa>/api/me` without a cookie returns 401, proving the proxy reaches the authenticated API and no redirect happens.
- Check 3, direct API access returning 401/403, is unchanged.

#### 2. Documentation

**Files**: `README.md`, `context/foundation/infrastructure.md`

**Intent**: Update the README with the current product status (accounts and first animal implemented), local setup, tests, and the rollback note that data now exists (forward-only migrations). In the infrastructure decisions, record the database authentication, the network choice and its accepted risk, and the S-03 follow-up to add a private endpoint and disable public access when moving to B1.

**Contract**: README sections "Status", "Local development", "Verification" and "Rollback"; the infrastructure.md S-01 decisions subsection.

### Success Criteria:

#### Automated Verification:

- Smoke test passes in the deploy workflow after merge
- Full test suite passes in CI: `dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj`

#### Manual Verification:

- On the deployed site, registering account A sets the `oz_session` cookie through SWA, and A reaches Animals after onboarding
- In a second browser profile, account B registers, completes onboarding, and sees only B's animal; B requesting `/api/animals/<A's animal id>` gets 404
- After restarting the App Service (`az webapp restart`), A is still signed in
- Phone (real device) and desktop both complete the flow in Polish and in English
- The two verification accounts are recorded (emails only, not in tracked files) so they can be recognised later

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Testing Strategy

### Unit Tests:

- None separately. The behaviour is thin enough that integration tests against real PostgreSQL cover it better than mocks would.

### Integration Tests:

- Health: 200 `{"status":"ok"}` with the database up.
- Register: 201 plus cookie, after which `/api/me` returns the email and `hasAnimals=false`. A duplicate email returns 409 `email_taken` (also with different letter case). A 9-character password returns 400 `password_too_short`; a 10-character password is accepted.
- Login: correct credentials return 204 plus cookie. A wrong password returns 401 `invalid_credentials`. Wrong-password attempts 1–4 return 401 `invalid_credentials`; the 5th and every later attempt within 5 minutes return 401 `locked_out`, even with the correct password. An unknown email returns 401 `invalid_credentials`.
- Logout: afterwards `/api/me` returns 401.
- Without a cookie, `GET /api/animals` and `GET /api/me` return 401 with no `Location` header (no redirect).
- A non-JSON `POST /api/animals` returns 415.
- Animals: `"  Czarek  "` is stored as `Czarek`. Blank returns 400 `name_required`; 101 characters returns 400 `name_too_long`; 100 characters is accepted. After creation, `hasAnimals=true`, and the member row has role `owner`.
- Isolation: A and B each create an animal. A's list contains only A's animal and B's list only B's. B's `GET /api/animals/{A id}` returns 404. B's `hasAnimals` does not change when A creates animals.

### Manual Testing Steps:

1. Local flow on phone and desktop widths (Phase 4 manual list).
2. Deployed flow with two accounts in separate browser profiles (Phase 5 manual list).
3. App Service restart keeps the session.
4. Polish copy review.

## Performance Considerations

B1ms and F1 are small; nothing in this slice is heavy. F1 cold starts plus the first Entra token request and migration check can make the first request slow, which is why smoke check 2 keeps its 600 s retry. Identity's password hashing is deliberately CPU-costly and counts against F1's 60 CPU-minutes per day, which is acceptable at this user count.

## Migration Notes

- From now on, rolling back the code does not roll back the data. Migrations must be forward-compatible (expand, then contract); a rollback means redeploying earlier code that still reads the current schema. The README rollback section says so.
- Phase 1 changes the deployment scope. The existing subscription-level deployment record `ogarniamy-mvp` stays in history and is harmless. The resource group is not recreated (same name and location), so no resource is replaced. The PR `what-if` must confirm this before merge.
- The PostgreSQL network mode (public access) is fixed at creation. It still permits the planned S-03 move to a private endpoint with public access disabled, with no recreation.

## References

- PRD: `context/foundation/prd.md` (FR-001–FR-003, §Access Control, §Success Criteria Guardrails)
- Roadmap item: `context/foundation/roadmap.md` (S-01)
- Infrastructure decisions: `context/foundation/infrastructure.md`
- F-01 deferred F3: `context/archive/2026-09-27-azure-walking-skeleton/follow-ups/review-fixes.md:5`, `reviews/impl-review.md:48-65`
- Mockup: `context/foundation/ui-mockup/README.md` (E01, E02, shell, tokens :99-127)
- Lessons: `context/foundation/lessons.md`
- Current code: `services/api/Program.cs:17-18`, `infra/main.bicep`, `infra/bootstrap/main.bicep`, `infra/deploy.sh`, `.github/workflows/deploy.yml:64-122`, `scripts/smoke.sh`
- Azure docs: PostgreSQL networking (https://learn.microsoft.com/azure/postgresql/network/how-to-networking), Entra auth for PostgreSQL (https://learn.microsoft.com/azure/postgresql/security/security-entra-concepts), App Service VNet integration (https://learn.microsoft.com/azure/app-service/overview-vnet-integration), MCSB NS-2 (https://learn.microsoft.com/security/benchmark/azure/mcsb-v2-network-security)

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Deploy identity narrowing (F3) and budget

#### Automated

- [x] 1.1 Bicep lint passes for both templates — 22acaa0
- [x] 1.2 PR what-if runs at resource-group scope with no deletions or replacements — 22acaa0
- [x] 1.3 API and web still build — 22acaa0

#### Manual

- [x] 1.4 Owner re-applied bootstrap, deleted subscription Contributor, registered PostgreSQL provider — 22acaa0
- [x] 1.5 Deploy identity holds Contributor only on rg-ogarniamy-mvp — 22acaa0
- [x] 1.6 Deploy workflow succeeds including smoke test — 22acaa0
- [x] 1.7 Budget shows amount 40 with unchanged start date and thresholds — 22acaa0

### Phase 2: Database foundation

#### Automated

- [x] 2.1 Bicep lint passes — 17a4971
- [x] 2.2 PR what-if shows server, database, admin and App Service identity with no deletions — 17a4971
- [x] 2.3 API builds with the new packages — 17a4971
- [x] 2.4 Tests pass against Testcontainers PostgreSQL — 17a4971
- [x] 2.5 No password or password-bearing connection string in tracked files — 17a4971

#### Manual

- [x] 2.6 Local compose plus user-secrets gives a healthy API — 17a4971
- [x] 2.7 Deployed /api/health is ok through the managed-identity connection — 17a4971
- [x] 2.8 Server has password auth disabled, one Entra admin, outbound-IP firewall rules, no 0.0.0.0 rule — 17a4971

### Phase 3: Accounts and animals API

#### Automated

- [x] 3.1 Migration applies from scratch in tests — 98a1cd5
- [x] 3.2 Auth tests pass — 98a1cd5
- [x] 3.3 Animal and onboarding tests pass — 98a1cd5
- [x] 3.4 Cross-account isolation tests pass — 98a1cd5
- [x] 3.5 API builds without new warnings — 98a1cd5

#### Manual

- [x] 3.6 .http sequence behaves as specified locally — 98a1cd5
- [x] 3.7 No endpoint reads animal DbSets outside OwnedAnimals — 98a1cd5
- [x] 3.8 Deployed session cookie round-trips through the SWA proxy — 98a1cd5

### Phase 4: Frontend — shell, sign-in, onboarding, Animals

#### Automated

- [x] 4.1 Type check passes — 3b55186
- [x] 4.2 Build emits every route in both locales — 3b55186
- [x] 4.3 CI web job runs the type check — 3b55186
- [x] 4.4 No Polish diacritics outside pl.ts — 3b55186
- [x] 4.10 Smoke test with the new page check passes in deploy workflow — 008aea2

#### Manual

- [x] 4.5 Local register to Animals flow works on phone and desktop widths — 3b55186
- [x] 4.6 Session gate routes signed-out, no-animal and with-animal owners correctly — 3b55186
- [x] 4.7 Sign-out ends access to Animals — 3b55186
- [x] 4.8 Language switch changes route and text — 3b55186
- [x] 4.9 Human reviewed and corrected pl.ts — 3b55186

### Phase 5: Deployed verification

#### Automated

- [x] 5.1 Smoke test passes in deploy workflow — c71258c
- [x] 5.2 Full test suite passes in CI — e81e45c

#### Manual

- [x] 5.3 Deployed registration sets the session cookie through SWA — e81e45c
- [x] 5.4 Second account sees only its own animal and gets 404 for the other's — e81e45c
- [x] 5.5 Session survives an App Service restart — e81e45c
- [x] 5.6 Real phone and desktop complete the flow in both languages — e81e45c
- [x] 5.7 Verification accounts recorded outside tracked files — e81e45c
