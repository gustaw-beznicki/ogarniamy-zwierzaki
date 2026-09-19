# Ogarniamy Zwierzaki

> A private, searchable archive for veterinary documents.

[![Astro](https://img.shields.io/badge/Astro-7-BC52EE?logo=astro&logoColor=white)](https://astro.build/)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Project status](https://img.shields.io/badge/status-early_development-orange)](#current-status)

Ogarniamy Zwierzaki helps pet owners keep veterinary records from different clinics in one place and find the right document by meaning, not only by filename or exact wording. The planned MVP accepts photos and PDFs, keeps each original as the source of truth, and returns matching source fragments instead of generating medical advice.

> [!IMPORTANT]
> The repository currently contains a verified frontend and API scaffold. Authentication, document storage, OCR, semantic search, and the remaining product flows described below are planned but not implemented yet.

## Planned MVP

- Owner accounts with strict isolation between users.
- Multiple animal profiles and per-animal document views.
- Photo and PDF upload with an editable veterinary-event date.
- Private storage of the original document.
- Text extraction from digital PDFs and OCR for scans or photos.
- Semantic search across document content, optionally filtered by animal.
- Search results that show the matching fragment and open the original.

The product deliberately returns documents and quoted source fragments—not generated diagnoses, summaries, or other medical conclusions.

## Current status

| Area | Location | State |
| --- | --- | --- |
| Web | [`apps/web`](apps/web) | Astro 7 minimal starter with strict TypeScript configuration and a placeholder page |
| API | [`services/api`](services/api) | ASP.NET Core 10 Web API starter with development OpenAPI output and the sample weather endpoint |
| Product definition | [`context/foundation/prd.md`](context/foundation/prd.md) | MVP requirements, guardrails, non-goals, and open questions documented |
| Technical direction | [`context/foundation/tech-stack.md`](context/foundation/tech-stack.md) | Astro + ASP.NET Core split, with Azure App Service recorded as the deployment target |
| Scaffold verification | [`context/changes/bootstrap-verification/verification.md`](context/changes/bootstrap-verification/verification.md) | Both components scaffolded and build-verified |

No database, cloud resources, CI workflow, or frontend-to-API integration is present yet.

## Architecture

The current source layout establishes two independently runnable components. The downstream services are the planned direction from the project documents.

```text
apps/web (Astro + TypeScript)
              |
              v
services/api (ASP.NET Core)
              |
              +-- private original-document storage    [planned]
              +-- PDF text extraction / OCR            [planned]
              +-- PostgreSQL + pgvector                 [planned]
              +-- background indexing                  [planned]
```

## Repository structure

```text
.
├── apps/
│   └── web/                         # Astro frontend
├── services/
│   └── api/                         # ASP.NET Core API
├── context/
│   ├── foundation/                  # PRD, stack decisions, and scaffold adapters
│   └── changes/bootstrap-verification/
│       └── verification.md          # Scaffold execution and verification record
├── AGENTS.md                        # Repository guidance for coding agents
├── CLAUDE.md                        # Claude-specific project guidance
└── README.md
```

## Getting started

### Prerequisites

- [Node.js](https://nodejs.org/) 22.12 or newer and npm
- [.NET SDK](https://dotnet.microsoft.com/download) 10
- [Git](https://git-scm.com/)

Clone the repository:

```bash
git clone git@github.com:gustaw-beznicki/ogarniamy-zwierzaki.git
cd ogarniamy-zwierzaki
```

### Run the web app

```bash
cd apps/web
npm ci
npm run dev
```

Astro serves the app at `http://localhost:4321` by default.

### Run the API

From another terminal at the repository root:

```bash
dotnet restore services/api/ogarniamy-zwierzaki-api.csproj
dotnet run --project services/api/ogarniamy-zwierzaki-api.csproj
```

The development launch profile assigns available HTTP and HTTPS ports dynamically. Use the addresses printed by `dotnet run`. The scaffold currently exposes `GET /weatherforecast`; its OpenAPI document is available in development mode.

## Verification

Build both components independently:

```bash
npm ci --prefix apps/web
npm run build --prefix apps/web
dotnet restore services/api/ogarniamy-zwierzaki-api.csproj
dotnet build services/api/ogarniamy-zwierzaki-api.csproj --no-restore
```

Automated application tests have not been added yet.

## Project documentation

- [Product requirements](context/foundation/prd.md)
- [Shaping notes](context/foundation/shape-notes.md)
- [Technology stack decision](context/foundation/tech-stack.md)
- [Scaffold adapter manifest](context/foundation/scaffold-adapters/manifest.md)
- [Bootstrap verification log](context/changes/bootstrap-verification/verification.md)

## Course history

This project is developed as part of 10xDevs. Course milestones are marked with Git tags in the form `m<module>l<lesson>`, for example `m1l1`.
