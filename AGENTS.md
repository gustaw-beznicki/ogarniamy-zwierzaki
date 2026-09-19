<!-- BEGIN @przeprogramowani/10x-cli -->

## Zestaw narzędzi AI 10xDevs — Moduł 1, Lekcja 2

Wybierz starter i stos dla PRD napisanego w Lekcji 1, z **łańcuchem stosu**:

```
(/10x-init  →  /10x-shape  →  /10x-prd)  →  /10x-tech-stack-selector  →  /10x-scaffold-adapter  →  /10x-bootstrapper
```

Łańcuch PRD jest dostarczany z Lekcji 1 (ponownie uwzględniony w tej lekcji, aby można było poprawić PRD w trakcie pracy). `/10x-tech-stack-selector` jest głównym tematem lekcji; przed bootstrapem `/10x-scaffold-adapter` tworzy aktualną instrukcję na podstawie oficjalnej dokumentacji i lokalnego CLI.

### Router zadań — Od czego zacząć

| Umiejętność | Użyj jej, gdy |
| --- | --- |
| **Wybór stosu (temat lekcji)** | |
| `/10x-tech-stack-selector` | Masz PRD w `context/foundation/prd.md` i musisz wybrać starter. Rozpoczyna się od wyraźnego wyboru (przyjmij zalecaną domyślną opcję dla swojej komórki `(product_type, language_family)` albo zaprojektuj własną), przechodzi przez zestaw pytań uzupełniających, gdy projektujesz własną opcję, stosuje cztery przyjazne agentom bramki jakości, analizuje rejestr starterów uwzględniający język i zapisuje `context/foundation/tech-stack.md`. Opcjonalny argument `[path-to-prd]` pozwala wskazać niestandardową lokalizację PRD (np. `/10x-tech-stack-selector @context/foundation/prd-v2.md`); bez niego umiejętność domyślnie używa `context/foundation/prd.md`. Użyj PO `/10x-prd`, PRZED `/10x-scaffold-adapter`. |
| `/10x-scaffold-adapter` | Masz `tech-stack.md` i potrzebujesz świeżej, projektowej instrukcji scaffoldingu. Skill sprawdza najnowszą stabilną oficjalną dokumentację oraz lokalne `--version`/`--help`, zapisuje niewykonywalny adapter Markdown dla każdego komponentu i przekazuje pracę do `/10x-bootstrapper`. |
| **W razie potrzeby uruchom ponownie wcześniejsze kroki** | |
| `/10x-init` / `/10x-shape` / `/10x-prd` | Zgrupowane, aby można było poprawić PRD w trakcie pracy. Jeśli `/10x-tech-stack-selector` ujawni lukę (np. Wymaganie funkcjonalne wymuszające funkcję, której nie obsługuje zalecany starter), uruchom ponownie `/10x-prd`, aby zmienić PRD przed wyborem stosu. |

### Jak łańcuch przekazuje pracę dalej

- `/10x-tech-stack-selector` odczytuje frontmatter `context/foundation/prd.md` (`product_type`, `target_scale`, `timeline_budget`) jako założenia wstępne. Jeśli PRD nie istnieje, odmawia działania z jednoliniowym przekierowaniem do `/10x-shape` — bez wbudowanego awaryjnego mini-PRD.
- Umiejętność zapisuje `context/foundation/tech-stack.md` z minimalnym frontmatterem. Dla architektury wielokomponentowej dodaje jawną listę `components`; dla pojedynczego komponentu zachowuje prosty kontrakt.
- `/10x-scaffold-adapter` traktuje rejestr jedynie jako wskazówkę odkrywania, a aktualny sposób użycia CLI potwierdza w oficjalnych źródłach i lokalnym `--help`.
- `/10x-bootstrapper` wykonuje osobne, zatwierdzone procesy opisane w adapterach, najpierw w stagingu. Nie wykonuje `cmd_template` z rejestru ani dowolnego `argv` przekazanego do ogólnego skryptu.

### Co przechwytuje tech-stack-selector (i czego NIE przechwytuje)

- **Przechwytywane**: wybór startera (w kształcie rejestru), rodzina językowa, menedżer pakietów (otwarty ciąg znaków dla ekosystemu — `pnpm`, `uv`, `bundle`, `cargo` itd.), wielkość zespołu, cel wdrożenia (pobrany z `deployment_defaults` wybranego startera), dostawca CI/CD + przepływ, pewność bootstrappera (`verified | first-class | best-effort`), obrana ścieżka (standard | custom), odpowiedzi samooceny (ścieżka custom), nadpisanie jakości (ustawiane, gdy użytkownik kontynuuje ze starterem, który nie przeszedł ≥1 bramki przyjaznej agentom), flagi funkcji (auth/payments/realtime/AI/background-jobs).
- **NIE przechwytywane (celowo)**: strategiczny plan testów, strategiczny plan wdrożenia, strategiczne decyzje implementacyjne. Są one dalszym etapem po wyborze stosu — zagadnieniem przyszłej technicznej roadmapy, jeszcze nieplanowanym. Tech-stack-selector odpowiada za *ukształtowane przez framework* wybory dotyczące testów/wdrożeń/CI, ponieważ są one nierozerwalne z wyborem stosu; odraczana jest *warstwa strategiczna* („stosujemy TDD na powierzchni X”, „środowisko podglądu dla każdego PR”).

### Początkowy wybór (kluczowy)

Pierwsze pytanie jest wyraźnym wyborem — nigdy niejawnym. Umiejętność od razu podaje zalecany starter dla Twojej komórki `(product_type, language_family)` i prosi o wyraźne potwierdzenie:

