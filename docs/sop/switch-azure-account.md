# Owner runbook: move production to a new Azure account

The trial credits on the old Azure account expired, so production is recreated from the existing Bicep in a new Azure account: a new Entra tenant and a new subscription. Production data (accounts, animals, documents, originals) is **not** migrated (owner decision, 2026-10-12). Local databases are untouched.

Every step below is run by the owner. The agent never applies Azure changes. Placeholders in `<angle brackets>` are filled in from your own shell; never paste tenant ids, subscription ids, object ids or e-mail addresses back into tracked files.

## Subscription offer

- **Offer:** Free Trial (`MS-AZR-0044P`), USD 200 credit for 30 days, **spending limit on**.
- **What the spending limit means:** spend cannot exceed the credit. When the credit runs out, or when the 30 days end, Azure disables the subscription and its resources stop (storage becomes read-only). That is the same failure that triggered this switch.
- **To keep production running after the trial**, upgrade the subscription to pay-as-you-go before day 30 (portal → Subscriptions → *Upgrade*). Resources are kept and re-enabled on upgrade; nothing has to be redeployed. Record the upgrade date in `context/foundation/infrastructure.md`.
- **Budget:** `budget-ogarniamy-monthly` (40 per month) still applies. Free Trial is a supported Cost Management offer, so the budget and its alerts work. Cost Management shows estimated consumption, not credit applied.

