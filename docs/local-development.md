# Local development

## Prerequisites

- [Node.js](https://nodejs.org/) 22.12 or newer, with npm
- [.NET SDK](https://dotnet.microsoft.com/download) 10
- [Docker](https://docs.docker.com/get-docker/) with Compose (local database and tests)
- EF Core CLI, only for creating migrations: `dotnet tool install --global dotnet-ef`

## 1. Start PostgreSQL and Azurite

```bash
cp .env.example .env          # then set POSTGRES_PASSWORD to any local password
docker compose up -d
```

This starts PostgreSQL 17 with the [pgvector](https://github.com/pgvector/pgvector) extension (the `pgvector/pgvector:0.8.2-pg17` image; the first migration that needs it creates the extension) and [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite), the local Blob Storage emulator that holds document originals.

If port 5432 is already taken on your machine, set `POSTGRES_PORT` in `.env` (for example `5433`) and use that port in the connection string below. The same applies to Azurite's `AZURITE_BLOB_PORT` (default `10000`).

## 2. Point the API at it

The connection string is kept in [.NET user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets), outside the repository. Use the password from `.env`:

```bash
dotnet user-secrets set ConnectionStrings:Default \
  "Host=localhost;Port=5432;Database=ogarniamy;Username=postgres;Password=<password from .env>" \
  --project services/api
```

Point the original-file storage at Azurite the same way. `UseDevelopmentStorage=true` is the emulator shorthand for the default port 10000:

```bash
dotnet user-secrets set ConnectionStrings:Storage "UseDevelopmentStorage=true" --project services/api
```

If you changed `AZURITE_BLOB_PORT`, set the full Azurite connection string instead, with `BlobEndpoint=http://127.0.0.1:<port>/devstoreaccount1` and the well-known emulator account from the [Azurite documentation](https://learn.microsoft.com/azure/storage/common/storage-use-azurite#http-connection-strings).

## 3. Run the API and the web app

```bash
dotnet run --project services/api       # http://localhost:5180, applies migrations and creates the Azurite container on start
```

```bash
cd apps/web
npm ci
npm run dev                             # http://localhost:4321
```

The Astro dev server proxies `/api/*` to `http://localhost:5180`, just like Static Web Apps does in Azure. Set `API_PROXY_TARGET` to proxy elsewhere. Check the stack with `curl http://localhost:5180/api/health`, which should return `{"status":"ok"}`.

## Local data

PostgreSQL and Azurite keep their data in the `postgres-data` and `azurite-data` Docker volumes. Treat them as a pair: a database whose blobs are gone still lists its documents, but their originals answer 503 `original_unavailable`. `docker compose down -v` deletes both. The integration tests use their own containers and never touch these volumes.

To look at stored originals, list the `originals` container with [Azure Storage Explorer](https://learn.microsoft.com/azure/storage/storage-explorer/vs-azure-tool-storage-manage-with-storage-explorer) or the Azure CLI connected to Azurite. Blobs are named `documents/<document id>/<file id>`; original file names exist only in the database.

## Configuration

| Key | Values | Purpose |
| --- | --- | --- |
| `ConnectionStrings:Default` | Npgsql connection string | Required. Includes a password only in `Password` mode. |
| `Database:Auth` | `Password` (default), `AzureManagedIdentity` | How the API authenticates to PostgreSQL. Azure sets `AzureManagedIdentity` through App Service settings. |
| `Database:EntraTokenScope`, `Database:TokenRefreshInterval`, `Database:TokenRetryInterval` | scope URI, `hh:mm:ss` | `AzureManagedIdentity` only: which Entra token to request, how often to refresh it, and how soon to retry a failure. Defaults are in `appsettings.json`. |
| `Storage:Auth` | `ConnectionString` (default), `AzureManagedIdentity` | How the API authenticates to Blob Storage. `ConnectionString` is for Azurite and is refused outside the `Development` environment; Azure sets `AzureManagedIdentity` through App Service settings. |
| `ConnectionStrings:Storage` | Azure Storage connection string | `ConnectionString` mode only. At startup the API creates the originals container in the emulator if it is missing. |
| `Storage:BlobServiceUri` | `https://<account>.blob.core.windows.net/` | `AzureManagedIdentity` only. The container is provisioned by Bicep and must already exist. |
| `Storage:OriginalsContainer` | container name, default `originals` | Private container holding the original files. |
| `DocumentReading:Auth` | `Disabled` (default), `AzureManagedIdentity`, `DeveloperCredential` | How the API authenticates to Azure Document Intelligence. `Disabled` turns reading off; `DeveloperCredential` uses your `az login` identity and is refused outside the `Development` environment. Azure sets `AzureManagedIdentity` through App Service settings. Invalid reading settings log a warning at startup and turn reading off; the API still starts and `/api/health` still reports only the database. |
| `DocumentReading:Endpoint` | `https://<account>.cognitiveservices.azure.com/` | Required unless reading is `Disabled`. Not a secret: the resource accepts only Entra ID tokens, never keys. |
| `DocumentReading:ModelId`, `DocumentReading:ApiVersion`, `DocumentReading:TokenScope`, `DocumentReading:HttpTimeout` | model id, API version, scope URI, `hh:mm:ss` | Provider call settings. Defaults (`prebuilt-read`, `2024-11-30`, the Cognitive Services scope, 30 seconds) are in `appsettings.json`. |
| `DocumentReading:WorkerEnabled` | `false` (default), `true` | Whether background reading runs, separately from `Auth`. |

| `Embedding:Auth` | `Disabled` (default), `AzureManagedIdentity`, `DeveloperCredential` | How the API authenticates to the Azure OpenAI embedding deployment. `Disabled` turns embedding off; `DeveloperCredential` uses your `az login` identity (role **Cognitive Services OpenAI User**) and is refused outside the `Development` environment. Invalid embedding settings log a warning at startup and turn embedding off; the API still starts. |
| `Embedding:Endpoint` | `https://<account>.openai.azure.com/` | Required unless embedding is `Disabled`. Not a secret: the resource accepts only Entra ID tokens. |
| `Embedding:Deployment`, `Embedding:Dimensions`, `Embedding:TokenScope`, `Embedding:HttpTimeout` | deployment name, integer, scope URI, `hh:mm:ss` | Provider call settings. Defaults (`text-embedding-3-large`, `1536`, the Cognitive Services scope, 30 seconds) are in `appsettings.json`. `Dimensions` must stay `1536`, the size of the stored vector column. |
| `Embedding:WorkerEnabled` | `false` (default), `true` | Whether the background embedding worker runs, separately from `Auth`. |
| `Search:MinSimilarity` | number between 0 and 1, default `0.30` | Cosine similarity threshold used by semantic search. |

Secrets never go into tracked files: local passwords and connection strings live in `.env` (gitignored) and user secrets; Azure uses managed identities.

## Reading probe (optional)

`ocr-probe` sends local files to Azure Document Intelligence `prebuilt-read`, prints what it recovered, and asks the provider to delete each analysis result. It needs neither PostgreSQL nor Azurite, and runs only in the `Development` environment. It needs an existing Document Intelligence resource and an account that holds the **Cognitive Services User** role on it.

```bash
az login                                # the account with Cognitive Services User on the resource
dotnet user-secrets set DocumentReading:Auth DeveloperCredential --project services/api
dotnet user-secrets set DocumentReading:Endpoint "https://<account>.cognitiveservices.azure.com/" --project services/api
dotnet run --project services/api -- ocr-probe "$PWD/services/api.Tests/Fixtures/Reading/synthetic-note.png"
```

Pass absolute paths: `dotnet run` starts the API in its project directory, so a relative path is reported as `file not found`.

For each file (`.jpg`, `.jpeg`, `.png` or `.pdf`) it reports page, line, word and character counts, average word confidence, elapsed time, the result deletion status and whether the file is unchanged. A `204` deletion status means the provider accepted the request, not that erasure was verified. It submits each file once and never retries a submission.

Extracted text is printed to the console only with `--print-text` (`ocr-probe --print-text <file>...`) and is never written to disk or logs. Never commit it or paste it into the repository. To return to the default, remove the two secrets with `dotnet user-secrets remove`.

Ordinary tests never call Azure: reading is `Disabled` by default, and the probe's tests use a fake provider.

## Background reading (optional)

By default reading is `Disabled`: captured documents get a `queued` reading status that never changes, and everything else works. To read documents locally, use the same two secrets as the probe and turn the worker on:

```bash
dotnet user-secrets set DocumentReading:WorkerEnabled true --project services/api
dotnet run --project services/api
```

The startup log says whether reading is available and whether the worker runs. The worker waits 60 seconds after startup, then retries due provider cleanup and reads due documents one at a time, including every `Stored` document already in your local database. Each photo and each PDF page without a usable text layer is sent to Azure and billed, so use a separate local database (a different `Database=` name in `ConnectionStrings:Default`) if your existing one holds documents you do not want read. Use only synthetic or your own non-sensitive files.

To stop reading, remove `DocumentReading:WorkerEnabled` (or set it to `false`) and restart; jobs, read text and pending cleanups stay in the database.

## Background embedding (optional)

Once a document is read, a second background worker cuts its text into chunks, embeds them with Azure OpenAI and stores the vectors in PostgreSQL (pgvector). It is off by default. To embed locally, use your own identity (role **Cognitive Services OpenAI User** on the resource) and turn the worker on:

```bash
az login
dotnet user-secrets set Embedding:Auth DeveloperCredential --project services/api
dotnet user-secrets set Embedding:Endpoint "https://<account>.openai.azure.com/" --project services/api
dotnet user-secrets set Embedding:WorkerEnabled true --project services/api
dotnet run --project services/api
```

The startup log says whether embedding is available and whether the worker runs. Every document that is already read (and every one read or renamed later) is queued for embedding, so use a separate local database if your existing one holds documents you do not want sent to Azure. Use only synthetic or your own non-sensitive files. Remove `Embedding:WorkerEnabled` (or set it to `false`) and restart to stop; jobs and stored vectors stay in the database.

## Database migrations

The API applies pending migrations at startup and will not start without a reachable database. After changing the EF Core model, add a migration:

```bash
dotnet ef migrations add <Name> --project services/api --output-dir Data/Migrations
dotnet ef database update --project services/api     # optional: apply without starting the API
```

Migrations are forward-only in practice: deployed data is not rolled back with code, so a schema change must stay compatible with the previous release (expand first, contract later).
