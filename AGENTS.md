<!-- BEGIN @przeprogramowani/10x-cli -->

## Zestaw narzędzi AI 10xDevs — Moduł 1, Lekcja 1

Uruchom projekt greenfield od początku do końca za pomocą **łańcucha kształtowania**:

```
/10x-init  →  /10x-shape  →  /10x-prd  →  (10x-tech-stack-selector)  →  (bootstrapper)
```

Pierwsze trzy umiejętności są dostarczane w tej lekcji; dwie ostatnie są kolejnymi ogniwami łańcucha.

### Router zadań — Od czego zacząć

| Umiejętność | Użyj jej, gdy |
| --- | --- |
| **Konfiguracja projektu** | |
| `/10x-init` | Katalog projektu jest świeży. Tworzy szkielety `context/foundation/lessons.md` i `docs/reference/contract-surfaces.md`, aby pozostała część przepływu pracy miała gdzie zapisywać. Uruchom to raz na projekt. |
| **Odkrywanie** | |
| `/10x-shape` | Masz pomysł i musisz przekształcić go w ustrukturyzowane notatki kształtu PRZED napisaniem PRD. Tylko greenfield. Prowadzi przez: wizja → persona/dostęp → MVP → FRs (z wyzwaniem sokratejskim) → logika biznesowa i dane → szkic otwartości stosu. Wprost wskazuje antywzorce empty-CRUD i MVP-too-big. Wynik: `context/foundation/shape-notes.md` ze wznawialnym blokiem `checkpoint:`. |
| **Generowanie dokumentu** | |
| `/10x-prd` | Masz shape-notes (lub surowe notatki) i chcesz uzyskać zgodny ze schematem plik `context/foundation/prd.md`. Generuje względem zablokowanego schematu, przekierowuje każdą lukę dosłownie do `## Open Questions` i odmawia wymyślania decyzji domenowych. W przypadku kolizji pyta o nadpisanie lub zapis wersjonowany (`prd-vN.md`). |

### Jak łańcuch przekazuje pracę dalej

- `/10x-init` tworzy szkielet workflow v2 (`context/foundation/`, `lessons.md`, `contract-surfaces.md`). `/10x-shape` wymaga tego i zaproponuje delegowanie do `/10x-init`, jeśli tego brakuje.
- `/10x-shape` zapisuje `context/foundation/shape-notes.md` z frontmatter `checkpoint:` (current_phase, phases_completed, frs_drafted, quality_check_status). Przy ponownym wejściu wznawia od następnej nieukończonej fazy.
- `/10x-prd` odczytuje `shape-notes.md` (domyślnie) lub dowolną przekazaną ścieżkę, ocenia dane wejściowe za pomocą heurystyki 4 sygnałów, ostrzega o zbyt skąpych danych wejściowych i zapisuje `context/foundation/prd.md` zgodnie ze schematem w `skills/10x-shape/references/prd-schema.md` (frontmatter wyrównany 1:1 z Q1–Q7 narzędzia 10x-tech-stack-selector).

### Co PRD zawiera (a czego NIE zawiera)

- **Zawiera**: wizję, personę, kryteria sukcesu, historie użytkownika (Given/When/Then), FRs (FR-NNN), NFRs, logikę biznesową (najpierw reguła w jednym zdaniu), model danych, kontrolę dostępu, trwałe decyzje implementacyjne, strategię testowania, strategię wdrażania i CI/CD, elementy poza zakresem, otwarte pytania.
- **NIE zawiera (celowo)**: wyborów frameworka, wyborów bazy danych, ścieżek plików, platformy wdrożeniowej. Otwartość stosu jest wiążąca — tylko `product_type` i `tech_preferences.language_family` ujmują intencję związaną ze stosem. Frameworki są zadaniem 10x-tech-stack-selector.

### Antywzorce wykrywane podczas kształtowania

- **Empty-CRUD**: logika biznesowa sprowadzająca się do „użytkownicy dodają i usuwają rekordy” bez reguły domenowej. `/10x-shape` nazywa to wprost i prosi o rzeczywisty kształt reguły (rekomendacja, priorytetyzacja, klasyfikacja, walidacja, ocenianie, workflow, obliczenie).
- **MVP-too-big**: szacowanie pierwszego przepływu przekracza ~1 tydzień pracy po godzinach albo wymaga > 4 odrębnych działań użytkownika przed uzyskaniem widocznej dla użytkownika wartości, albo wymaga wielu integracji przed osiągnięciem korzyści. Umiejętność wskazuje kosztowne elementy i oferuje konkretne sposoby zawężenia zakresu.

Oba są **miękkimi bramkami**: ostrzegają, ale pozwalają na nadpisanie. Nadpisania są zapisywane w checkpoint i ujawniane w `## Open Questions` PRD.

### Ścieżki fundamentów używane przez tę lekcję

- `context/foundation/shape-notes.md` — wynik `/10x-shape`
- `context/foundation/prd.md` (lub `prd-vN.md`) — wynik `/10x-prd`
- `context/foundation/lessons.md` — powtarzające się reguły i pułapki (szkielet utworzony przez `/10x-init`)
- `docs/reference/contract-surfaces.md` — rejestr nazw o krytycznym znaczeniu (szkielet utworzony przez `/10x-init`)

### Uniwersalny język

Dostarczone umiejętności nie zawierają odniesień do 10xDevs / kohort / certyfikacji. Mechanika (wyzwanie sokratejskie, odkrywanie szarych obszarów, ograniczanie zmęczenia rekomendowanymi odpowiedziami, miękka bramka jakości) to uniwersalne wskaźniki dobrze określonego projektu greenfield.

Umiejętności nie mogą zapisywać do `context/archive/`. Zarchiwizowane zmiany są niezmienne; jeśli rozpoznana docelowa ścieżka zaczyna się od `context/archive/`, przerwij z komunikatem: „Ta zmiana jest zarchiwizowana. Zamiast tego otwórz nową zmianę za pomocą `/10x-new`.”

<!-- END @przeprogramowani/10x-cli -->

## Konwencja commitów kursowych

- Każdy commit związany z pracą na kursie musi mieć przypisany numer modułu i lekcji.
- Przed wykonaniem commita agent ma sprawdzić, czy użytkownik podał numer lekcji w bieżącym zadaniu. Jeśli nie podał, agent musi go o ten numer zapytać i nie może commitować bez odpowiedzi.
- Commit oznaczaj tagiem w formacie `m<moduł>l<lekcja>`, na przykład `m1l1` dla modułu 1, lekcji 1.
- Tag lekcji wskazuje commit zawierający efekt pracy wykonanej w ramach tej lekcji.
- Wiadomość commita musi opisywać faktycznie wykonaną zmianę. Nie może składać się wyłącznie z numeru modułu lub lekcji — do tego służy tag.
