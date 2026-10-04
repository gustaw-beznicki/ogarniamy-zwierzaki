# Deployment

Everything in Azure is declared in Bicep under [`infra/`](../infra) and deployed by GitHub Actions. CI signs in to Azure with GitHub OIDC; the repository and GitHub hold no credentials, only non-secret identifiers.

- **Pull request** ([`ci.yml`](../.github/workflows/ci.yml)): builds and tests both components, lints Bicep, and runs `infra/deploy.sh what-if` with a read-only identity so the infrastructure diff is visible in the run log. Pull requests from forks skip the Azure steps.
- **Merge to `main`** ([`deploy.yml`](../.github/workflows/deploy.yml)): after approval in the `production` environment, runs the tests, applies the Bicep, deploys the API and the web app, and finishes with [`scripts/smoke.sh`](../scripts/smoke.sh). Deployments run one at a time and are never cancelled mid-run.

The same infrastructure commands work locally after `az login`: `infra/deploy.sh lint`, `infra/deploy.sh what-if`.

## Environment

| Resource | Details |
| --- | --- |
| Resource group | `rg-ogarniamy-mvp` in `swedencentral` |
| API | App Service Linux, F1 by default (`appServiceSku` in [`mvp.bicepparam`](../infra/environments/mvp.bicepparam)); F1 has cold starts and a daily CPU quota |
| Web | Static Web Apps Standard; resource in `eastus2`, content served globally; PR preview environments disabled because linked backends do not support them |
| Database | PostgreSQL Flexible Server 17, Burstable B1ms, 32 GiB; Entra-only authentication with the API identity as administrator; firewall open only to the App Service outbound IPs |
| Originals | Storage account `stogarniamy<uniqueString>` (StorageV2, Standard_LRS, backend region) with the private `originals` container; HTTPS and TLS 1.2 only, no anonymous blob access, no shared keys (Entra ID only); blob versioning and 30-day blob and container soft delete; no lifecycle rules, so originals never expire |

**Storage configuration.** Bicep sets the API's App Service settings `Storage__Auth=AzureManagedIdentity`, `Storage__BlobServiceUri` and `Storage__OriginalsContainer`; there is no storage connection string or key anywhere. The API reaches the container with its system-assigned managed identity, which needs **Storage Blob Data Contributor** on the `originals` container. The deploy identity is only Contributor and cannot grant roles, so that assignment is a separate, owner-run step (below), and the main deployment never manages it. Until the role exists, or while it propagates, capture and original requests answer 503 `storage_unavailable`; `/api/health` checks only the database and stays healthy.

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

4. **Configure GitHub.** Add repository variables `AZURE_CLIENT_ID` (deploy identity), `AZURE_PR_CLIENT_ID` (PR identity), `AZURE_TENANT_ID` and `AZURE_SUBSCRIPTION_ID` from the bootstrap outputs. Create the `production` environment with a required reviewer and a branch policy allowing only `main`.

What the bootstrap grants:

- **Deploy identity** `id-ogarniamy-github`: Contributor on `rg-ogarniamy-mvp` only, trusted only for jobs in the `production` environment.
- **PR identity** `id-ogarniamy-github-pr`: Reader plus the custom `Ogarniamy What-If` role at subscription scope, trusted only for `pull_request` jobs. `what-if` runs with `--validation-level ProviderNoRbac`, so this identity never needs write access; do not add write actions to fix a `what-if` error.
- **Budget** `budget-ogarniamy-monthly`: 40 per month, alerts at 50 %, 80 % and 100 % actual and 100 % forecasted spend. Its start date cannot be changed after creation.

The federated credentials use GitHub's immutable OIDC subject (`owner@id/name@id`).

## Smoke test

`scripts/smoke.sh <swa-host> <api-host>` checks that the page is served, that `https://<swa-host>/api/health` returns `{"status":"ok"}` (retried for up to 10 minutes to cover cold starts), that `/api/me`, the capture defaults, document list, document and original routes answer 401 without a session, and that the API's own `*.azurewebsites.net` host refuses direct requests. It only sends anonymous `GET` requests, so it changes no data and does not exercise storage access.

## Rollback

Revert the offending commit on `main` and approve the resulting deploy, or re-run an earlier successful `deploy` run. This rolls back code only; the database keeps its current schema, which is why migrations must stay backward compatible. Keep the document tables, the storage account, the API's storage role and every original blob: an older release may be unable to show newer documents, but their bytes and records stay intact for the next corrected release. Never delete originals or incomplete uploads to make a rollback work.

## Originals recovery

The originals container is the only copy of each original. Blob versioning and 30-day soft delete for blobs and containers make an accidental deletion recoverable within that window; they are not an independent backup. The account forbids shared keys, so Azure CLI data commands need `--auth-mode login` and an operator with a Blob data role on the container.

- **A document's original returns 503 `original_unavailable`**: the record exists but its blob is missing. Restore the blob from its soft-deleted or previous version under the same key (`documents/{documentId}/{fileId}`); its content and `sha256` metadata come back unchanged and the API serves it again without other changes. Step by step: [Recover document originals](sop/recover-originals.md).
- **Capture or original requests return 503 `storage_unavailable`**: Blob Storage or the database could not be reached, or the API's storage role is missing. Records are kept, and the same request can be retried once the cause is fixed.
- **An upload stays incomplete**: the browser retries the same operation; a stored blob whose receipt was not recorded is picked up at completion. Incomplete operations remain private, are never listed and are not deleted automatically. To count them, see [Check incomplete uploads](sop/check-incomplete-uploads.md).
