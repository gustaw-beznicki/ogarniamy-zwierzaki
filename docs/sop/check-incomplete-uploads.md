# SOP: Check incomplete uploads

**Purpose**: find document uploads that were started but never finished, and look up the originals of one stored document.
**When**: occasionally, out of curiosity or capacity planning, or while investigating a missing original (`original_unavailable`). Not part of deployment or routine operation.
**Safety**: every step is read-only. Never delete an incomplete upload or an original to "clean up"; there is no cleanup policy yet.

## Background

A capture first stores its manifest as an `uploading` document, then uploads each file, then completes. If the owner closes the tab or loses the connection before completion, the document stays `uploading`: private, never listed, harmless. Nothing removes it automatically.

## Option A: from logs (no database access needed)

1. Open the API logs (Azure Portal → App Service → Log stream, or `az webapp log tail --resource-group rg-ogarniamy-mvp --name <api app>`). If nothing streams, container logging is off; turning it on is a configuration change that needs approval.
2. Collect operation IDs from `Upload operation <id> created with <n> file(s).` messages.
3. Collect operation IDs from `Upload operation <id> is stored.` messages over the same period.
4. IDs in step 2 but not in step 3 are incomplete uploads.

## Option B: from the database

### 1. Get access

- **Locally**: `docker compose exec postgres psql -U postgres -d ogarniamy`.
- **Azure**: only the API's managed identity can sign in, and the firewall allows only App Service IPs. Running queries needs a temporary, separately approved access path (for example a temporary Entra administrator and firewall rule for you). Remove it afterwards; never widen access permanently.

### 2. Count incomplete uploads

```sql
SELECT count(*) AS uploading, min(created_at) AS oldest
FROM documents
WHERE storage_state = 'uploading';
```

### 3. See how far each one got

```sql
SELECT d.id, d.created_at, count(f.id) AS files, count(f.receipt_etag) AS files_with_receipt
FROM documents d
JOIN document_files f ON f.document_id = d.id
WHERE d.storage_state = 'uploading'
GROUP BY d.id, d.created_at
ORDER BY d.created_at;
```

`files_with_receipt` lower than `files` means some originals never reached storage.

### 4. Look up the originals of one stored document

Use this when a stored document's original cannot be opened, to get the blob keys and expected hashes for comparison with Blob Storage.

```sql
SELECT f.position, f.id AS file_id, f.blob_key, f.byte_length, f.sha256
FROM document_files f
JOIN documents d ON d.id = f.document_id
WHERE d.id = '<document id>' AND d.storage_state = 'stored'
ORDER BY f.position;
```

## What to do with the result

- **Incomplete uploads found**: nothing. Only the owner can finish one, by retrying in the still-open form. Leave them and their blobs in place.
- **A stored original is missing from storage**: follow [Recover document originals](recover-originals.md).
