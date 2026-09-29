# Local development

## Prerequisites

- [Node.js](https://nodejs.org/) 22.12 or newer, with npm
- [.NET SDK](https://dotnet.microsoft.com/download) 10
- [Docker](https://docs.docker.com/get-docker/) with Compose (local database and tests)
- EF Core CLI, only for creating migrations: `dotnet tool install --global dotnet-ef`

## 1. Start PostgreSQL

```bash
cp .env.example .env          # then set POSTGRES_PASSWORD to any local password
docker compose up -d
```

If port 5432 is already taken on your machine, set `POSTGRES_PORT` in `.env` (for example `5433`) and use that port in the connection string below.

## 2. Point the API at it

The connection string is kept in [.NET user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets), outside the repository. Use the password from `.env`:

```bash
dotnet user-secrets set ConnectionStrings:Default \
  "Host=localhost;Port=5432;Database=ogarniamy;Username=postgres;Password=<password from .env>" \
  --project services/api
```

## 3. Run the API and the web app

```bash
dotnet run --project services/api       # http://localhost:5180, applies migrations on start
```

```bash
cd apps/web
npm ci
npm run dev                             # http://localhost:4321
```

The Astro dev server proxies `/api/*` to `http://localhost:5180`, just like Static Web Apps does in Azure. Set `API_PROXY_TARGET` to proxy elsewhere. Check the stack with `curl http://localhost:5180/api/health`, which should return `{"status":"ok"}`.

## Configuration

| Key | Values | Purpose |
| --- | --- | --- |
| `ConnectionStrings:Default` | Npgsql connection string | Required. Includes a password only in `Password` mode. |
| `Database:Auth` | `Password` (default), `AzureManagedIdentity` | How the API authenticates to PostgreSQL. Azure sets `AzureManagedIdentity` through App Service settings. |
| `Database:EntraTokenScope`, `Database:TokenRefreshInterval`, `Database:TokenRetryInterval` | scope URI, `hh:mm:ss` | `AzureManagedIdentity` only: which Entra token to request, how often to refresh it, and how soon to retry a failure. Defaults are in `appsettings.json`. |

Secrets never go into tracked files: local passwords live in `.env` (gitignored) and user secrets; Azure uses managed identities.

## Database migrations

The API applies pending migrations at startup and will not start without a reachable database. After changing the EF Core model, add a migration:

```bash
dotnet ef migrations add <Name> --project services/api --output-dir Data/Migrations
dotnet ef database update --project services/api     # optional: apply without starting the API
```

Migrations are forward-only in practice: deployed data is not rolled back with code, so a schema change must stay compatible with the previous release (expand first, contract later).
