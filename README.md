# Ogarniamy Zwierzaki

> A private, searchable archive for veterinary documents.

[![Astro](https://img.shields.io/badge/Astro-7-BC52EE?logo=astro&logoColor=white)](https://astro.build/)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Project status](https://img.shields.io/badge/status-early_development-orange)](#status)

Ogarniamy Zwierzaki helps pet owners keep veterinary records from different clinics in one place and find the right document by meaning, not only by filename or exact wording. Owners upload photos and PDFs; the original always stays the source of truth, and search returns the matching source fragments, never generated medical advice.

## Status

The project is in early development. What runs today:

- An Astro frontend and an ASP.NET Core API, deployed to Azure by CI/CD.
- A PostgreSQL database that the API migrates at startup; `GET /api/health` is healthy only when the database is reachable.
- Email and password registration, sign-in and sign-out with cookie sessions.
- First-animal onboarding and an Animals list isolated by owner.
- Document capture on the Add page: take a photo or choose one PDF or 1–10 JPEG/PNG photos (each at most 10 MiB, 10,485,760 bytes), reorder the photos, pick the animal and the date of the veterinary event (today or earlier), and save. The animal defaults to the one used in the account's last saved capture.
- Originals are stored byte for byte in a private Blob Storage container and are never public: a per-animal document list and a document view open them only through the signed-in owner's session. The event date and the upload time are shown separately.
- A Polish and English interface, with bottom navigation on phones and a sidebar on desktop. Search is a placeholder.
- An integration test suite that runs against real PostgreSQL and Azurite (Blob Storage emulator) containers.

Everything else in the MVP is planned, not built yet:

- Password reset, email verification and additional sign-in methods.
- Animal profile editing, inactive status and a full animal profile.
- Editing or deleting documents after they are saved, and HEIC/HEIF photos.
- Text extraction from PDFs and OCR for scans and photos. Stored documents are not read or searchable yet.
- Semantic search across document content, optionally filtered by animal, showing the matching fragment and opening the original.

The requirements, guardrails and non-goals are in the [PRD](context/foundation/prd.md); the delivery order is in the [roadmap](context/foundation/roadmap.md).

## Documentation

| Topic | Read |
| --- | --- |
| How the pieces fit together and where the code lives | [Architecture](docs/architecture.md) |
| Running the database, API and web app on your machine | [Local development](docs/local-development.md) |
| Test suite and build checks | [Tests and checks](docs/testing.md) |
| CI/CD, the Azure environment and setting up a new one | [Deployment](docs/deployment.md) |
| Conventions for code and changes | [Contributing](CONTRIBUTING.md) |

## Local development

Install Node.js 22.12 or newer, .NET SDK 10 and Docker with Compose. Copy `.env.example` to `.env`, set a local database password, then start PostgreSQL and Azurite (the local Blob Storage emulator for originals) with `docker compose up -d`. Store the database and storage connection strings in .NET user secrets as described in the [local development guide](docs/local-development.md); keep passwords outside tracked files.

Run the API and frontend in separate terminals:

```bash
dotnet run --project services/api
```

```bash
npm ci --prefix apps/web
npm run dev --prefix apps/web
```

Open `http://localhost:4321/signin/` for Polish or `http://localhost:4321/en/signin/` for English. The frontend proxies `/api/*` to the local API at `http://localhost:5180`. Register, save the first animal's name, then add a document from Add and open it from the animal's documents in Animals.

## Verification

With Docker running, run the integration suite and component checks:

```bash
dotnet test services/api.Tests/ogarniamy-zwierzaki-api.Tests.csproj
dotnet build services/api/ogarniamy-zwierzaki-api.csproj
npm run check --prefix apps/web
npm run build --prefix apps/web
```

The tests use disposable PostgreSQL and Azurite containers and cover authentication, animals, document capture and retrieval, isolation between accounts, and recovery from storage and database failures. Pull-request CI also lints Bicep and previews infrastructure changes. See [tests and checks](docs/testing.md) for details.

After deployment, `scripts/smoke.sh <swa-host> <api-host>` checks both locale sign-in pages, API and database health through the proxy (with a 600-second retry window), 401 responses without a redirect for anonymous `/api/me` and document/original requests, and refusal of direct API access. It creates no accounts and changes no data.

## Rollback

Revert the offending commit on `main` and approve the resulting deployment, or re-run an earlier successful deployment. This restores code only: accounts, animals, documents, session keys, the migrated database schema and every stored original remain. Use forward-only, backward-compatible migrations (expand first, contract later) so the previous release can still read the current data. Database recovery is a separate operation. See the [deployment guide](docs/deployment.md).

## Further reading

- [Product requirements](context/foundation/prd.md)
- [Roadmap](context/foundation/roadmap.md)
- [Technology stack](context/foundation/tech-stack.md)
- [Infrastructure decisions and risks](context/foundation/infrastructure.md)
