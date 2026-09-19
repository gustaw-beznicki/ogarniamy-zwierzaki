---
bootstrapped_at: 2026-09-19T01:14:57Z
handoff_path: context/foundation/tech-stack.md
handoff_sha256: dd1f48145984d04f2f38c4da5bb3e6fa26acf2ae88774aa50487f04927c3302d
adapter_manifest_path: context/foundation/scaffold-adapters/manifest.md
adapter_manifest_sha256: 046aa01423c44c5e5687ff6bf88e42f01cff73c5ea77d345d3425ad2b96ca0ac
component_count: 2
adapter_overall_status: ready
phase_3_status: ok
---

## Hand-off

Path: `context/foundation/tech-stack.md`

SHA-256: `dd1f48145984d04f2f38c4da5bb3e6fa26acf2ae88774aa50487f04927c3302d`

Verbatim hand-off:

```markdown
---
starter_id: dotnet
package_manager: dotnet
project_name: ogarniamy-zwierzaki-api
components:
  - id: api
    starter_id: dotnet
    package_manager: dotnet
    project_name: ogarniamy-zwierzaki-api
    target_dir: services/api
    language_family: dotnet
  - id: web
    starter_id: astro
    package_manager: npm
    project_name: ogarniamy-zwierzaki-web
    target_dir: apps/web
    language_family: js
hints:
  language_family: multi
  team_size: solo
  deployment_target: azure-app-service
  ci_provider: github-actions
  ci_default_flow: auto-deploy-on-merge
  bootstrapper_confidence: verified
  path_taken: custom
  quality_override: false
  self_check_answers:
    typed: true
    from_official_starter: true
    conventions: true
    docs_current: true
    can_judge_agent: true
  has_auth: true
  has_payments: false
  has_realtime: false
  has_ai: true
  has_background_jobs: true
---

## Why this stack

Ogarniamy zwierzaki is split into an Astro and TypeScript frontend in `apps/web` and an ASP.NET Core API in `services/api`, with .NET retained as the primary component and Azure App Service as the recorded deployment target. This keeps authentication, private document storage, PostgreSQL with vector search, OCR, embeddings, and background processing in the developer's strongest ecosystem while giving the mobile-first interface a lightweight frontend. Astro passes all four agent-friendly gates, but its content-first bias is an accepted tradeoff for this application-shaped UI; the separate .NET API owns application logic. Both registered paths have verified historical scaffolding confidence, which the scaffold-adapter will re-check against current official CLI documentation. For scaffolding, the API is explicitly constrained to the locally installed .NET SDK `10.0.112`; SDK `10.0.401` is not required. GitHub Actions remains configured for automatic deployment after merges.
```

## Adapter evidence

Manifest `context/foundation/scaffold-adapters/manifest.md` has SHA-256 `046aa01423c44c5e5687ff6bf88e42f01cff73c5ea77d345d3425ad2b96ca0ac`, schema `1`, and status `ready`. Its component identities and non-overlapping targets match the hand-off.

### api

- Adapter: `context/foundation/scaffold-adapters/api.md`
- SHA-256: `f61bd51532eca02e8db0e4c7704c04a28914dee0091939dcadbcca7223b324df`
- Status: `smoke-tested`
- Generated: `2026-09-19T01:07:23Z`; freshness window: 7 days
- Official sources: `https://dotnet.microsoft.com/en-us/download/dotnet/10.0`, `https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new-sdk-templates`, `https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new`, `https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-list`
- CLI: `/usr/bin/dotnet`; project-constrained version `10.0.112`; latest researched stable version `10.0.401`
- Freshness recheck: `which dotnet` resolved `/usr/bin/dotnet`; `dotnet --version` returned `10.0.112`; `dotnet new webapi --help` exited `0`; normalized help SHA-256 matched `af3b2255e5dd4c1279da3387507eed9966ec4eaec6d39c1b4a89a2e1b72fffb5`.

### web

