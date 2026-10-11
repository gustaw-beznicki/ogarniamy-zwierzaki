# Deployment

Everything in Azure is declared in Bicep under [`infra/`](../infra) and deployed by GitHub Actions. CI signs in to Azure with GitHub OIDC; the repository and GitHub hold no credentials, only non-secret identifiers.

- **Pull request** ([`ci.yml`](../.github/workflows/ci.yml)): builds and tests both components, lints Bicep, and runs `infra/deploy.sh what-if` with a read-only identity so the infrastructure diff is visible in the run log. Pull requests from forks skip the Azure steps.
- **Merge to `main`** ([`deploy.yml`](../.github/workflows/deploy.yml)): builds and tests every push, then waits for approval in the `production-approval` environment. After approval it applies the Bicep, deploys the API and the web app, and finishes with [`scripts/smoke.sh`](../scripts/smoke.sh). Only the newest commit waits for approval: a newer merge cancels an older run that is still waiting. Approved deployments run one at a time and are never cancelled mid-run.

The same infrastructure commands work locally after `az login`: `infra/deploy.sh lint`, `infra/deploy.sh what-if`.

## Environment

| Resource | Details |
| --- | --- |
| Resource group | `rg-ogarniamy-mvp` in `swedencentral` |
| API | App Service Linux, F1 by default (`appServiceSku` in [`mvp.bicepparam`](../infra/environments/mvp.bicepparam)); F1 has cold starts and a daily CPU quota |
| Web | Static Web Apps Standard; resource in `eastus2`, content served globally; PR preview environments disabled because linked backends do not support them |
| Database | PostgreSQL Flexible Server 17, Burstable B1ms, 32 GiB; Entra-only authentication with the API identity as administrator; firewall open only to the App Service outbound IPs |
| Document reading | Azure Document Intelligence `di-ogarniamy-<uniqueString>`, S0, backend region; custom subdomain, local (key) authentication disabled, Entra ID only; used for OCR by the API's background reader |
| Originals | Storage account `stogarniamy<uniqueString>` (StorageV2, Standard_LRS, backend region) with the private `originals` container; HTTPS and TLS 1.2 only, no anonymous blob access, no shared keys (Entra ID only); blob versioning and 30-day blob and container soft delete; no lifecycle rules, so originals never expire |

**Storage configuration.** Bicep sets the API's App Service settings `Storage__Auth=AzureManagedIdentity`, `Storage__BlobServiceUri` and `Storage__OriginalsContainer`; there is no storage connection string or key anywhere. The API reaches the container with its system-assigned managed identity, which needs **Storage Blob Data Contributor** on the `originals` container. The deploy identity is only Contributor and cannot grant roles, so that assignment is a separate, owner-run step (below), and the main deployment never manages it. Until the role exists, or while it propagates, capture and original requests answer 503 `storage_unavailable`; `/api/health` checks only the database and stays healthy.

**Document reading configuration.** Bicep sets `DocumentReading__Auth=AzureManagedIdentity`, `DocumentReading__Endpoint`, `__ModelId` (`prebuilt-read`), `__ApiVersion` (`2024-11-30`) and `DocumentReading__WorkerEnabled`, which [`app-service.bicep`](../infra/modules/app-service.bicep) keeps at `false` until a release turns the worker on. The API calls the account with its managed identity, which needs **Cognitive Services User** on the account; like the storage role, that assignment is owner-run (step 3 below). Missing or wrong settings never stop the API: it logs a warning, reading stays off, and originals keep working. Every deployment rewrites the App Service settings from Bicep, so a setting changed by hand lasts only until the next deployment.

**Firewall rules and deploy time.** Azure applies the PostgreSQL firewall rules one at a time, about a minute per rule, with one rule per App Service outbound IP (31 today).

- Before each `what-if` and `apply`, `infra/deploy.sh` compares the existing rules with the App Service's outbound IPs.
- If they already match, it passes `applyFirewallRules=false`, the firewall module is skipped, and the rules stay as they are.
- The rules are applied in full on a first deployment, when the IPs change (for example after a plan SKU change), or when the comparison cannot read either side.
- Rules for IPs the App Service no longer uses are not deleted automatically; remove them by hand.

