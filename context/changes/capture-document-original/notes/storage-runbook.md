# Originals storage runbook (owner)

Rollout, verification and recovery steps for `capture-document-original`. Status on 2026-10-04: **nothing in this runbook has been run against Azure.** All verification so far is local (integration tests against Azurite and PostgreSQL containers). Local Azurite results are not evidence for production; fill in the evidence table only from the deployed environment.

Every command is marked **read-only** or **mutation**. Run mutations only with the approval the existing deployment process requires. Never point `scripts/smoke.sh` at production outside an approved deployment, and never delete an original or an incomplete upload to fix a problem.

Shared shell variables:

```bash
RG=rg-ogarniamy-mvp
API_APP="$(az webapp list --resource-group "${RG}" --query '[0].name' --output tsv)"                    # read-only
API_PRINCIPAL_ID="$(az webapp show --resource-group "${RG}" --name "${API_APP}" --query identity.principalId --output tsv)"  # read-only
SA="$(az storage account list --resource-group "${RG}" --query '[0].name' --output tsv)"                  # read-only
```

## 1. One-time rollout

Order matters: storage and access first, then the code that uses them.

1. **Review the infrastructure diff** (read-only): `infra/deploy.sh what-if`. Expect the storage account, blob service settings, the `originals` container and the three `Storage__*` App Service settings, and no role assignment in the main deployment.
2. **Register `Microsoft.Storage`** (mutation, subscription owner): see [storage-access.md](storage-access.md), step 1.
3. **Deploy** through the gated `deploy` workflow (mutation, approved in the `production` environment). It applies the Bicep (storage and App Service settings), runs migrations at API startup (additive tables only) and runs the smoke test.
4. **Grant the API container-scoped access** (mutation, subscription owner): [storage-access.md](storage-access.md), step 2. Preview with `what-if` first.
5. **Verify storage settings and the role assignment** (read-only): [storage-access.md](storage-access.md), step 3. Allow a few minutes for role propagation; until then capture and original requests answer 503 `storage_unavailable` while `/api/health` stays `ok`.
6. **Smoke test** (read-only, anonymous `GET`s): the deploy workflow runs `scripts/smoke.sh`; check 2c must show 401 for the capture defaults, document list, document and original routes.
7. **Manual acceptance on the deployed SWA origin** with two synthetic accounts A and B and synthetic files only (never real veterinary records):
   - A captures one PDF and a reordered multi-photo document; every original opens and matches the selected file.
   - Pasting A's original URL in a private window (no session) gets 401; as B it gets 404.
   - Restart the API (mutation: `az webapp restart --resource-group "${RG}" --name "${API_APP}"`), sign in again as A and reopen every original.
   - A real phone captures with the camera, and an interrupted upload is retried in the same form without creating a duplicate.

## 2. Evidence (fill in from the deployed environment)

| Step | Date | Result / output summary | By |
| --- | --- | --- | --- |
| What-if reviewed | | | |
| `Microsoft.Storage` registered | | | |
| Deployment run (workflow run link) | | | |
| `storage-access.bicep` applied | | | |
| Storage settings verified (public access, shared key, TLS, versioning, soft delete) | | | |
| Role assignment scope ends in `/containers/originals` | | | |
| Smoke test incl. check 2c passed | | | |
| Two-account isolation and anonymous pasted-link denial | | | |
| Originals readable after API restart | | | |
| Phone camera capture and interrupted-upload retry | | | |

No entries have been recorded yet.

## 3. Diagnostics

### Logs (read-only)

The API logs operation, document and file IDs, file positions, states and failure codes. It never logs file names, file contents, connection strings or tokens. Relevant messages:

| Message starts with | Level | Meaning |
| --- | --- | --- |
| `Upload operation <id> created with <n> file(s).` | Information | Manifest stored as `Uploading`. |
| `File <fileId> at position <n> of operation <id> stored.` | Information | Original written and its receipt recorded. |
| `Upload of file <fileId> of operation <id> rejected: <Failure>.` | Information | Bytes did not match the manifest, for example `FileMismatch`, `EmptyFile` or `UnsupportedFileType`. |
| `Receipt of file <fileId> of operation <id> recovered from storage.` | Information | A blob written before a database failure was reconciled at completion. |
| `Completion of operation <id> refused: <n> original(s) missing.` | Information | The client completed before every file was stored. |
| `Upload operation <id> is stored.` | Information | Document visible to its owner. |
| `Storage unavailable for operation/document <id>, file <fileId>: StorageUnavailable.` | Warning | Transient Blob Storage or database failure (503 `storage_unavailable`); the exception is attached. |
| `File <fileId> of operation <id> has a different original in storage: UploadConflict.` | Warning | A blob under the slot's key does not match the manifest; it was not overwritten. |
| `Original of file <fileId> of Stored document <id> is missing from storage.` | Error | 503 `original_unavailable`; see recovery 4.2. |

Stream the API logs: `az webapp log tail --resource-group "${RG}" --name "${API_APP}"` (read-only). If nothing is streamed because container logging is off, enabling it (`az webapp log config --docker-container-logging filesystem`) is a configuration change and needs approval.

