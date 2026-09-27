---
change_id: azure-walking-skeleton
title: Azure walking skeleton
status: archived
created: 2026-09-27
updated: 2026-09-27
archived_at: 2026-09-27T20:34:38Z
---

## Notes

<!-- Free-form notes for this change: links, ad-hoc context, decisions that don't belong in research/frame/plan. -->

### Phase 2 manual verification (Progress 2.6–2.8) — done 2026-09-27

- 2.6: The bootstrap was applied as deployment `ogarniamy-bootstrap`. It created `rg-ogarniamy-cicd`, both identities with their federated credentials, the `Ogarniamy What-If` role with its three role assignments, and `budget-ogarniamy-monthly`. The alert email was set only in the shell.
- 2.7: Repository variables `AZURE_CLIENT_ID`, `AZURE_PR_CLIENT_ID`, `AZURE_TENANT_ID` and `AZURE_SUBSCRIPTION_ID` are set. The `production` environment exists and requires review by `gustaw-beznicki`.
- 2.8: `infra/deploy.sh what-if` lists 5 creates and nothing else: `rg-ogarniamy-mvp`, `asp-ogarniamy-mvp`, `app-ogarniamy-api-3jhak2vhze7d2`, `swa-ogarniamy-web` and `linkedBackends/api`.

Decisions:
- `swaLocation` moved from `westeurope` to `eastus2`. The westeurope what-if failed with `RequestDisallowedByAzure` ("not accepting new customers"), and this is the fallback from the plan's Migration Notes. Phase 3 docs (README, `infrastructure.md`) must say eastus2.
- `infra/bootstrap/identities.bicep` is not in the plan's file list. It was added because a subscription-scope template can create the identities in `rg-ogarniamy-cicd` only through a module scoped to that resource group.
- Not yet proven: whether Reader plus `Ogarniamy What-If` is enough for the PR what-if. Phase 3 gate 3.3 will show it. If it fails with `AuthorizationFailed`, see the plan's Migration Notes.

### Phase 3 findings (PR #1 CI, 2026-09-27)

- **GitHub immutable OIDC subject.** The repository issues `repo:gustaw-beznicki@132839973/ogarniamy-zwierzaki@1372210399:<context>` (`use_immutable_subject: true`), not `repo:owner/name:<context>`. The legacy subject failed with `AADSTS700213`. `bootstrap.bicepparam` `githubRepo` now holds the ID-qualified prefix, and the bootstrap was re-applied; the client IDs did not change.
- **The read-only what-if needs `ProviderNoRbac`.** The default what-if (`Provider`) checks write permission on every resource, so the PR identity failed with `AuthorizationFailed` on `resourceGroups/write`, `deployments/write`, `sites/write` and more. `infra/deploy.sh what-if` now passes `--validation-level ProviderNoRbac`, which runs full validation with read permission only. `apply` keeps the default. This replaces the plan's Migration Note "add the missing action to the custom role", which would have given the PR identity write access.

### Phase 3 manual verification (Progress 3.5–3.9): done 2026-09-27

- 3.5: PR #1 was merged with a merge commit (`3906912`), so the Progress SHAs are ancestors of `main`. `deploy` run 36344948052 was approved and every step passed, including the smoke test.
- 3.6: `https://mango-bush-08ef40b0f.5.azurestaticapps.net/` shows `API: ok` on phone and desktop (confirmed by the user).
- 3.7: the direct `https://app-ogarniamy-api-3jhak2vhze7d2.azurewebsites.net/api/health` returns `401`, which is what smoke check 3 now requires (401/403).
- 3.8: `budget-ogarniamy-monthly` is 20 per month from 2026-09-01, with 4 notifications.
- 3.9: the second `deploy` (run 36348050946, from PR #2 merge `d623fb0`, which moved the actions to Node 24) re-applied the linked backend without errors, the smoke test passed, and the lock held (401). A later `infra/deploy.sh what-if` reports only noise, which a reviewer should not mistake for drift:
  - App Service `siteConfig` values: the live values match the template;
  - SWA `provider`, `branch` and `repositoryUrl`: written by the upload action;
  - SWA `stableInboundIP` and linked backend `managedServiceIdentityType`: set by the service.
