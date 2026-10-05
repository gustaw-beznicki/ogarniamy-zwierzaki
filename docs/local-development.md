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

This starts PostgreSQL and [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite), the local Blob Storage emulator that holds document originals.

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

Secrets never go into tracked files: local passwords and connection strings live in `.env` (gitignored) and user secrets; Azure uses managed identities.

## Reading probe (optional)

`ocr-probe` sends local files to Azure Document Intelligence `prebuilt-read`, prints what it recovered, and asks the provider to delete each analysis result. It needs neither PostgreSQL nor Azurite, and runs only in the `Development` environment. It needs an existing Document Intelligence resource and an account that holds the **Cognitive Services User** role on it.

```bash
az login                                # the account with Cognitive Services User on the resource
dotnet user-secrets set DocumentReading:Auth DeveloperCredential --project services/api
dotnet user-secrets set DocumentReading:Endpoint "https://<account>.cognitiveservices.azure.com/" --project services/api
dotnet run --project services/api -- ocr-probe services/api.Tests/Fixtures/Reading/synthetic-note.png
```

For each file (`.jpg`, `.jpeg`, `.png` or `.pdf`) it reports page, line, word and character counts, average word confidence, elapsed time, the result deletion status and whether the file is unchanged. A `204` deletion status means the provider accepted the request, not that erasure was verified. It submits each file once and never retries a submission.

Extracted text is printed to the console only with `--print-text` (`ocr-probe --print-text <file>...`) and is never written to disk or logs. Never commit it or paste it into the repository. To return to the default, remove the two secrets with `dotnet user-secrets remove`.

Ordinary tests never call Azure: reading is `Disabled` by default, and the probe's tests use a fake provider.

## Database migrations

The API applies pending migrations at startup and will not start without a reachable database. After changing the EF Core model, add a migration:

```bash
dotnet ef migrations add <Name> --project services/api --output-dir Data/Migrations
dotnet ef database update --project services/api     # optional: apply without starting the API
```

Migrations are forward-only in practice: deployed data is not rolled back with code, so a schema change must stay compatible with the previous release (expand first, contract later).