## Setting up a new Azure environment

These steps are done once by a subscription owner, because CI deliberately lacks the permissions for them.

1. **Apply the bootstrap.** [`infra/bootstrap/main.bicep`](../infra/bootstrap/main.bicep) creates `rg-ogarniamy-cicd` with two GitHub OIDC identities, the application resource group `rg-ogarniamy-mvp`, the role assignments and the monthly budget. The budget alert address comes only from your shell, because the repository is public:

   ```bash
   export BUDGET_ALERT_EMAIL='<your alert address>'
   az deployment sub create \
     --location swedencentral \
     --name ogarniamy-bootstrap \
     --template-file infra/bootstrap/main.bicep \
     --parameters infra/bootstrap/bootstrap.bicepparam
   ```

2. **Register resource providers** that the resource-group-scoped deploy identity cannot register:

   ```bash
   az provider register --namespace Microsoft.DBforPostgreSQL
   az provider register --namespace Microsoft.Storage
   az provider register --namespace Microsoft.CognitiveServices
   ```

3. **Grant the API access to the originals container**, after the first deployment has created the App Service and its managed identity. [`infra/bootstrap/storage-access.bicep`](../infra/bootstrap/storage-access.bicep) provisions the same storage through the shared module and assigns Storage Blob Data Contributor to the API identity, scoped to the `originals` container only. Preview it with `az deployment group what-if` before `az deployment group create`:

   ```bash
   API_PRINCIPAL_ID="$(az webapp list --resource-group rg-ogarniamy-mvp --query '[0].identity.principalId' --output tsv)"
   az deployment group create \
     --resource-group rg-ogarniamy-mvp \
     --name ogarniamy-storage-access \
     --template-file infra/bootstrap/storage-access.bicep \
     --parameters location=swedencentral apiPrincipalId="${API_PRINCIPAL_ID}"
   ```

   `location` must match [`mvp.bicepparam`](../infra/environments/mvp.bicepparam). Role assignments can take several minutes to take effect.

   Grant the API **Cognitive Services User** on the Document Intelligence account the same way with [`infra/bootstrap/document-reading-access.bicep`](../infra/bootstrap/document-reading-access.bicep). It provisions the same account through the shared module, so it can run before or after the first deployment, and scopes the role to that account only. Pass `developerPrincipalIds='["<object id>"]'` as well only for people who run the local OCR probe with their own sign-in:

   ```bash
   az deployment group create \
     --resource-group rg-ogarniamy-mvp \
     --name ogarniamy-document-reading-access \
     --template-file infra/bootstrap/document-reading-access.bicep \
     --parameters location=swedencentral apiPrincipalId="${API_PRINCIPAL_ID}"
   ```

4. **Configure GitHub.** Add repository variables `AZURE_CLIENT_ID` (deploy identity), `AZURE_PR_CLIENT_ID` (PR identity), `AZURE_TENANT_ID` and `AZURE_SUBSCRIPTION_ID` from the bootstrap outputs. Create two environments, both with a branch policy allowing only `main`: `production-approval` with a required reviewer (the approval gate), and `production` without reviewers (the deploy job; its OIDC subject is the one the deploy identity trusts).

What the bootstrap grants:

- **Deploy identity** `id-ogarniamy-github`: Contributor on `rg-ogarniamy-mvp` only, trusted only for jobs in the `production` environment.
- **PR identity** `id-ogarniamy-github-pr`: Reader plus the custom `Ogarniamy What-If` role at subscription scope, trusted only for `pull_request` jobs. `what-if` runs with `--validation-level ProviderNoRbac`, so this identity never needs write access; do not add write actions to fix a `what-if` error.
- **Budget** `budget-ogarniamy-monthly`: 40 per month, alerts at 50 %, 80 % and 100 % actual and 100 % forecasted spend. Its start date cannot be changed after creation.

The federated credentials use GitHub's immutable OIDC subject (`owner@id/name@id`).

## Rolling out document reading

Reading is switched on in stages, so each stage can be checked before the next one and originals never depend on it:

