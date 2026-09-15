<!-- BEGIN @przeprogramowani/10x-cli -->

## Zestaw narzędzi AI 10xDevs — Moduł 1, Lekcja 1

Zainicjuj projekt greenfield kompleksowo za pomocą **łańcucha kształtowania**:

```
/10x-init  →  /10x-shape  →  /10x-prd  →  (10x-tech-stack-selector)  →  (bootstrapper)
```

Pierwsze trzy umiejętności są dostępne w tej lekcji; dwie ostatnie to kolejne ogniwa łańcucha.

### Router zadań — Od czego zacząć

| Umiejętność | Użyj jej, gdy |
| --- | --- |
| **Konfiguracja projektu** | |
| `/10x-init` | Katalog projektu jest świeży. Tworzy szkielety `context/foundation/lessons.md` i `docs/reference/contract-surfaces.md`, aby reszta przepływu pracy miała miejsce do zapisu. Uruchom to raz na projekt. |
| **Odkrywanie** | |
| `/10x-shape` | Masz pomysł i musisz przekształcić go w ustrukturyzowane notatki kształtu PRZED napisaniem PRD. Tylko greenfield. Prowadzi przez: wizję → personę/dostęp → MVP → FR-y (z sokratejskim kwestionowaniem) → logikę biznesową i dane → szkic otwartości stosu. Wprost wskazuje antywzorce pustego CRUD i zbyt dużego MVP. Wynik: `context/foundation/shape-notes.md` ze wznawialnym blokiem `checkpoint:`. |
| **Generowanie dokumentu** | |
| `/10x-prd` | Masz notatki kształtu (lub surowe notatki) i chcesz uzyskać zgodny ze schematem plik `context/foundation/prd.md`. Generuje na podstawie zablokowanego schematu, przekazuje każdą lukę dosłownie do `## Otwarte pytania` i odmawia wymyślania decyzji domenowych. W przypadku kolizji pyta o nadpisanie lub zapis wersjonowany (`prd-vN.md`). |

### Jak następuje przekazanie w łańcuchu

- `/10x-init` tworzy szkielet workflow v2 (`context/foundation/`, `lessons.md`, `contract-surfaces.md`). `/10x-shape` wymaga tego i zaproponuje delegowanie do `/10x-init`, jeśli tego brakuje.
- `/10x-shape` zapisuje `context/foundation/shape-notes.md` z frontmatterem `checkpoint:` (current_phase, phases_completed, frs_drafted, quality_check_status). Przy ponownym wejściu wznawia od następnej nieukończonej fazy.
- `/10x-prd` odczytuje `shape-notes.md` (domyślnie) lub dowolną podaną ścieżkę, ocenia dane wejściowe według heurystyki 4 sygnałów, ostrzega przy zbyt skąpych danych wejściowych i zapisuje `context/foundation/prd.md` zgodnie ze schematem w `skills/10x-shape/references/prd-schema.md` (frontmatter wyrównany 1:1 z Q1–Q7 narzędzia 10x-tech-stack-selector).

### Co PRD zawiera (a czego NIE zawiera)

- **Zawiera**: wizję, personę, kryteria sukcesu, historie użytkownika (Given/When/Then), FR-y (FR-NNN), NFR-y, logikę biznesową (najpierw reguła w jednym zdaniu), model danych, kontrolę dostępu, trwałe decyzje implementacyjne, strategię testowania, strategię wdrożeń i CI/CD, elementy poza zakresem, otwarte pytania.
- **NIE zawiera (celowo)**: wyborów frameworków, wyborów baz danych, ścieżek plików, platformy wdrożeniowej. Otwartość stosu jest wiążąca — tylko `product_type` oraz `tech_preferences.language_family` zapisują intencję związaną ze stosem. Frameworki są zadaniem 10x-tech-stack-selector.

### Antywzorce wykrywane podczas kształtowania

- **Pusty CRUD**: logika biznesowa sprowadzająca się do „użytkownicy dodają i usuwają rekordy” bez żadnej reguły domenowej. `/10x-shape` nazywa to wprost i prosi o kształt rzeczywistej reguły (rekomendacja, priorytetyzacja, klasyfikacja, walidacja, punktacja, przepływ pracy, obliczenie).
- **Zbyt duże MVP**: szacowany pierwszy przepływ przekracza ~1 tydzień pracy po godzinach albo obejmuje > 4 odrębne działania użytkownika przed uzyskaniem widocznej dla użytkownika wartości, albo wymaga wielu integracji przed osiągnięciem korzyści. Umiejętność wskazuje kosztowne elementy i oferuje konkretne sposoby ograniczenia zakresu.

Oba są **miękkimi bramkami**: ostrzegają, ale pozwalają na nadpisanie. Nadpisania są rejestrowane w punkcie kontrolnym i ujawniane w `## Otwarte pytania` PRD.

### Ścieżki foundation używane przez tę lekcję

- `context/foundation/shape-notes.md` — wynik `/10x-shape`
- `context/foundation/prd.md` (lub `prd-vN.md`) — wynik `/10x-prd`
- `context/foundation/lessons.md` — powtarzające się reguły i pułapki (tworzone przez `/10x-init`)
- `docs/reference/contract-surfaces.md` — rejestr nazw o kluczowym znaczeniu (tworzony przez `/10x-init`)

### Uniwersalny język

Dostarczone umiejętności nie zawierają odniesień do 10xDevs / kohorty / certyfikacji. Mechanizmy (sokratejskie kwestionowanie, odkrywanie szarych stref, łagodzenie zmęczenia zalecanymi odpowiedziami, miękka bramka jakości) są uniwersalnymi wskaźnikami dobrze określonego projektu greenfield.

Umiejętności nie mogą zapisywać do `context/archive/`. Zarchiwizowane zmiany są niezmienne; jeśli rozstrzygnięta ścieżka docelowa zaczyna się od `context/archive/`, przerwij z komunikatem: „Ta zmiana jest zarchiwizowana. Zamiast tego otwórz nową zmianę za pomocą `/10x-new`.”

<!-- END @przeprogramowani/10x-cli -->