Source: [Azure spending limit](https://learn.microsoft.com/azure/cost-management-billing/manage/spending-limit), [Reactivate a disabled subscription](https://learn.microsoft.com/azure/cost-management-billing/manage/subscription-disabled), [Supported Cost Management offers](https://learn.microsoft.com/azure/cost-management-billing/costs/understand-cost-mgt-data#supported-microsoft-azure-offers).

## 0. Before you start

Run all commands from the repository root: on branch `chore/azure-account-switch` for steps 1–4, on `main` once its PR has merged.

```bash
az version --query '"azure-cli"' --output tsv   # expected: 2.90.0 or newer
az bicep version                                # expected: Bicep CLI version 0.4x
gh auth status                                  # expected: Logged in to github.com
```

## 1. Sign in to the new tenant

```bash
az logout
az login --tenant <new-tenant-id>
az account list --query "[].{name:name, id:id, state:state, tenant:tenantId}" --output table
az account set --subscription <new-subscription-id>
az account show --query "{name:name, state:state}" --output table
az rest --method get \
  --url "https://management.azure.com/subscriptions/$(az account show --query id --output tsv)?api-version=2022-12-01" \
  --query "{offer:subscriptionPolicies.quotaId, spendingLimit:subscriptionPolicies.spendingLimit}" --output table
```

Expected: `state` is `Enabled`, `offer` is `FreeTrial_2014-09-01`, `spendingLimit` is `On`. If the quota id differs, record the real offer in `infrastructure.md` instead of "Free Trial".

## 2. Register resource providers

A new subscription has few providers registered, and the resource-group-scoped deploy identity cannot register them. Register every namespace the bootstrap and the main template use:

```bash
for ns in Microsoft.ManagedIdentity Microsoft.Consumption Microsoft.Web \
          Microsoft.DBforPostgreSQL Microsoft.Storage Microsoft.CognitiveServices; do
  az provider register --namespace "$ns"
done
az provider list \
  --query "[?contains('Microsoft.ManagedIdentity Microsoft.Consumption Microsoft.Web Microsoft.DBforPostgreSQL Microsoft.Storage Microsoft.CognitiveServices', namespace)].{ns:namespace, state:registrationState}" \
  --output table
```

Expected: every row reads `Registered` (registration can take a few minutes; re-run the list until it does).

This is done before the bootstrap, not after it as in `docs/deployment.md`, so the bootstrap's identities and budget do not depend on implicit registration.

## 3. Apply the bootstrap

`infra/bootstrap/bootstrap.bicepparam` now has `budgetStartDate = '2026-10-01'`. The start date must be the first day of the current or a future month **at creation time** and cannot be changed later. If you apply in November 2026 or later, change it to the first day of that month first (and commit the change).

```bash
export BUDGET_ALERT_EMAIL='<your alert address>'
az deployment sub what-if \
  --location swedencentral \
  --name ogarniamy-bootstrap \
  --template-file infra/bootstrap/main.bicep \
  --parameters infra/bootstrap/bootstrap.bicepparam
az deployment sub create \
  --location swedencentral \
  --name ogarniamy-bootstrap \
  --template-file infra/bootstrap/main.bicep \
  --parameters infra/bootstrap/bootstrap.bicepparam \
  --query properties.outputs --output json
```

Expected what-if: only creates (two resource groups, two identities with federated credentials, the `Ogarniamy What-If` role definition, role assignments, the budget). Expected create output: `clientId`, `prClientId` and `tenantId` values. Keep this output in your terminal for step 4.

```bash
az group list --query "[?starts_with(name, 'rg-ogarniamy')].name" --output tsv
# expected: rg-ogarniamy-cicd and rg-ogarniamy-mvp
az consumption budget list --query "[].{name:name, amount:amount, start:timePeriod.startDate}" --output table
# expected: budget-ogarniamy-monthly, 40, 2026-10-01
```

## 4. Repoint GitHub to the new subscription

Replace the four repository variables with the new values. The `production-approval` and `production` environments stay as they are.

```bash
REPO=gustaw-beznicki/ogarniamy-zwierzaki
gh variable set AZURE_CLIENT_ID       --repo "$REPO" --body '<clientId from step 3>'
gh variable set AZURE_PR_CLIENT_ID    --repo "$REPO" --body '<prClientId from step 3>'
gh variable set AZURE_TENANT_ID       --repo "$REPO" --body '<tenantId from step 3>'
gh variable set AZURE_SUBSCRIPTION_ID --repo "$REPO" --body "$(az account show --query id --output tsv)"
gh variable list --repo "$REPO"
```

Expected: the four variables show today's update time. The PR's `what-if` job now runs against the new subscription; on a first deploy it shows every resource as a create.

## 5. Merge the account-switch PR and approve the deploy

Merge the `chore/azure-account-switch` PR, then approve the `production-approval` gate of the `deploy` run on `main`.

```bash
gh run list --repo "$REPO" --workflow deploy.yml --branch main --limit 1
gh run watch --repo "$REPO" <run-id>
```

Expected: build, tests, apply, API deploy, web deploy and smoke all green. The first apply creates every resource and every PostgreSQL firewall rule (about a minute per App Service outbound IP, so roughly half an hour). Smoke retries `/api/health` for up to 10 minutes to cover the cold start.

If the apply fails with `MissingSubscriptionRegistration`, register the namespace named in the error (step 2) and re-run the job. If it fails with a quota or `RequestDisallowedByAzure` error, stop and report it: the plan does not change regions or SKUs.

## 6. Grant the API its data roles

Run after step 5, once the App Service and its managed identity exist.

```bash
API_PRINCIPAL_ID="$(az webapp list --resource-group rg-ogarniamy-mvp --query '[0].identity.principalId' --output tsv)"
echo "${API_PRINCIPAL_ID}"   # expected: a GUID, not empty

az deployment group create \
  --resource-group rg-ogarniamy-mvp \
  --name ogarniamy-storage-access \
  --template-file infra/bootstrap/storage-access.bicep \
  --parameters location=swedencentral apiPrincipalId="${API_PRINCIPAL_ID}"

az deployment group create \
  --resource-group rg-ogarniamy-mvp \
  --name ogarniamy-document-reading-access \
  --template-file infra/bootstrap/document-reading-access.bicep \
  --parameters location=swedencentral apiPrincipalId="${API_PRINCIPAL_ID}"
```

Optional, only if you run the local OCR probe with your own sign-in: add `developerPrincipalIds="[\"$(az ad signed-in-user show --query id --output tsv)\"]"` to the second command. Your object id is new in the new tenant.

Expected: both deployments `Succeeded`; `az role assignment list --assignee "${API_PRINCIPAL_ID}" --all --query "[].roleDefinitionName" --output tsv` lists `Storage Blob Data Contributor` and `Cognitive Services User`. Allow several minutes for the roles to take effect.

## 7. Verify the current product on the new environment

```bash
SWA_HOST="$(az staticwebapp list --resource-group rg-ogarniamy-mvp --query '[0].defaultHostname' --output tsv)"
API_HOST="$(az webapp list --resource-group rg-ogarniamy-mvp --query '[0].defaultHostName' --output tsv)"
scripts/smoke.sh "${SWA_HOST}" "${API_HOST}"
az webapp log tail --resource-group rg-ogarniamy-mvp --name "$(az webapp list --resource-group rg-ogarniamy-mvp --query '[0].name' --output tsv)"
```

Expected: smoke passes; the startup log contains "Document reading is available: AzureManagedIdentity against ...".

Then, in a browser on `https://<SWA_HOST>`: register a new account, sign in, create an animal, capture a photo and upload a PDF, wait until both show as read, and open both originals. The production database starts empty, so this is the first account.

## 8. Confirm Azure OpenAI quota for S-04

S-04 Phase 1 adds a Standard `text-embedding-3-large` deployment in `swedencentral`. Trial subscriptions can have no Azure OpenAI quota, so check before the S-04 PR merges:

```bash
az cognitiveservices usage list --location swedencentral \
  --query "[?contains(name.value, 'text-embedding-3-large')].{name:name.value, used:currentValue, limit:limit}" \
  --output table
az cognitiveservices model list --location swedencentral \
  --query "[?model.name=='text-embedding-3-large'].{version:model.version, skus:model.skus[].name}" \
  --output json
```

Expected: a row `OpenAI.Standard.text-embedding-3-large` with `limit` greater than 0, and the model list includes version `1` with SKU `Standard`. If the limit is 0 or the row is missing, record it and stop: S-04 cannot deploy until quota is granted (request it in the portal, or upgrade to pay-as-you-go first).

## 9. Decommission the old environment

Only after steps 5–8 pass. This permanently deletes the old production data, which the owner chose not to migrate.

```bash
az login --tenant <old-tenant-id>
az account set --subscription <old-subscription-id>
az group list --query "[?starts_with(name, 'rg-ogarniamy')].name" --output tsv
az group delete --name rg-ogarniamy-mvp --yes
az group delete --name rg-ogarniamy-cicd --yes
az group list --query "[?starts_with(name, 'rg-ogarniamy')].name" --output tsv   # expected: empty
```

If the old subscription is already disabled, deletion may be refused; it is then removed with the subscription and nothing keeps billing. The old budget is deleted with the subscription; it is not in a resource group.

Finally, sign back in to the new tenant (`az login --tenant <new-tenant-id>`) and confirm the budget alert is visible: portal → Cost Management → Budgets → `budget-ogarniamy-monthly`, or the alert e-mail once spend passes 50 %.

## Record the result

Record the switch date, the trial end date and any upgrade in `context/foundation/infrastructure.md`. Report anything that differed from the expected output above, including the real offer type and the quota found in step 8.
