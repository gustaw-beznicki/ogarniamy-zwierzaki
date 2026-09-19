# Consuming scaffold adapters safely

Bootstrapper consumes Markdown adapters created by `/10x-scaffold-adapter`. It does not consume commands from the starter registry and does not parse adapter commands into an executable data structure.

## Required files

- `context/foundation/scaffold-adapters/manifest.md`
- every `adapter_path` listed by the manifest

Read each file in full. Parse YAML frontmatter only for evidence and identity. Commands are allowed only in the required Markdown body sections.

## Manifest checks

1. `adapter_manifest_schema` equals `1`.
2. Recompute SHA-256 over the exact hand-off bytes. It must equal `handoff_sha256` in the manifest and every adapter.
3. `overall_status` matches component aggregation.
4. Component ids, starter ids and target dirs match the hand-off. For legacy hand-offs without `components`, require exactly one manifest component with id `app`.
5. Every path is relative, normalized, inside `context/foundation/scaffold-adapters/` for adapter files and inside the workspace for targets.
6. Target dirs are unique and non-overlapping. Allow `.` only when the manifest contains exactly one component with id `app`; reject it for multi-component plans. Always reject absolute paths, parent traversal, `.git`, `context/archive` and `.bootstrap-scaffold` as final targets.

## Adapter checks

1. `adapter_schema` equals `1` and all required body headings from the producer schema exist exactly once and in order.
2. Frontmatter contains no keys whose name or value represents a command, arguments, shell fragment, environment variable or authorization.
3. `status: blocked` or `status: docs-only` is a hard refusal.
4. `generated_at + fresh_for_days` has not elapsed.
5. Official sources are present and use HTTP(S) URLs.
6. Scaffold instruction contains one command block and one process invocation. Reject shell wrappers, `eval`, pipes, redirects, command substitution, `&&`, `||`, semicolons and backgrounding.
7. Optional dependency, build and audit sections follow the same single-process rule.
8. `staging_dir` equals `.bootstrap-scaffold/<component_id>` and `generated_root` resolves inside it without traversal. Neither may be derived from the command text.

## Local freshness recheck

Before showing the execution plan:

1. Resolve `cli_executable` again and compare its path to `cli_path`.
2. Run the documented read-only version invocation and compare the normalized version.
3. Run the documented help invocation, normalize it as the producer did, calculate SHA-256 and compare with `help_sha256`.
4. A mismatch makes the adapter stale. Do not edit or reinterpret it; route to `/10x-scaffold-adapter`.

`docs-only` may have null local evidence and cannot proceed. Route it back to `/10x-scaffold-adapter` after the user has made the documented CLI available. `local-cli-confirmed` requires the caution choice because it lacks an isolated scaffold test.

## No automatic execution

Do not feed adapter Markdown to a script or parser that launches processes. The agent reads the instruction, shows it to the user, and invokes the exact process through the ordinary terminal tool. The terminal tool remains the enforcement point for sandboxing, network escalation and filesystem permissions.
