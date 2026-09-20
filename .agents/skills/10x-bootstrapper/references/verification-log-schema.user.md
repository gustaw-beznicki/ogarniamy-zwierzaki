# Bootstrap verification log schema

Path: `context/changes/bootstrap-verification/verification.md`.

## Frontmatter

```yaml
---
bootstrapped_at: <ISO 8601 UTC>
handoff_path: <relative path>
handoff_sha256: <sha256>
adapter_manifest_path: context/foundation/scaffold-adapters/manifest.md
adapter_manifest_sha256: <sha256>
component_count: <integer>
adapter_overall_status: ready | caution
phase_3_status: ok | failed
---
```

`phase_3_status: ok` requires every component scaffold/build requirement and identity validation to pass. Audit findings do not change it.

## Required sections

### `## Hand-off`

Record path, hash and verbatim hand-off.

### `## Adapter evidence`

For every component record adapter path/hash, status, generated time, freshness window, official sources, CLI path/version/latest stable/help hash and freshness recheck result.

### `## Confirmed execution plan`

Record exactly what was shown to the user: component, logical name, staging, target, scaffold/dependency/build/audit instructions and known network/cache writes. State whether the normal or caution confirmation path was used. Do not claim a file-based approval token.

### `## Scaffold execution`

For every invoked process record exact command text, cwd, exit code and bounded stdout/stderr. Record output tree, required-path checks, forbidden-token scan, identity result and outside-staging write check.

### `## Merge log`

Record the complete planned and applied operations, conflicts, `.gitignore` handling, staging cleanup and unexpected leftovers.

### `## Dependency audits`

Record each adapter-provided audit instruction, exit code, raw-output location/summary and parsed severities when possible. A missing audit tool is a documented skip, not zero findings.

### `## Next steps`

List unresolved `.scaffold` siblings, audit findings, weak-evidence adapters and manual actions. Do not claim the project is verified when `phase_3_status: failed`.

## Partial hard-stop log

On failure before merge, still write frontmatter with `phase_3_status: failed`, adapter evidence, confirmed plan and execution up to the failure. Replace merge/audit results with explicit `not run` reasons and retain the exact staging path for inspection.