- Adapter: `context/foundation/scaffold-adapters/web.md`
- SHA-256: `f04961551d56a10a614536465cd97163ae0a43666d4ba3cd7ad0dbc3d6a13e3c`
- Status: `smoke-tested`
- Generated: `2026-09-19T01:07:23Z`; freshness window: 7 days
- Official sources: `https://docs.astro.build/en/install-and-setup/`, `https://www.npmjs.com/package/create-astro`, `https://github.com/withastro/astro/releases`
- Runner: `/home/gustawb/.nvm/versions/node/v24.21.0/bin/npm` version `12.0.2`; pinned generator/latest stable generator `create-astro@5.2.4`
- Freshness recheck: `which npm` resolved the recorded path; `npm --version` returned `12.0.2`; `npm create astro@5.2.4 -- --help` exited `0`; normalized help SHA-256 matched `7ffdc1b92544dd24c26407df86ff2ed7b047348c7f888151ffec19a22454e998`.

## Confirmed execution plan

The user chose the normal **Proceed** path after both adapters were shown as `smoke-tested`. This confirmation applied only to the exact processes and locations below; no file-based approval token was used.

### api

- Logical name: `ogarniamy-zwierzaki-api`
- Staging: `.bootstrap-scaffold/api`
- Generated root: `.bootstrap-scaffold/api/ogarniamy-zwierzaki-api`
- Target: `services/api`
- Scaffold: `dotnet new webapi --name ogarniamy-zwierzaki-api --output ogarniamy-zwierzaki-api --framework net10.0 --no-restore --no-update-check`
- Dependency setup: `dotnet restore`
- Build: `dotnet build --no-restore`
- Post-merge audit: `dotnet package list --include-transitive --vulnerable --no-restore`
- Network/cache: restore and audit may contact NuGet and write the user NuGet cache. The build was approved outside the restrictive sandbox because the adapter smoke test had demonstrated a sandbox-only CLR failure.

### web

- Logical name: `ogarniamy-zwierzaki-web`
- Staging: `.bootstrap-scaffold/web`
- Generated root: `.bootstrap-scaffold/web/ogarniamy-zwierzaki-web`
- Target: `apps/web`
- Scaffold: `npm create astro@5.2.4 ogarniamy-zwierzaki-web -- --template minimal --no-install --no-git --no-ai --yes`
- Dependency setup: `npm install`
- Build: `npm run build`
- Post-merge audit: `npm audit --audit-level=high`
- Network/cache: generator execution, dependency installation, and audit may contact npm and write the npm cache. Git initialization and generated AI instruction files were disabled.

Both targets were absent before execution. `.bootstrap-scaffold/` was also absent. Existing workspace `context/` content was declared authoritative and preserved.

## Scaffold execution

### api

1. Command: `dotnet new webapi --name ogarniamy-zwierzaki-api --output ogarniamy-zwierzaki-api --framework net10.0 --no-restore --no-update-check`
   - CWD: `.bootstrap-scaffold/api`
   - Exit code: `0`
   - Bounded output: `The template "ASP.NET Core Web API" was created successfully.`
2. Command: `dotnet restore`
   - CWD: `.bootstrap-scaffold/api/ogarniamy-zwierzaki-api`
   - Exit code: `0`
   - Bounded output: project restored successfully in 167 ms.
3. Command: `dotnet build --no-restore`
   - CWD: `.bootstrap-scaffold/api/ogarniamy-zwierzaki-api`
   - Exit code: `0`
   - Bounded output: build succeeded with 0 warnings and 0 errors; output assembly was created under `bin/Debug/net10.0/`.

Sorted source tree observed before merge:

```text
Program.cs
Properties/launchSettings.json
appsettings.Development.json
appsettings.json
ogarniamy-zwierzaki-api.csproj
ogarniamy-zwierzaki-api.http
```

All required paths existed. The project used `Microsoft.NET.Sdk.Web`, targeted `net10.0`, and declared `RootNamespace` as `ogarniamy_zwierzaki_api`. The authored source/configuration scan found no staging token. Workspace status showed only the pre-existing hand-off/adapter changes plus `.bootstrap-scaffold/`, so no scaffold write outside staging was detected.