To count incomplete operations from logs alone, compare the operation IDs of `created` messages with those of `is stored` messages over the same period.

### Database (read-only queries)

The production database accepts only the API's managed identity, and its firewall allows only the App Service outbound IPs. Running these queries in Azure therefore needs a temporary, separately approved access path (for example a temporary Entra administrator and firewall rule for the operator, removed afterwards); do not widen access permanently. Locally they run as is: `docker compose exec postgres psql -U postgres -d ogarniamy`.

```sql
-- Incomplete (Uploading) operations: count and oldest.
SELECT count(*) AS uploading, min(created_at) AS oldest
FROM documents
WHERE storage_state = 'uploading';

-- Each incomplete operation with how many of its originals have a recorded receipt.
SELECT d.id, d.created_at, count(f.id) AS files, count(f.receipt_etag) AS files_with_receipt
FROM documents d
JOIN document_files f ON f.document_id = d.id
WHERE d.storage_state = 'uploading'
GROUP BY d.id, d.created_at
ORDER BY d.created_at;

-- Blob key and expected hash of every original of one Stored document, to compare with storage.
SELECT f.position, f.id AS file_id, f.blob_key, f.byte_length, f.sha256
FROM document_files f
JOIN documents d ON d.id = f.document_id
WHERE d.id = '<document id>' AND d.storage_state = 'stored'
ORDER BY f.position;
```

### Storage (read-only)

The account rejects shared keys, so every data command uses `--auth-mode login`, and the operator needs a Blob data role on the container (granting one is a mutation that needs approval; the API's own role is not usable by people).

```bash
# Current, previous and soft-deleted versions of one document's originals.
az storage blob list --auth-mode login --account-name "${SA}" --container-name originals \
  --prefix "documents/<document id>/" --include dvm \
  --query "[].{name:name, version:versionId, current:isCurrentVersion, deleted:deleted, length:properties.contentLength, sha256:metadata.sha256}" \
  --output table

# Soft-deleted containers.
az storage container list --auth-mode login --account-name "${SA}" --include-deleted \
  --query "[].{name:name, deleted:deleted, version:version}" --output table
```

## 4. Recovery

### 4.1 Missing receipt (an `Uploading` operation whose original is in storage)

Caused by a database failure, timeout or lost response after the blob write. No manual action: retrying the same file upload, or completing the operation, finds the blob under its deterministic key, checks its length and SHA-256 against the manifest and records the receipt. The operation must be retried by its owner from the still-open form; there is no administrative completion. An abandoned operation stays private and invisible in lists; leave it and its blobs in place (there is no cleanup policy yet).

### 4.2 Stored original unavailable (503 `original_unavailable`)

The document record exists but its blob is missing. The record is never removed automatically.

1. Read the blob key and expected `sha256` with the third SQL query, then list the key's versions (read-only, section 3).
2. Blob versioning is on, so a deleted blob leaves its last content as a previous version. Restore it by copying that version back to the same key (mutation):

   ```bash
   az storage blob copy start --auth-mode login --account-name "${SA}" \
     --destination-container originals --destination-blob "documents/<document id>/<file id>" \
     --source-uri "https://${SA}.blob.core.windows.net/originals/documents/<document id>/<file id>?versionid=<version id>"
   ```

   If the version itself is soft-deleted, first undelete it (mutation): `az storage blob undelete --auth-mode login --account-name "${SA}" --container-name originals --name "documents/<document id>/<file id>"`, then copy as above.
3. If the whole container was deleted, restore it within the retention window (mutation): `az storage container restore --auth-mode login --account-name "${SA}" --name originals --deleted-version <version>`.
4. Verify: the restored blob's `sha256` metadata and length equal the database values, and the owner can open the original again. The API reads the blob under its key; no database change is needed.
5. After 30 days a deleted blob cannot be recovered. Keep the record, note the document and file IDs, and tell the owner the original is lost; do not substitute another file.

### 4.3 Storage unavailable (503 `storage_unavailable`)

Transient Blob Storage or database failures, or missing storage authorization. Nothing is deleted and every request can be retried.

1. Read-only checks: the Warning log entries and their exceptions (403 `AuthorizationPermissionMismatch` means the API's role is missing or still propagating), the role assignment and storage settings ([storage-access.md](storage-access.md), step 3), and Azure service health for the region.
2. If the role is missing, apply `storage-access.bicep` again (mutation, owner). Do not enable shared keys, public access or SAS to work around it.
3. `/api/health` covers only the database and must stay that way: original retrieval does not depend on OCR or search, and a storage problem must not make the whole API unhealthy.

### 4.4 Code rollback

Revert the commit on `main` and approve the deploy, or re-run an earlier successful `deploy` run (mutation, approved). This restores code only:

- Keep the `documents` and `document_files` tables, the storage account and container, the API's role assignment and every blob. Migrations are not reverted; the schema change is additive, so the previous release keeps working with it.
- A release from before this change has no screens for documents; the data waits for the next corrected release.
- Do not delete originals or incomplete uploads during or after a rollback. Soft delete and versioning are a 30-day safety net, not a backup.
