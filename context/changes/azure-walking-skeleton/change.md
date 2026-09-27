---
change_id: azure-walking-skeleton
title: Azure walking skeleton
status: implementing
created: 2026-09-27
updated: 2026-09-27
archived_at: null
---

## Notes

<!-- Free-form notes for this change: links, ad-hoc context, decisions that don't belong in research/frame/plan. -->

### Deferred manual verification — Phase 2 (Progress 2.6–2.8)

Deferred on 2026-09-27 and still pending. Phase 3 depends on 2.6 and 2.7: the `ci.yml` and `deploy.yml` runs need the identities and repository variables.

**2.6 Apply the bootstrap once** (set `BUDGET_ALERT_EMAIL` only in your shell, never in a tracked file):

```bash
BUDGET_ALERT_EMAIL=<address> az deployment sub create --location swedencentral \
  --template-file infra/bootstrap/main.bicep \
  --parameters infra/bootstrap/bootstrap.bicepparam --confirm-with-what-if
```

Record the `clientId`, `prClientId` and `tenantId` outputs.

**2.7 Repository variables and the `production` environment:**

```bash
gh variable set AZURE_CLIENT_ID --body <clientId>
gh variable set AZURE_PR_CLIENT_ID --body <prClientId>
gh variable set AZURE_TENANT_ID --body <tenantId>
gh variable set AZURE_SUBSCRIPTION_ID --body "$(az account show --query id -o tsv)"
gh api -X PUT repos/gustaw-beznicki/ogarniamy-zwierzaki/environments/production \
  -F "reviewers[][type]=User" -F "reviewers[][id]=$(gh api user --jq .id)"
```

**2.8 Local what-if:** `infra/deploy.sh what-if` lists only creates (resource group, plan, site, static site, linked backend), with no deletes or modifications.

Notes:
- `infra/bootstrap/identities.bicep` is not in the plan's file list. It was added because a subscription-scope template can create the identities in `rg-ogarniamy-cicd` only through a module scoped to that resource group.
- Not yet proven against Azure: whether Reader plus the `Ogarniamy What-If` custom role is enough for the PR what-if. If it fails with `AuthorizationFailed`, see the plan's Migration Notes.

After each step is confirmed, tick its row in the plan's `## Progress` section.
