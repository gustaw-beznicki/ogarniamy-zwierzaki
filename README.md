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
- An integration test suite that runs against a real PostgreSQL container.

Everything else in the MVP is planned, not built yet:

- Owner accounts with strict isolation between users.
- Animal profiles and per-animal document views.
- Photo and PDF upload with an editable veterinary-event date, stored privately.
- Text extraction from PDFs and OCR for scans and photos.
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

Quick start: start PostgreSQL with `docker compose up -d`, store the connection string in user secrets, then run `dotnet run --project services/api` and `npm run dev` in `apps/web`. The [local development guide](docs/local-development.md) has the exact steps.

## Further reading

- [Product requirements](context/foundation/prd.md)
- [Roadmap](context/foundation/roadmap.md)
- [Technology stack](context/foundation/tech-stack.md)
- [Infrastructure decisions and risks](context/foundation/infrastructure.md)
