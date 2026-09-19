---
adapter_manifest_schema: 1
generated_at: 2026-09-19T01:07:23Z
handoff_path: context/foundation/tech-stack.md
handoff_sha256: dd1f48145984d04f2f38c4da5bb3e6fa26acf2ae88774aa50487f04927c3302d
overall_status: ready
components:
  - id: api
    starter_id: dotnet
    target_dir: services/api
    adapter_path: context/foundation/scaffold-adapters/api.md
    status: smoke-tested
  - id: web
    starter_id: astro
    target_dir: apps/web
    adapter_path: context/foundation/scaffold-adapters/web.md
    status: smoke-tested
---

## Summary

Official current sources and local CLI evidence were checked for both components. Astro's pinned `create-astro@5.2.4` instruction passed an isolated scaffold, separate dependency installation, build, identity check, dependency audit, and cleanup. The .NET instruction passed scaffold, restore, build, identity, and vulnerability checks with SDK `10.0.112`, which the hand-off now records as an explicit project compatibility constraint. Both adapters are smoke-tested and ready for bootstrapper review.
