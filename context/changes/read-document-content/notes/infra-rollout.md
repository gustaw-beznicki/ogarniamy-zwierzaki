# Infrastructure rollout for document reading (owner runbook)

Phase 1 changes the infrastructure templates only. Nothing has been applied. Every step below that changes Azure
needs the owner's explicit approval.

## What changes

- App Service plan `F1` -> `B1`, with Always On, which the reading worker needs to run unattended.
- A VNet (`vnet-ogarniamy-mvp`, `10.20.0.0/16`): `snet-api` (`/26`, delegated to App Service VNet integration) and
  `snet-private-endpoints` (`/27`).
- The API's outbound traffic goes through the VNet (`vnetRouteAllEnabled`). Inbound stays public for the
  Static Web Apps linked backend.
- A PostgreSQL private endpoint, with the `privatelink.postgres.database.azure.com` private DNS zone linked to the VNet.
  The connection string does not change; the host name resolves to the private address inside the VNet.
- `postgresPublicNetworkAccess` (in `infra/environments/mvp.bicepparam`) stays `Enabled` until the private path is verified.
- Azure Document Intelligence `S0` in `swedencentral` (`di-ogarniamy-<unique>`), with a custom subdomain, local
  (key) auth disabled and the public endpoint behind Entra ID. The API gets `DocumentReading__*` settings with
  `WorkerEnabled=false`.
- Owner-only template `infra/bootstrap/document-reading-access.bicep`: Cognitive Services User on the account for the
  API identity and, optionally, for developer users. CI creates no role assignment.

## Cost (approximate; verify on the Azure pricing pages before approving)

| Item | Approximate cost |
| --- | --- |
| App Service B1 (Linux) | about USD 13 per month (F1 is free) |
| Document Intelligence S0, Read model | about USD 1.50 per 1,000 pages, pay per use |
| PostgreSQL private endpoint | about USD 7-8 per month plus a small per-GB data charge |
| Private DNS zone | about USD 0.50 per month plus queries |
| VNet | no charge |

Pricing: https://azure.microsoft.com/pricing/details/app-service/linux/ ,
https://azure.microsoft.com/pricing/details/ai-document-intelligence/ ,
https://azure.microsoft.com/pricing/details/private-link/ , https://azure.microsoft.com/pricing/details/dns/ .

The existing budget (`budget-ogarniamy-monthly`) is 40 per month. Static Web Apps Standard and PostgreSQL
`Standard_B1ms` already cost roughly USD 20-25 per month together. With B1 and the private endpoint added, the fixed
cost comes close to the budget or exceeds it, before any OCR pages. Review the budget before approving step 1.

## Prerequisites

- Resource providers `Microsoft.CognitiveServices` and `Microsoft.Network` are already `Registered` in the subscription.
  This was checked read-only.
- The CI deploy identity (Contributor on the resource group) can create the VNet, private endpoint, DNS zone and account.
- The PR identity runs what-if with the custom `Ogarniamy What-If` role. If validation fails on the new resource types
  (network, private DNS, Cognitive Services), adding the missing read-only or validate action is owner bootstrap
  work in `infra/bootstrap/`. Never add write actions, and do not widen CI permissions.

## Steps

### 1. Merge and approved deploy (public database access still Enabled)

The normal approved deploy (`infra/deploy.sh apply`) applies B1, VNet integration, the private endpoint, DNS and the
Document Intelligence account. Run `infra/deploy.sh what-if` first and check the diff: no role assignments, the
postgres server only gets a private endpoint connection, and `publicNetworkAccess` stays `Enabled`.

The API keeps working through the existing public path while DNS switches over. Reading stays off
(`WorkerEnabled=false`).

### 2. Grant Cognitive Services User (owner, once)

Run this from a shell signed in as the owner, in the same way as `docs/deployment.md` step 3 (storage access):

```bash
RG=rg-ogarniamy-mvp
API_PRINCIPAL_ID="$(az webapp list --resource-group "$RG" --query '[0].identity.principalId' -o tsv)"
OWNER_ID="$(az ad signed-in-user show --query id -o tsv)"   # only if you will run the local OCR probe

az deployment group what-if --resource-group "$RG" --name ogarniamy-document-reading-access \
  --template-file infra/bootstrap/document-reading-access.bicep \
  --parameters location=swedencentral apiPrincipalId="$API_PRINCIPAL_ID" developerPrincipalIds="[\"$OWNER_ID\"]"

# After reviewing the what-if output:
az deployment group create --resource-group "$RG" --name ogarniamy-document-reading-access \
  --template-file infra/bootstrap/document-reading-access.bicep \
  --parameters location=swedencentral apiPrincipalId="$API_PRINCIPAL_ID" developerPrincipalIds="[\"$OWNER_ID\"]"
```

Omit `developerPrincipalIds` to grant the API identity only. Role assignments can take a few minutes to apply.

### 3. Verify the private path from the API

- From the App Service SSH console (Kudu, `https://<app>.scm.azurewebsites.net/webssh/host`):
  `nslookup <postgres-server>.postgres.database.azure.com` should return an address in `10.20.2.0/27`.
- `curl https://<app>.azurewebsites.net/api/health` and the SWA `/api/health` should both return `{"status":"ok"}`.
- Check that sign-in and opening an original still work.
- In the API log stream, the startup line should read "Document reading is available: AzureManagedIdentity against
  di-ogarniamy-....cognitiveservices.azure.com ..." with the worker disabled.

### 4. Disable public database access (a later PR)

In a separate PR, set `postgresPublicNetworkAccess = 'Disabled'` in `infra/environments/mvp.bicepparam`. Then
`infra/deploy.sh` skips the firewall comparison and passes `applyFirewallRules=false`. Deployments are incremental,
so the old firewall rules are not deleted; they stop applying once public access is disabled. After the approved
apply, repeat step 3.

## Rollback

- Database connectivity broken after step 1: public access is still enabled, so remove the VNet integration
  (`az webapp vnet-integration remove`), or revert the PR and redeploy. With integration removed, the host name
  resolves publicly again.
- Broken after step 4: set `postgresPublicNetworkAccess = 'Enabled'` and redeploy. If the API's outbound IPs changed,
  `infra/deploy.sh` re-applies the firewall rules.
- Reading misconfigured: the API still starts. It logs a warning and reading stays off; originals are unaffected.
- Returning to F1 removes Always On and VNet integration, so private database access stops working. Do this only
  while `postgresPublicNetworkAccess` is `Enabled`.
