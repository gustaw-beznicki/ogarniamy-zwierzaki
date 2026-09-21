# Repository instructions

## Project overview

Current product status, architecture, setup, and public documentation are maintained in `@README.md`.

Do not present planned capabilities as implemented. The repository currently contains a verified Astro frontend and ASP.NET Core API scaffold; the remaining product flows are planned.

## Authoritative context

- Product requirements and guardrails: `@context/foundation/prd.md`
- Stack and deployment direction: `@context/foundation/tech-stack.md`
- Original discovery record: `@context/foundation/shape-notes.md`
- Scaffold evidence: `@context/changes/bootstrap-verification/verification.md`
- Scaffold adapters: `@context/foundation/scaffold-adapters/`

Foundation documents evolve in place. Change-scoped notes belong under `context/changes/<change-id>/`.

Never edit anything under `context/archive/`. If a requested target starts there, stop with: “Ta zmiana jest zarchiwizowana. Zamiast tego otwórz nową zmianę za pomocą `/10x-new`.”

## Product invariants

- A stored original must remain retrievable even if OCR or search fails.
- Every list and semantic-search query must isolate data by owner; cross-account fragments are a critical security failure.
- Search returns source documents and supporting fragments, never generated medical advice or conclusions.
- The MVP does not delete documents or animal profiles; animals may be marked inactive.
- Clearly distinguish uploaded/event dates. Event-date defaults are a known product limitation documented in the PRD.

## Setup and verification

- Setup, run, and build instructions: `@README.md`
- Web scripts and supported Node.js version: `@apps/web/package.json`
- Web TypeScript configuration: `@apps/web/tsconfig.json`
- API project settings: `@services/api/ogarniamy-zwierzaki-api.csproj`
- API development launch configuration: `@services/api/Properties/launchSettings.json`

Run the documented build for every component you change. Install dependencies or restore packages first when required.

Generated outputs such as `apps/web/node_modules/`, `apps/web/dist/`, `apps/web/.astro/`, `services/api/bin/`, and `services/api/obj/` must remain untracked.

## Code and architecture conventions

- Keep browser/UI concerns in `apps/web` and application/API logic in `services/api`.
- Do not change strict TypeScript, nullable reference types, or implicit usings unless the task explicitly requires an intentional configuration migration.
- Never add credentials, API keys, or connection strings to tracked files. If a feature requires a secret and no storage convention exists, stop and ask where it should be stored.
- Edit only files required by the current task. Do not revert, format, stage, or commit unrelated changes or untracked files.

## Git and course commits

- Inspect `git status` before editing and before committing. Do not overwrite unrelated changes.
- Every course-related commit requires a module/lesson identifier supplied for the current task.
- Commits that are not tied to a specific lesson may omit the module/lesson identifier and are considered lesson-agnostic.
- Use the tag format `m<module>l<lesson>`, for example `m1l1`, on the commit containing that lesson’s result.
- The commit message must describe the actual change; the lesson number alone is not a valid message.