### web

1. Command: `npm create astro@5.2.4 ogarniamy-zwierzaki-web -- --template minimal --no-install --no-git --no-ai --yes`
   - CWD: `.bootstrap-scaffold/web`
   - Exit code: `0`
   - Bounded output: `create-astro` used the `minimal` template, skipped dependency installation and Git initialization, and reported project initialization success.
2. Command: `npm install`
   - CWD: `.bootstrap-scaffold/web/ogarniamy-zwierzaki-web`
   - Exit code: `0`
   - Bounded output: 187 packages added, 188 audited, zero vulnerabilities reported at installation time.
3. Command: `npm run build`
   - CWD: `.bootstrap-scaffold/web/ogarniamy-zwierzaki-web`
   - Exit code: `0`
   - Bounded output: Astro produced one static page in `dist/`; build completed successfully.

Sorted source tree observed before merge:

```text
.gitignore
.vscode/extensions.json
.vscode/launch.json
README.md
astro.config.mjs
package-lock.json
package.json
public/favicon.ico
public/favicon.svg
src/pages/index.astro
tsconfig.json
```

All required paths existed. `package.json` identified `ogarniamy-zwierzaki-web`, declared Astro `^7.3.3`, and required Node.js `>=22.12.0`. No nested `.git/` existed, the authored source/configuration scan found no staging token, and no scaffold write outside staging was detected.

## Merge log

The complete plan contained 17 source-file moves, zero destination conflicts, zero `.scaffold` siblings, and no `.gitignore` merge because the web target did not exist. `context/` was not touched.

Before moving source files, verified transient outputs were dropped:

- API: `bin/`, `obj/`
- Web: `node_modules/`, `dist/`, `.astro/`

Applied directory creation:

- `services/api/Properties`
- `apps/web/public`
- `apps/web/src/pages`
- `apps/web/.vscode`

Applied API moves:

- `Program.cs`
- `ogarniamy-zwierzaki-api.http`
- `appsettings.Development.json`
- `appsettings.json`
- `ogarniamy-zwierzaki-api.csproj`
- `Properties/launchSettings.json`

Applied web moves:

- `.gitignore`
- `.vscode/extensions.json`
- `.vscode/launch.json`
- `README.md`
- `astro.config.mjs`
- `package.json`
- `package-lock.json`
- `tsconfig.json`
- `public/favicon.ico`
- `public/favicon.svg`
- `src/pages/index.astro`

All moves exited `0`. The now-empty generated roots, component staging directories, and `.bootstrap-scaffold/` were removed with `rmdir`. No staging leftovers or unexpected files remained. A final scan found no staging tokens in either target.

## Dependency audits

### api

- Command: `dotnet package list --include-transitive --vulnerable --no-restore`
- CWD: `services/api`
- Exit code: `1`
- Raw bounded output: no assets file was found and the CLI requested a restore; NuGet source `https://api.nuget.org/v3/index.json` was listed.
- Result: inconclusive. The verified staging audit from the adapter had no findings, but this post-merge run cannot be reported as zero vulnerabilities because transient `obj/` assets were intentionally not transferred.

### web

- Command: `npm audit --audit-level=high`
- CWD: `apps/web`
- Exit code: `0`
- Raw bounded output: `found 0 vulnerabilities`
- Result: zero npm vulnerabilities reported.

Audit results are informational and do not change `phase_3_status: ok`.

## Next steps

- Run `dotnet restore` in `services/api`, then repeat the recorded NuGet vulnerability audit to obtain a conclusive post-merge result.
- Run `npm install` in `apps/web` before local development; `node_modules/` was deliberately not transferred from staging.
- There are no `.scaffold` siblings, merge conflicts, weak-evidence adapters, or staging leftovers.
- The scaffold, updated hand-off, adapters, and this verification log remain uncommitted.
