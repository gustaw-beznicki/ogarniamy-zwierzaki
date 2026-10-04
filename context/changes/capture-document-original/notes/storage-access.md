# Storage access prerequisites (owner, one time)

Phase 1 adds a private Blob Storage account (`stogarniamy<uniqueString>`) with the `originals` container to `infra/main.bicep`. Two steps need permissions that CI deliberately lacks. Run them as a subscription owner, after reviewing the storage what-if and before the first production release that reads or writes originals. Nothing here has been applied yet.

## 1. Register the Microsoft.Storage resource provider

The deploy identity is Contributor on `rg-ogarniamy-mvp` only and cannot register providers. Without registration, `infra/deploy.sh apply` fails on the storage account.

```bash
az provider show --namespace Microsoft.Storage --query registrationState --output tsv
az provider register --namespace Microsoft.Storage --wait     # only if not "Registered"
```

## 2. Grant the API identity access to the originals container

`infra/bootstrap/storage-access.bicep` provisions the same storage through the shared module and assigns **Storage Blob Data Contributor** to the API's system-assigned managed identity, scoped to the `originals` container only. The main deployment creates no role assignment, and the CI identities are not changed.

Validate first (no changes), then apply after review:

```bash
API_PRINCIPAL_ID="$(az webapp list --resource-group rg-ogarniamy-mvp --query '[0].identity.principalId' --output tsv)"

az deployment group what-if \
  --resource-group rg-ogarniamy-mvp \
  --name ogarniamy-storage-access \
  --template-file infra/bootstrap/storage-access.bicep \
  --parameters location=swedencentral apiPrincipalId="${API_PRINCIPAL_ID}"

az deployment group create \
  --resource-group rg-ogarniamy-mvp \
  --name ogarniamy-storage-access \
  --template-file infra/bootstrap/storage-access.bicep \
  --parameters location=swedencentral apiPrincipalId="${API_PRINCIPAL_ID}"
```

`location` must match `infra/environments/mvp.bicepparam`. The template can run before or after the regular deployment; both manage the same account with the same settings.

## 3. Verify

```bash
STORAGE_ACCOUNT="$(az storage account list --resource-group rg-ogarniamy-mvp --query '[0].name' --output tsv)"

az storage account show --resource-group rg-ogarniamy-mvp --name "${STORAGE_ACCOUNT}" \
  --query '{publicBlob:allowBlobPublicAccess, sharedKey:allowSharedKeyAccess, tls:minimumTlsVersion, httpsOnly:enableHttpsTrafficOnly}'

az storage account blob-service-properties show --resource-group rg-ogarniamy-mvp --account-name "${STORAGE_ACCOUNT}" \
  --query '{versioning:isVersioningEnabled, blobSoftDelete:deleteRetentionPolicy, containerSoftDelete:containerDeleteRetentionPolicy}'

az role assignment list --all --assignee "${API_PRINCIPAL_ID}" \
  --query "[?roleDefinitionName=='Storage Blob Data Contributor'].scope" --output tsv
```

Expected: public blob access and shared key access `false`, `TLS1_2`, HTTPS only, versioning on, 30-day blob and container soft delete, and a single role assignment whose scope ends in `/blobServices/default/containers/originals`. Role assignments can take several minutes to propagate; the API returns storage errors until then.
