# Architecture

```text
Browser
   |
   v
Azure Static Web Apps (Standard)        apps/web      Astro, static output
   |  /api/*  same-origin proxy (linked backend)
   v
Azure App Service (Linux)               services/api  ASP.NET Core 10
   |  Entra token of the App Service managed identity
   v
Azure Database for PostgreSQL           Flexible Server 17, Entra-only auth
```

- The page and `/api/*` share one origin, so there is no CORS configuration. The App Service accepts traffic only through the Static Web App.
- The API never stores a database password in Azure: it signs in with its managed identity. Locally and in tests it uses an ordinary password connection string.
- Planned additions: private blob storage for originals, OCR, pgvector search, and background indexing.

## Repository layout

```text
apps/web/            Astro frontend (strict TypeScript)
services/api/        ASP.NET Core API; EF Core model and migrations in Data/
services/api.Tests/  xUnit integration tests (WebApplicationFactory + Testcontainers)
infra/               Bicep templates and deploy.sh (lint / what-if / apply)
  bootstrap/         One-time, hand-applied setup: CI identities, resource group, budget
scripts/smoke.sh     End-to-end check of a deployed environment
compose.yaml         Local PostgreSQL
docs/                Developer documentation (this folder)
context/             Product and planning documents (PRD, stack, roadmap, change plans)
.github/workflows/   ci.yml (pull requests), deploy.yml (main)
```
