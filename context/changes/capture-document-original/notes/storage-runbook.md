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
| `Original of file <fileId> of Stored document <id> is missing from storage.` | Error | 503 `original_unavailable`; see the recover-originals SOP, scenario 2. |

Stream the API logs: `az webapp log tail --resource-group "${RG}" --name "${API_APP}"` (read-only). If nothing is streamed because container logging is off, enabling it (`az webapp log config --docker-container-logging filesystem`) is a configuration change and needs approval.

To count incomplete operations from logs alone, compare the operation IDs of `created` messages with those of `is stored` messages over the same period.

### Database (read-only queries)

See the SOP [Check incomplete uploads](../../../../docs/sop/check-incomplete-uploads.md): access, counting `uploading` operations and looking up one document's blob keys and hashes.

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

See the SOP [Recover document originals](../../../../docs/sop/recover-originals.md): unfinished uploads, `original_unavailable`, `storage_unavailable` and code rollback.
