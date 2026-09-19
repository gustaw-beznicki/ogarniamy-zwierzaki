# Component scaffold adapter schema

Adapter is a human- and agent-readable Markdown instruction. Nothing reads command blocks as executable configuration.

## Frontmatter

```yaml
---
adapter_schema: 1
component_id: <kebab-case>
starter_id: <registry starter id>
target_dir: <relative path inside workspace>
staging_dir: .bootstrap-scaffold/<component-id>
generated_root: <relative path inside staging_dir, or .>
generated_at: <ISO 8601 UTC>
status: smoke-tested | local-cli-confirmed | docs-only | blocked
handoff_sha256: <sha256 of exact hand-off bytes>
cli_executable: <basename only>
cli_path: <resolved local path or null>
cli_version: <detected version or null>
latest_stable_version: <version or unknown>
help_sha256: <normalized help hash or null>
fresh_for_days: 7
---
```

Frontmatter contains evidence and identity only. It MUST NOT contain a command, argument list, shell fragment, environment variable, secret, or authorization marker. `target_dir: .` is permitted only for the sole legacy/single component `app`. `staging_dir` must equal `.bootstrap-scaffold/<component-id>`; `generated_root` must resolve inside it.

## Required body sections

Use this exact order:

1. `## Official sources` — direct URLs, title, version/date, and what each source establishes.
2. `## Environment discovered` — local executable, version, relevant help invocation, compatibility with latest stable.
3. `## Naming and paths` — raw project name, logical name, staging directory and final target directory.
4. `## Scaffold instruction` — prose first, then one exact command block. One process only; no shell composition.
5. `## Expected result` — source root, required paths, forbidden staging tokens, expected identity.
6. `## Dependency setup` — optional separate command block or `Not required`.
7. `## Build or smoke verification` — optional separate command block and success criteria.
8. `## Dependency audit` — optional separate command block or documented reason for no suitable tool.
9. `## Smoke-test evidence` — temp directory class (never a surviving absolute path), exit codes, observed files, identity check, cleanup result.
10. `## Safety notes` — network/cache writes, permissions, known interactions, and unresolved risks.

## Command block rules

- Exactly one process invocation per block.
- No prompt text, `$` prefix, comments, continuations that hide extra commands, pipes, redirections, `&&`, `;`, command substitution, `eval`, or shell wrappers.
- Use concrete resolved values for the project being scaffolded; do not leave placeholders in a `smoke-tested` adapter.
- Paths must remain relative to the invocation workspace except for the read-only resolved CLI path recorded in prose/frontmatter.
- Installation/update instructions may be documented in `Safety notes`, but never as the scaffold command and never as something already authorized.

## Status rules

- `smoke-tested`: latest stable official sources checked, matching local/pinned CLI help confirmed, scaffold ran in a fresh temp directory, expected identity and tree passed, temp directory cleaned.
- `local-cli-confirmed`: latest stable official sources and matching local/pinned CLI help confirmed; generator not run.
- `docs-only`: official sources checked but local executable/help not confirmed; informational and not executable by bootstrapper.
- `blocked`: no safe deterministic procedure, unsupported interaction, conflicting docs/help, missing required CLI, or unresolvable version mismatch.
