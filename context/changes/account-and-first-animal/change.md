---
change_id: account-and-first-animal
title: Account and first animal
status: implementing
created: 2026-09-28
updated: 2026-09-29
archived_at: null
---

## Notes

<!-- Free-form notes for this change: links, ad-hoc context, decisions that don't belong in research/frame/plan. -->

- 2026-09-28, Phase 2 gate 2.5: the plan's grep `password=|pwd=` matches the empty template line `POSTGRES_PASSWORD=` in `.env.example`, which the plan's own contract requires. Decision: the gate is run as `git grep -nIiE "(password|pwd)=[^[:space:]]" -- . ':!*.md'` (a value is required to match); the intent (no password literal in tracked files) is unchanged.
- 2026-09-29, Phase 2 gate 2.2: a firewall loop over the App Service's runtime outbound IPs made what-if short-circuit the whole PostgreSQL module. The loop now lives in `infra/modules/postgres-firewall.bicep`, so what-if shows the server and database. The Entra administrator is still listed as "Unsupported" (its name is the not-yet-existing principal id), and what-if does not display the App Service `identity` or `appSettings`; both are present in the compiled template and are verified after deploy by 2.7/2.8.
