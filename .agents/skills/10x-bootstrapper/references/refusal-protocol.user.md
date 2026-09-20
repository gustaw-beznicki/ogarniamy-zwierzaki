# Bootstrapper refusal and confirmation protocol

## Missing hand-off — hard refusal

Copy `/10x-tech-stack-selector`, print:

```text
Bootstrapper requires a tech-stack hand-off at `<handoff-path>`. Run `/10x-tech-stack-selector` first, then re-invoke.
```

and stop.

## Missing adapter manifest — hard refusal

Copy `/10x-scaffold-adapter`, print:

```text
Bootstrapper requires fresh scaffold adapters at `context/foundation/scaffold-adapters/manifest.md`. Run `/10x-scaffold-adapter` first, then re-invoke.
```

and stop without reading registry `cmd_template` as fallback.

## Stale, inconsistent or blocked adapter — hard refusal

Copy `/10x-scaffold-adapter`, print the exact failed check, then:

```text
The scaffold adapter cannot be executed safely. Re-run `/10x-scaffold-adapter` to research and verify the current official CLI instructions.
```

Do not offer an inline correction or override for `blocked`, `docs-only`, hash mismatch, expired evidence, CLI path/version/help mismatch, invalid target paths or malformed command blocks.

## Existing `.bootstrap-scaffold/` — hard refusal

Print:

```text
Bootstrapper found an existing `.bootstrap-scaffold/` working directory and will not overwrite it. Inspect it first, then rename or remove it explicitly and re-invoke `/10x-bootstrapper`.
```

Do not delete it automatically.

## Populated target — warn and confirm

Detect common manifests in each target (`package.json`, `*.csproj`, `Cargo.toml`, `pyproject.toml`, `requirements.txt`, `Gemfile`, `go.mod`, `pom.xml`, `build.gradle`, `composer.json`, `pubspec.yaml`). Show component id and exact files.

Ask whether to continue with strict conflict handling or abort. Existing files always win; generated conflicts become adjacent `.scaffold` files; `context/` is preserved; `.gitignore` is append-merged.

## Weak adapter evidence — warn and confirm

If any adapter is `local-cli-confirmed`, explain exactly which smoke-test evidence is missing. Offer:

- **Proceed with caution** — execute the shown instructions with normal sandbox/approvals.
- **Stop and smoke-test** — route to `/10x-scaffold-adapter`.

Never present the caution path as recommended.

`docs-only` follows the hard-refusal path above; it is documentation, not an executable plan.

## Runtime hard stop

Non-zero scaffold/build exit, missing required identity, forbidden staging token, unexpected writes outside staging or ambiguous generated root all stop the run before merge. Preserve staging, write a partial verification log, copy `/10x-bootstrapper` and report the failing component and invariant.
