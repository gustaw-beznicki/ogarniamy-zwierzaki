# Scaffold adapter manifest schema

Path: `context/foundation/scaffold-adapters/manifest.md`.

```yaml
---
adapter_manifest_schema: 1
generated_at: <ISO 8601 UTC>
handoff_path: <relative hand-off path>
handoff_sha256: <sha256 of exact hand-off bytes>
overall_status: ready | caution | blocked
components:
  - id: <component id>
    starter_id: <starter id>
    target_dir: <relative target>
    adapter_path: <relative adapter path>
    status: smoke-tested | local-cli-confirmed | docs-only | blocked
---

## Summary

<one paragraph describing sources checked, local CLI coverage, smoke tests and blockers>
```

Status aggregation:

- `ready`: every component is `smoke-tested`.
- `caution`: none is `blocked` or `docs-only`, and at least one is `local-cli-confirmed`.
- `blocked`: any component is `blocked` or `docs-only`. A docs-only adapter remains useful documentation, but is not execution-ready.

The manifest is an index and freshness record. It contains no executable instructions. Bootstrapper recomputes the hand-off hash and reads each component adapter in full.
