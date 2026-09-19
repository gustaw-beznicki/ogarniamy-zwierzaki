---
adapter_schema: 1
component_id: api
starter_id: dotnet
target_dir: services/api
staging_dir: .bootstrap-scaffold/api
generated_root: ogarniamy-zwierzaki-api
generated_at: 2026-09-19T01:07:23Z
status: smoke-tested
handoff_sha256: dd1f48145984d04f2f38c4da5bb3e6fa26acf2ae88774aa50487f04927c3302d
cli_executable: dotnet
cli_path: /usr/bin/dotnet
cli_version: "10.0.112"
latest_stable_version: "10.0.401"
help_sha256: af3b2255e5dd4c1279da3387507eed9966ec4eaec6d39c1b4a89a2e1b72fffb5
fresh_for_days: 7
---

## Official sources

- [Download .NET 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) — official release page checked on 2026-09-19; latest stable LTS SDK is `10.0.401`, released 2026-09-08, while .NET 11 remains a release candidate.
- [.NET default templates for `dotnet new`](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new-sdk-templates) — official template reference checked on 2026-09-19; establishes the `webapi` template, `net10.0`, minimal APIs by default, OpenAPI support, and `--no-restore`.
- [`dotnet new`](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new) — official CLI reference checked on 2026-09-19; establishes name, output, dry-run, framework, and update-check behavior.
- [`dotnet package list`](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-list) — official audit reference checked on 2026-09-19; establishes `--include-transitive`, `--vulnerable`, and `--no-restore`.

## Environment discovered

The local executable is `/usr/bin/dotnet`, SDK version `10.0.112`. `dotnet new webapi --help` confirmed the template and every flag used below; its normalized SHA-256 is recorded in the frontmatter. The latest stable SDK is `10.0.401`, but the hand-off explicitly constrains scaffolding to installed SDK `10.0.112`. The tested local version therefore satisfies the project-specific compatibility requirement.

## Naming and paths

- Raw and logical project name: `ogarniamy-zwierzaki-api`
- Invocation working directory: `.bootstrap-scaffold/api`
- Generated source root: `.bootstrap-scaffold/api/ogarniamy-zwierzaki-api`
- Final target directory: `services/api`

## Scaffold instruction

From `.bootstrap-scaffold/api`, invoke the locally confirmed generator as one process:

```text
dotnet new webapi --name ogarniamy-zwierzaki-api --output ogarniamy-zwierzaki-api --framework net10.0 --no-restore --no-update-check
```

## Expected result

The generated source root is `.bootstrap-scaffold/api/ogarniamy-zwierzaki-api`. It must contain `ogarniamy-zwierzaki-api.csproj`, `Program.cs`, `appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json`, and `ogarniamy-zwierzaki-api.http`. The project must target `net10.0`, use `Microsoft.NET.Sdk.Web`, and set the sanitized root namespace to `ogarniamy_zwierzaki_api`. Authored source and configuration must not contain `.bootstrap-scaffold`, a temporary-directory suffix, or another staging-only identity. Transient `bin/` and `obj/` outputs are build artifacts, not scaffold source to transfer.

## Dependency setup

From the generated source root, run:

```text
dotnet restore
```

## Build or smoke verification

From the generated source root, run:

```text
dotnet build --no-restore
```

Success means exit code `0`, zero build errors, and an application assembly under `bin/Debug/net10.0/`.

## Dependency audit

From the generated source root, run:

```text
dotnet package list --include-transitive --vulnerable --no-restore
```

## Smoke-test evidence

Tested with the hand-off-constrained SDK `10.0.112` in a fresh `/tmp/10x-scaffold-adapter.XXXXXX` directory that was removed afterward. Help, scaffold, restore, and vulnerability audit exited `0`. The first build attempt inside the restricted sandbox exited `134` with an internal CLR error; the identical build outside that restriction exited `0` with zero warnings and errors. The expected files, `net10.0` target, sanitized root namespace, and package identity were observed. NuGet reported no vulnerable packages. Cleanup exited `0`; the absolute temporary path does not survive. SDK `10.0.401` was researched as the latest stable release but is intentionally outside this adapter's compatibility scope.

## Safety notes

This adapter is executable only with the explicitly constrained SDK `10.0.112`; a different active SDK requires rerunning `/10x-scaffold-adapter`. Restore and vulnerability checks access NuGet and may write to the user package cache. The scaffold command itself disables implicit restore and template update checks. The skill did not install or update any CLI tool.