- **Ścieżka standardowa** — zaakceptuj zalecaną domyślną opcję. Umiejętność pomija audyt funkcji, profil zespołu, preferencje technologiczne i pytania dotyczące wariantu frameworka; zadaje jedynie pytania o wdrożenie, CI/CD i nazwę projektu. Przekazanie zapisuje `path_taken: standard` w `hints`.
- **Ścieżka custom** — zaprojektuj własną opcję. Umiejętność przechodzi przez pełny zestaw pytań uzupełniających (audyt funkcji, profil zespołu, preferencje technologiczne, wdrożenie, CI/CD, wariant frameworka), zagłębia się w pytanie o runner testów tylko wtedy, gdy wybrany starter pozostawia tę kwestię niejednoznaczną, i kończy 5-punktową samooceną gotowości (z lekcji przygotowawczej 4.1) przed zatwierdzeniem wyboru. Przekazanie zapisuje `path_taken: custom` i wypełnia `self_check_answers`.

Mapa zalecanych domyślnych opcji dla każdej komórki jest wielojęzykowa: web/JS i saas/JS oba → 10x-astro-starter (starter marki 10x prowadzi zawsze, gdy konkuruje w komórce JS); api/JS → hono; api/Python → fastapi; web/Python → django; web/Ruby → rails; api/Go → go; api/Rust → axum; mobile/Dart → flutter; desktop/Rust → tauri; itd. Komórki bez zweryfikowanej domyślnej opcji zawierają `<none>` i wymuszają ścieżkę custom.

### Bramki jakości (kryteria przyjazne agentom)

Każda karta startera zawiera cztery wartości logiczne, według których LLM filtruje:

1. **Typed** — jawne typy/schematy, na podstawie których agent może wnioskować bez uruchamiania programu.
2. **Convention-based** — silne opinie dotyczące układu, routingu, konfiguracji.
3. **Popular in training data** — oceniane *dla każdej rodziny językowej*, a nie globalnie (Django jest popularne w danych treningowych Pythona; Spring w Javie; itd.).
4. **Well-documented** — aktualna, przypięta do wersji dokumentacja, do której można podać link.

Kandydaci, którzy nie przejdą którejkolwiek bramki, są wykluczani ze zbioru rekomendacji bez dodatkowego pytania. Jeśli wyraźnie wskażesz nieprzechodzący starter jako swoją preferencję, umiejętność zakwestionuje ten wybór — przedstawiając najsilniejszą alternatywę spełniającą wyższe kryteria ORAZ ścieżkę kompensacji — i poprosi o potwierdzenie albo zmianę kierunku. Potwierdzenie zapisuje nadpisanie w przekazaniu; utworzenie reguł projektu pozostaje osobnym, jawnym zadaniem, a scaffold nie generuje ich automatycznie.

### Pewność bootstrappera

Każda rekomendacja wyświetla `bootstrapper_confidence` dosłownie — nigdy nie jest ona po cichu pomijana:

- **`verified`** — ścieżka była wcześniej sprawdzona end-to-end; adapter i tak ponownie weryfikuje bieżące CLI.
- **`first-class`** — wcześniej znano prawidłową ścieżkę CLI, ale bez pełnej weryfikacji end-to-end.
- **`best-effort`** — historyczne wsparcie jest ograniczone; adapter wymaga uważniejszego przeglądu.

To historyczny sygnał przed uruchomieniem `/10x-scaffold-adapter`; dopiero świeże dowody adaptera określają gotowość do wykonania.

### Ścieżki foundation używane przez tę lekcję

- `context/foundation/prd.md` — wejście (z Lekcji 1)
- `context/foundation/tech-stack.md` — wyjście (przekazanie w łańcuchu)
- `context/foundation/lessons.md` — powtarzające się zasady i pułapki
- `docs/reference/contract-surfaces.md` — rejestr kluczowych nazw

### Uniwersalny język

Dostarczona umiejętność nie zawiera odniesień do 10xDevs / kohort / certyfikacji. Rejestr zalecanych domyślnych opcji jest wielojęzykowy (JS, Python, Ruby, Java, Go, Rust, PHP, .NET, Dart), a kohortowy `10x-astro-starter` jest jedną kartą w komórce JS+web — nie „tą” zalecaną ścieżką dla wszystkich.

Umiejętności nie mogą zapisywać do `context/archive/`. Zarchiwizowane zmiany są niezmienne; jeśli rozpoznana ścieżka docelowa zaczyna się od `context/archive/`, przerwij z komunikatem: „Ta zmiana jest zarchiwizowana. Zamiast tego otwórz nową zmianę za pomocą `/10x-new`.”

<!-- END @przeprogramowani/10x-cli -->

## Konwencja commitów kursowych

- Każdy commit związany z pracą na kursie musi mieć przypisany numer modułu i lekcji.
- Przed wykonaniem commita agent ma sprawdzić, czy użytkownik podał numer lekcji w bieżącym zadaniu. Jeśli nie podał, agent musi go o ten numer zapytać i nie może commitować bez odpowiedzi.
- Commit oznaczaj tagiem w formacie `m<moduł>l<lekcja>`, na przykład `m1l1` dla modułu 1, lekcji 1.
- Tag lekcji wskazuje commit zawierający efekt pracy wykonanej w ramach tej lekcji.
- Wiadomość commita musi opisywać faktycznie wykonaną zmianę. Nie może składać się wyłącznie z numeru modułu lub lekcji — do tego służy tag.
