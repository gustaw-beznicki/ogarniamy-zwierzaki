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
<!-- BEGIN @przeprogramowani/10x-cli -->

## Zestaw narzędzi AI 10xDevs — Moduł 2, Lekcja 3

Przed scaleniem przejrzyj kod wygenerowany przez AI za pomocą **łańcucha przeglądu implementacji**:

```
/10x-implement -> /10x-impl-review -> triage -> (/10x-lesson | fix | skip | disagree)
```

`/10x-impl-review` jest głównym tematem lekcji. Przegląd jest bramką jakości, a nie poleceniem naprawienia każdego znaleziska.

### Router zadań — od czego zacząć

| Umiejętność | Użyj jej, gdy |
| --- | --- |
| **Przegląd kodu (główny temat lekcji)** | |
| `/10x-impl-review <change-id>` | Zaimplementowałeś kod i chcesz przeprowadzić ustrukturyzowany przegląd przed scaleniem. Umiejętność sprawdza zgodność z planem, dyscyplinę zakresu, bezpieczeństwo i jakość, architekturę, spójność wzorców oraz kryteria sukcesu, a następnie przedstawia znaleziska do triage. |
| **Rezultat powtarzającej się lekcji** | |
| `/10x-lesson` | Znalezisko ujawnia powtarzającą się regułę projektu lub wzorzec błędów agenta. Zapisz je w `context/foundation/lessons.md` zamiast traktować jako jednorazową notatkę. |

### Dyscyplina triage

- Severity określa, jak poważne jest znalezisko. Impact określa, jak duże znaczenie ma teraz decyzja.
- Prawidłowe rezultaty: napraw teraz, napraw inaczej, pomiń, zaakceptuj jako ryzyko, zapisz jako powtarzającą się regułę (`/10x-lesson`), nie zgódź się.
- Napraw krytyczne znaleziska. Nie poświęcaj godzin na obserwacje o niskim wpływie tylko dlatego, że agent je znalazł.
- Świadome pomijanie znalezisk o niskim wpływie jest prawidłowym wynikiem przeglądu, a nie zaniedbaniem.
- Jeśli nie zgadzasz się ze znaleziskiem, zapisz dlaczego. Błędne rozumowanie agenta również jest sygnałem.

### Granice przeglądu

- Ta lekcja dotyczy przeglądu zaimplementowanego kodu. Nie tworzy planu, nie wykonuje nowych faz ani nie uczy przeglądu CI.
- Strategia testowania i bramki jakości są wprowadzane w Module 3.
- W tej lekcji nie używaj `/10x-contract` jako wyniku triage.

### Ścieżki używane przez tę lekcję

- `context/changes/<change-id>/plan.md` — oczekiwany kontrakt implementacji
- `context/changes/<change-id>/reviews/` — wynik przeglądu
- `context/foundation/lessons.md` — powtarzające się lekcje

Umiejętności nie mogą zapisywać do `context/archive/`. Zarchiwizowane zmiany są niezmienne; jeśli rozwiązana ścieżka docelowa zaczyna się od `context/archive/`, przerwij z komunikatem: „Ta zmiana jest zarchiwizowana. Zamiast tego otwórz nową zmianę za pomocą `/10x-new`.”

<!-- END @przeprogramowani/10x-cli -->