1. **Account and access.** An approved deployment creates the Document Intelligence account and the `DocumentReading__*` settings with the worker off. Check its `what-if` first: one new Cognitive Services account and the app settings, and no SKU, network, PostgreSQL or role assignment changes. The owner grants Cognitive Services User (step 3 above).
2. **Schema and application, reading off.** The release with the reading migration deploys with `WorkerEnabled=false`. The migration queues every `Stored` document; owners see "queued". Capture and originals work as before.
3. **Verify configuration.** `/api/health` is healthy, the smoke test passes, and the API's startup log reads "Document reading is available: AzureManagedIdentity against ... Worker disabled."
4. **Activate the worker.** A separate, approved release sets `DocumentReading__WorkerEnabled` to `true` in `app-service.bicep`. The worker reads the queued documents one at a time and asks the provider to delete each result after saving the text.

Operating notes:

- **F1 sleep.** F1 has no Always On. While the app is unloaded after idle time, new reading jobs, retries and provider cleanup wait; the next request that reaches the API wakes it, and the worker then resumes due work and pending cleanup.
- **CPU quota.** F1 allows 60 CPU minutes per day, and exceeding it stops the app, originals included. Watch CPU time in the app's Quotas page or the `CpuTime` metric after activation. Getting close to the quota, or cold starts that break the search target once search exists, is the trigger for moving to B1.
- **Pausing reading.** Set `DocumentReading__WorkerEnabled=false` (by hand for an emergency, which restarts the app, and in Bicep to keep it). Queued jobs wait, and provider cleanup pauses with the worker. Failed jobs stay failed until their owner presses Retry.
- **Reading outages** (provider down, missing role, wrong endpoint) mark affected jobs failed or retry them later. They do not affect `/api/health`, capture or original downloads.

## Smoke test

`scripts/smoke.sh <swa-host> <api-host>` checks that the page is served, that `https://<swa-host>/api/health` returns `{"status":"ok"}` (retried for up to 10 minutes to cover cold starts), that `/api/me`, the capture defaults, document list, document, original, reading status and reading Retry routes answer 401 without a session, and that the API's own `*.azurewebsites.net` host refuses direct requests. It sends only anonymous requests: `GET`s and one empty `POST` to the Retry route, which is refused before any lookup. It changes no data and does not exercise storage access or reading.

## Rollback

Revert the offending commit on `main` and approve the resulting deploy, or re-run an earlier successful `deploy` run. This rolls back code only; the database keeps its current schema, which is why migrations must stay backward compatible. Keep the document tables, the storage account, the API's storage role and every original blob: an older release may be unable to show newer documents, but their bytes and records stay intact for the next corrected release. Never delete originals or incomplete uploads to make a rollback work.

The same applies to reading: keep the reading jobs, run files (they hold pending provider deletions), read text, the Document Intelligence account and its role. To stop reading without a code rollback, pause the worker. A release from before document reading ignores the reading tables. Documents it stores get no reading job, so before reading is switched on again, queue them by re-running the conflict-safe backfill from the `DocumentReading` migration as an approved database change.

## Originals recovery

The originals container is the only copy of each original. Blob versioning and 30-day soft delete for blobs and containers make an accidental deletion recoverable within that window; they are not an independent backup. The account forbids shared keys, so Azure CLI data commands need `--auth-mode login` and an operator with a Blob data role on the container.

- **A document's original returns 503 `original_unavailable`**: the record exists but its blob is missing. Restore the blob from its soft-deleted or previous version under the same key (`documents/{documentId}/{fileId}`); its content and `sha256` metadata come back unchanged and the API serves it again without other changes. Step by step: [Recover document originals](sop/recover-originals.md).
- **Capture or original requests return 503 `storage_unavailable`**: Blob Storage or the database could not be reached, or the API's storage role is missing. Records are kept, and the same request can be retried once the cause is fixed.
- **An upload stays incomplete**: the browser retries the same operation; a stored blob whose receipt was not recorded is picked up at completion. Incomplete operations remain private, are never listed and are not deleted automatically. To count them, see [Check incomplete uploads](sop/check-incomplete-uploads.md).
