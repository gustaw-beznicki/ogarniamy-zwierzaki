# Infrastructure rollout for document reading (owner runbook)

Phase 1 changes the infrastructure templates only. Nothing has been applied. Every step below that changes Azure
needs the owner's explicit approval.

## What changes

The API stays on App Service `F1` and the database keeps its public firewall (owner decision 2026-10-06, see the
plan's "Plan amendment 2026-10-06"). B1/Always On follows a trigger, and the private database network is the
M-2 change `database-private-network`.

- Azure Document Intelligence `S0` in `swedencentral` (`di-ogarniamy-<unique>`), with a custom subdomain, local
  (key) auth disabled and the public endpoint behind Entra ID. The API gets `DocumentReading__*` settings with
  `WorkerEnabled=false`.
- Owner-only template `infra/bootstrap/document-reading-access.bicep`: Cognitive Services User on the account for the
  API identity and, optionally, for developer users. CI creates no role assignment.

## Cost (approximate; verify on the Azure pricing pages before approving)

| Item | Approximate cost |
| --- | --- |
| Document Intelligence S0, Read model | about USD 1.50 per 1,000 pages, pay per use |
| App Service | unchanged (F1, free) |

Pricing: https://azure.microsoft.com/pricing/details/ai-document-intelligence/ .

## F1 limitations to keep in mind

- F1 sleeps after idle time. New reading jobs, retries, provider cleanup and backfill wait until the next request wakes
  the app; the worker then resumes due and lease-expired jobs and pending cleanup.
- F1 has a daily CPU quota (60 CPU minutes). Exceeding it stops the app, so originals become unavailable. Local PDF
  parsing has a hard time limit for this reason. Watch CPU time in the App Service quotas view after enabling the worker;
  approaching the quota is a trigger for B1.

## Prerequisites

- Resource provider `Microsoft.CognitiveServices` is already `Registered` in the subscription (checked read-only).
- The CI deploy identity (Contributor on the resource group) can create the account.
- The PR identity runs what-if with the custom `Ogarniamy What-If` role. If validation fails on Cognitive Services,
  adding the missing read-only or validate action is owner bootstrap work in `infra/bootstrap/`. Never add write
  actions, and do not widen CI permissions.

## Steps

### 1. Merge and approved deploy

The normal approved deploy (`infra/deploy.sh apply`) creates the Document Intelligence account and adds the
`DocumentReading__*` app settings. Run `infra/deploy.sh what-if` first and check the diff: one new
Cognitive Services account, no SKU, network, PostgreSQL or role assignment changes. Reading stays off
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

### 3. Verify configuration

- `/api/health` on the SWA host returns `{"status":"ok"}`; sign-in and opening an original still work.
- In the API log stream, the startup line should read "Document reading is available: AzureManagedIdentity against
  di-ogarniamy-....cognitiveservices.azure.com ..." with the worker disabled.

## Rollback

- Reading misconfigured: the API still starts. It logs a warning and reading stays off; originals are unaffected.
- To stop reading, set `DocumentReading__WorkerEnabled=false` (or revert and redeploy). Jobs, text and cleanup records
  stay in the database.
