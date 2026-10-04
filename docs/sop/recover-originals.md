# SOP: Recover document originals

**Purpose**: get originals readable again after a storage or database problem, or after a code rollback.
**When**: users see "original unavailable" or "storage unavailable" errors, an upload never finishes, or a release is rolled back.
**Safety**: steps are marked **read-only** or **mutation**. Run mutations only with the approval the deployment process requires. Never delete an original, a document record or an incomplete upload to fix a problem, and never enable shared keys, public access or SAS links as a workaround.

Blob versioning and 30-day soft delete (blobs and container) are the safety net. They are not a backup: after 30 days a deleted original is gone.

## Setup (read-only)

The storage account rejects shared keys, so data commands use `--auth-mode login`. You need a Blob data role on the `originals` container yourself; granting it is a mutation that needs approval. The API's own role cannot be used by people.

```bash
RG=rg-ogarniamy-mvp
API_APP="$(az webapp list --resource-group "${RG}" --query '[0].name' --output tsv)"
API_PRINCIPAL_ID="$(az webapp show --resource-group "${RG}" --name "${API_APP}" --query identity.principalId --output tsv)"
SA="$(az storage account list --resource-group "${RG}" --query '[0].name' --output tsv)"
```

Each original is stored under `documents/<document id>/<file id>`.

## Scenario 1: an upload never finishes

**Symptom**: a document stays `uploading` (see [Check incomplete uploads](check-incomplete-uploads.md)).

**Action**: none. If the file reached storage but the database missed it, the next retry or completion from the owner's still-open form finds the blob, checks its length and SHA-256 and records it. There is no administrative completion. Leave abandoned uploads and their blobs in place.

## Scenario 2: "original unavailable" (503 `original_unavailable`)

**Symptom**: the document is listed, but one of its files cannot be opened. The API log shows `Original of file <fileId> of Stored document <id> is missing from storage or does not match its receipt.` The API opens an original only when its length and `sha256` metadata match the database; the ETag is not compared, so a version restored below opens again.

1. **Find the expected file** (read-only): step 4 of [Check incomplete uploads](check-incomplete-uploads.md) gives the blob key, length and SHA-256.
2. **List its versions** (read-only):

   ```bash
   az storage blob list --auth-mode login --account-name "${SA}" --container-name originals \
     --prefix "documents/<document id>/" --include dvm \
     --query "[].{name:name, version:versionId, current:isCurrentVersion, deleted:deleted, length:properties.contentLength, sha256:metadata.sha256}" \
     --output table
   ```

3. **If the version is soft-deleted, undelete it** (mutation):

   ```bash
   az storage blob undelete --auth-mode login --account-name "${SA}" --container-name originals \
     --name "documents/<document id>/<file id>"
   ```

4. **Copy the previous version back to the same key** (mutation):

   ```bash
   az storage blob copy start --auth-mode login --account-name "${SA}" \
     --destination-container originals --destination-blob "documents/<document id>/<file id>" \
     --source-uri "https://${SA}.blob.core.windows.net/originals/documents/<document id>/<file id>?versionid=<version id>"
   ```

5. **If the whole container was deleted**, restore it instead (mutation):

   ```bash
   az storage container list --auth-mode login --account-name "${SA}" --include-deleted \
     --query "[].{name:name, deleted:deleted, version:version}" --output table   # read-only
   az storage container restore --auth-mode login --account-name "${SA}" --name originals --deleted-version <version>
   ```

6. **Verify** (read-only): the restored blob's length and `sha256` metadata match step 1 (a blob without `sha256` metadata was not written by the API and is never served; it also makes completing its upload fail with a 500 until the blob is replaced by the matching version), and the owner can open the file again. No database change is needed.
7. **If nothing can be restored** (older than 30 days): keep the record, note the document and file IDs, and tell the owner the original is lost. Never substitute another file.

## Scenario 3: "storage unavailable" (503 `storage_unavailable`)

**Symptom**: uploads or original downloads fail with a retryable error; `/api/health` may still be `ok`. Nothing is deleted, and every request can be retried once the cause is fixed.

1. **Check the API log** (read-only) for `Storage unavailable for operation/document …` warnings. A 403 `AuthorizationPermissionMismatch` means the API's storage role is missing or still propagating.
2. **Check the role and settings** (read-only):

   ```bash
   az role assignment list --all --assignee "${API_PRINCIPAL_ID}" \
     --query "[?roleDefinitionName=='Storage Blob Data Contributor'].scope" --output tsv
   az storage account show --resource-group "${RG}" --name "${SA}" \
     --query '{publicBlob:allowBlobPublicAccess, sharedKey:allowSharedKeyAccess, tls:minimumTlsVersion}'
   ```

   Expected: one scope ending in `/containers/originals`; public access and shared keys `false`; `TLS1_2`.
3. **Check Azure service health** for the region (read-only, Azure Portal → Service Health).
4. **If the role is missing, grant it again** (mutation, subscription owner; preview with `az deployment group what-if` first):

   ```bash
   az deployment group create --resource-group "${RG}" --name ogarniamy-storage-access \
     --template-file infra/bootstrap/storage-access.bicep \
     --parameters location=swedencentral apiPrincipalId="${API_PRINCIPAL_ID}"
   ```

   `location` must match `infra/environments/mvp.bicepparam`. Role assignments can take several minutes to take effect.

Do not make `/api/health` depend on storage: a storage problem must not mark the whole API unhealthy, and original retrieval never depends on OCR or search.

## Scenario 4: code rollback

**Action** (mutation, approved): revert the commit on `main` and approve the deploy, or re-run an earlier successful `deploy` run.

This restores code only. Keep the `documents` and `document_files` tables, the storage account and container, the API's role assignment and every blob. Migrations are not reverted; the schema change is additive, so the previous release keeps working. A release from before document capture has no document screens; the data waits for the next corrected release.
