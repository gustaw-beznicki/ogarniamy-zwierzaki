# Multi-component staging, identity validation and merge

## Invariants

- Every component writes only below its adapter-declared path inside `.bootstrap-scaffold/`.
- Final targets are relative, normalized, inside the invocation workspace and non-overlapping. `.` is allowed only for the sole `app` component.
- All components complete scaffold, required dependency setup/build and identity validation before the first merge.
- A staging directory name never becomes project identity.
- Existing user files and `context/` are never overwritten.

## Before execution

Snapshot the workspace paths relevant to target dirs and staging. Create `.bootstrap-scaffold/` and only the component parents required by adapters. Do not pre-create the exact CLI output directory when official CLI behavior requires it to be absent.

## After each scaffold

Using the adapter's `Expected result` section:

1. Resolve exactly one generated source root.
2. Require every listed path.
3. Scan generated path names and text identity surfaces for forbidden staging tokens.
4. Confirm the logical project identity where manifests expose it.
5. Compare workspace snapshot and fail if the CLI wrote outside its staging boundary.
6. Capture a sorted output tree for the verification log.

Do not infer a different root or silently rename identity files. A mismatch means the adapter is wrong or stale.

## Merge planning

Create the complete plan for all components before moving anything. For each generated path compare the destination under its component `target_dir`:

| Generated path | Destination exists | Resolution |
| --- | --- | --- |
| `context/**` | yes or no | Drop generated copy; workspace context is authoritative |
| `.gitignore` | yes | Preserve destination order; append exact-line-deduplicated generated entries after `# from <starter_id>` |
| `.gitignore` | no | Move unchanged |
| anything else | yes | Keep existing file; move generated file to adjacent `<filename>.scaffold` |
| anything else | no | Move unchanged |

Show counts and all conflicts before applying the plan.

## Applying the plan

Execute deterministic filesystem operations only; do not run adapter commands during merge. Record each directory creation, move, drop, merge and `.scaffold` sibling. If a filesystem operation fails, stop immediately, preserve remaining staging and record the partial merge. Never try a broad cleanup or recursive deletion of unresolved paths.

After a successful merge, remove only empty, exactly resolved component staging directories and then the empty `.bootstrap-scaffold/`. If anything unexpected remains, keep it and report it rather than forcing deletion.
