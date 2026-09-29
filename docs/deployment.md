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
   ```

3. **Configure GitHub.** Add repository variables `AZURE_CLIENT_ID` (deploy identity), `AZURE_PR_CLIENT_ID` (PR identity), `AZURE_TENANT_ID` and `AZURE_SUBSCRIPTION_ID` from the bootstrap outputs. Create the `production` environment with a required reviewer and a branch policy allowing only `main`.

What the bootstrap grants:

- **Deploy identity** `id-ogarniamy-github`: Contributor on `rg-ogarniamy-mvp` only, trusted only for jobs in the `production` environment.
- **PR identity** `id-ogarniamy-github-pr`: Reader plus the custom `Ogarniamy What-If` role at subscription scope, trusted only for `pull_request` jobs. `what-if` runs with `--validation-level ProviderNoRbac`, so this identity never needs write access; do not add write actions to fix a `what-if` error.
- **Budget** `budget-ogarniamy-monthly`: 40 per month, alerts at 50 %, 80 % and 100 % actual and 100 % forecasted spend. Its start date cannot be changed after creation.

The federated credentials use GitHub's immutable OIDC subject (`owner@id/name@id`).

## Smoke test

`scripts/smoke.sh <swa-host> <api-host>` checks that the page is served, that `https://<swa-host>/api/health` returns `{"status":"ok"}` (retried for up to 10 minutes to cover cold starts), and that the API's own `*.azurewebsites.net` host refuses direct requests.

## Rollback

Revert the offending commit on `main` and approve the resulting deploy, or re-run an earlier successful `deploy` run. This rolls back code only; the database keeps its current schema, which is why migrations must stay backward compatible.
