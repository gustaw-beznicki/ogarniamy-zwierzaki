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

## Zestaw narzędzi AI 10xDevs — Moduł 2, Lekcja 2

Przekształć jeden element roadmapy w pierwszy cykl implementacji za pomocą **łańcucha planowania zmian**:

```
/10x-roadmap -> /10x-new -> /10x-plan -> /10x-plan-review -> /10x-implement
```

`/10x-new`, `/10x-plan`, `/10x-plan-review` i `/10x-implement` są przedmiotem tej lekcji. `/10x-frame` i `/10x-research` nie są tutaj wymaganymi rytuałami; są ścieżkami eskalacji wprowadzanymi w następnej lekcji.

### Router zadań — od czego zacząć

| Umiejętność | Użyj jej, gdy |
| --- | --- |
| **Przygotowanie zmiany (temat lekcji)** | |
| `/10x-new <change-id>` | Wybrano element roadmapy i potrzebujesz stabilnego folderu zmiany. Tworzy `context/changes/<change-id>/change.md`, aby planowanie, implementacja, postęp, commity i późniejszy przegląd współdzieliły jedną tożsamość. Użyj PO wyborze roadmapy, PRZED `/10x-plan`. |
| **Planowanie (temat lekcji)** | |
| `/10x-plan <change-id>` | Masz folder zmiany i potrzebujesz planu implementacji możliwego do przeglądu. Odczytuje kontekst roadmapy, dokumenty bazowe, dowody z codebase oraz wszelkie istniejące notatki o zmianie; zapisuje `plan.md` i `plan-brief.md` z fazami, kontraktami plików, kryteriami sukcesu i `## Progress`. |
| **Gotowość planu (temat lekcji)** | |
| `/10x-plan-review <change-id>` | Masz `plan.md` i potrzebujesz lekkiej kontroli gotowości przed kodowaniem. Użyj go, aby wychwycić brakujący stan końcowy, słabe kontrakty, niepoprawnie sformatowany postęp, dryf zakresu lub martwe punkty przed rozpoczęciem zmian w kodzie. |
| **Implementacja (temat lekcji)** | |
| `/10x-implement <change-id> phase <n>` | Masz zatwierdzony plan i chcesz wykonać jedną fazę wraz z weryfikacją, ręczną bramką, rytuałem commitu i zapisem SHA w `## Progress`. |
| **Zamknięcie cyklu życia** | |
| `/10x-archive <change-id>` | Zmiana została scalona lub celowo zamknięta. Przenieś ją z aktywnego `context/changes/` do stanu archiwalnego. |

### Jak następuje przekazanie w łańcuchu

- `/10x-new` tworzy trwałą tożsamość zmiany.
- `/10x-plan` przekształca tę tożsamość w kontrakt implementacyjny.
- `/10x-plan-review` sprawdza plan, zanim agent zmodyfikuje kod.
- `/10x-implement` wykonuje jedną zaplanowaną fazę, weryfikuje ją, prosi o ręczne potwierdzenie, gdy jest potrzebne, wykonuje commit i zapisuje postęp.

### Granice lekcji

- Plan jest domyślnym routerem po wyborze roadmapy. Zacznij od `/10x-plan`, chyba że problem jest niejasny lub blokują Cię zewnętrzne dowody.
- Nie uruchamiaj `/10x-frame + /10x-research` jako ceremonii dla każdej zmiany.
- Nie przekształcaj tej lekcji w kompletny, end-to-endowy build produktu. Punkt kontrolny z zaplanowanym i częściowo lub w pełni zaimplementowanym strumieniem jest prawidłowy.
- Przegląd kodu zaimplementowanego diffu należy do Lekcji 3 przez `/10x-impl-review`.
- Zamknięcie cyklu życia przez `/10x-archive` po scaleniu zmiany lub jej celowym zamknięciu.

### Ścieżki używane przez tę lekcję

- `context/foundation/roadmap.md` - nadrzędna roadmapa
- `context/changes/<change-id>/change.md` - tożsamość zmiany
- `context/changes/<change-id>/plan.md` - kontrakt implementacyjny
- `context/changes/<change-id>/plan-brief.md` - skompresowane przekazanie
- `context/foundation/lessons.md` - powtarzające się zasady i pułapki
- `docs/reference/contract-surfaces.md` - rejestr nazw mających kluczowe znaczenie

Umiejętności nie mogą zapisywać do `context/archive/`. Zarchiwizowane zmiany są niezmienne; jeśli rozwiązana ścieżka docelowa zaczyna się od `context/archive/`, przerwij z komunikatem: „Ta zmiana jest zarchiwizowana. Zamiast tego otwórz nową zmianę za pomocą `/10x-new`.”

<!-- END @przeprogramowani/10x-cli -->
