# Ogarniamy Zwierzaki

Ogarniamy Zwierzaki is an early-stage application for privately storing and semantically searching veterinary documents. Read `AGENTS.md` before editing; it contains the complete repository-wide contract and course commit rules.

## Current state

The repository contains a verified scaffold, not the completed MVP:

- `apps/web/`: Astro 7 frontend with strict TypeScript and npm.
- `services/api/`: ASP.NET Core 10 API with nullable reference types and implicit usings enabled.
- `context/`: product, stack, and scaffold documentation.

Authentication, frontend/API integration, private document storage, OCR, PostgreSQL/pgvector, background indexing, Azure resources, and CI are planned but not implemented. Never describe them as working features.

## Commands

| Command | Purpose |
| --- | --- |
| `npm ci --prefix apps/web` | Install locked frontend dependencies |
| `npm run dev --prefix apps/web` | Start Astro development server on port 4321 by default |
| `npm run build --prefix apps/web` | Build the frontend into `apps/web/dist/` |
| `npm run preview --prefix apps/web` | Preview the built frontend |
| `dotnet restore services/api/ogarniamy-zwierzaki-api.csproj` | Restore API packages |
| `dotnet run --project services/api/ogarniamy-zwierzaki-api.csproj` | Start the API using dynamic development ports |
| `dotnet build services/api/ogarniamy-zwierzaki-api.csproj --no-restore` | Build the restored API |

There are no application tests, lint scripts, deployment commands, or CI workflows yet. Do not claim those checks ran.

## Architecture

```text
apps/web/src/pages/index.astro         Astro entry page
                 |
                 |  integration not implemented
                 v
services/api/Program.cs                ASP.NET Core entry point
                 |
                 +-- storage/OCR/vector search/background jobs [planned]
```

Keep browser/UI concerns in `apps/web` and application/API logic in `services/api`.

## Authoritative files

- `README.md`: public status and local setup.
- `context/foundation/prd.md`: product requirements, scope, and guardrails.
- `context/foundation/tech-stack.md`: selected architecture and deployment direction.
- `context/foundation/shape-notes.md`: discovery history and the OCR validation premise.
- `context/changes/bootstrap-verification/verification.md`: scaffold and dependency-audit evidence.
- `context/foundation/scaffold-adapters/`: researched scaffold commands.

Foundation documents evolve in place. Put change-specific material in `context/changes/<change-id>/`.

## Product guardrails

- Original documents remain retrievable even when extraction or search fails.
- Owner isolation applies to every list and semantic-search query; cross-account results are a critical failure.
- Search returns documents and matching source fragments, never generated medical advice or conclusions.
- The MVP does not delete documents or animal profiles; animals may be marked inactive.
- Event date and upload date are different concepts. The event-date default is a documented limitation.

## Project workflow

- Preserve unrelated working-tree and untracked files.
- Keep generated output untracked: `apps/web/node_modules/`, `apps/web/dist/`, `apps/web/.astro/`, `services/api/bin/`, and `services/api/obj/`.
- Never edit `context/archive/`. If asked, instruct the user to open a new change with `/10x-new`.
- Preserve Astro strict TypeScript and the API nullable-reference configuration unless an intentional migration requires otherwise.
- Keep secrets out of tracked files. `.claude/settings.json` controls local command permissions; do not weaken it during unrelated work.

The completed 10xDevs foundation chain is:

```text
/10x-init -> /10x-shape -> /10x-prd -> /10x-tech-stack-selector -> /10x-scaffold-adapter -> /10x-bootstrapper
```

Re-run a stage only when its source artifact needs revision. Course skill copies and manifests under `.claude/` are managed by the 10x CLI; do not hand-edit them.

For a course-related commit, require the current module/lesson number, create tag `m<module>l<lesson>`, and use a descriptive commit message. Lesson-agnostic maintenance may use a normal commit only when the user explicitly identifies it that way.

## Gotchas

- API launch profiles use port `0`; copy the actual URL printed by `dotnet run`.
- The only API route is the scaffold `GET /weatherforecast`; OpenAPI is development-only.
- A NuGet vulnerability lookup that cannot reach `https://api.nuget.org/v3/index.json` is inconclusive, not a clean audit.
- The first product-risk check is OCR quality on real veterinary-document photos; do not build semantic-search assumptions on unvalidated OCR output.
