---
name: 10x-bootstrapper
description: >
  Scaffold a project into the current working directory after the tech stack
  is picked. Reads context/foundation/tech-stack.md, runs the chosen starter's
  CLI with a strict conflict policy that always preserves context/, and writes
  a verification log. Use when the user says "bootstrap the project",
  "scaffold the app", "set up the codebase", "let's start the project".
  Use AFTER /10x-tech-stack-selector.
---
# Bootstrapper: od stosu technologicznego do projektu ze szkieletem

Ta umiejętność jest końcowym ogniwem sekwencji bootstrap (`/10x-shape → /10x-prd → /10x-tech-stack-selector → 10x-bootstrapper`). Jej jedynym zadaniem jest przekształcenie pisanego przekazania stosu technologicznego w projekt ze szkieletem w bieżącym katalogu roboczym, z ustaleniami weryfikacyjnymi zapisanymi do wglądu użytkownika.

Umiejętność jest **konsumentem rejestru**, a nie jego właścicielem. Rejestr starterów znajduje się w `/10x-tech-stack-selector` (`/skills/10x-tech-stack-selector/references/starter-registry.yaml`); bootstrapper wyszukuje wybraną kartę według `starter_id`, podstawia jej `cmd_template` i kieruje do właściwej strategii cwd. Walidator CI (`scripts/validate-starter-registry-sync.mjs`) zapobiega odwoływaniu się przez bootstrapper do `starter_id`, którego nie ma w tym rejestrze.

v1 działa **wyłącznie w trybie łańcuchowym**. Bez `context/foundation/tech-stack.md` umiejętność odmawia i przekierowuje do `/10x-tech-stack-selector`. Nie ma wbudowanego mini-przekazania, trybu samodzielnego ani awaryjnego rozwiązania AI-as-bridge dla nieznanych stosów. v1 również **nie** generuje `AGENTS.md` / pliku konfiguracji AI projektu (AGENTS.md) — odpowiedzialność za to należy do przyszłej umiejętności M1L4.

## Kiedy uruchamiać

Używaj, gdy istnieje `context/foundation/tech-stack.md`, a użytkownik jest gotowy do utworzenia szkieletu. Frazy wyzwalające: „bootstrap the project”, „scaffold the app”, „set up the codebase”, „let's start the project”, „spin up the repo” lub dowolna naturalna kontynuacja uruchomienia `/10x-tech-stack-selector`, które właśnie zapisało przekazanie.

Warunkiem wstępnym jest pojedynczy plik na dysku: `context/foundation/tech-stack.md`. Umiejętność nigdy nie korzysta awaryjnie z historii rozmowy, nigdy nie uruchamia ponownie wywiadu dotyczącego stosu technologicznego i nigdy nie akceptuje stosu nazwanego inline.

## Kiedy pominąć

Pomiń, gdy:

- Użytkownik jest w trakcie implementacji w istniejącej bazie kodu i prosi o dodanie pojedynczej biblioteki lub zastąpienie pojedynczej zależności — to obszar `/10x-frame`, a nie bootstrap.
- Użytkownik nazywa stos spoza rejestru tech-stack-selector — przekieruj do `/10x-tech-stack-selector` (jest właścicielem rejestru; jeśli brakuje startera, tam trafia ten przypadek).
- Brakuje `context/foundation/tech-stack.md` — sprawdzenie warunku wstępnego w Kroku 0 obsługuje to poprzez jawne przekierowanie.

## Wymagane dane wejściowe

1. `context/foundation/tech-stack.md` — przekazanie zapisane przez `/10x-tech-stack-selector`. Kontrakt: zobacz `references/handoff-consumer.md` (który wskazuje `/10x-tech-stack-selector/references/handoff-schema.md` jako autorytatywny schemat).
2. Wybrana karta z `/skills/10x-tech-stack-selector/references/starter-registry.yaml`. Rozwiązywana przez wyszukiwanie `starter_id`. Zawiera `cmd_template`, `language_family`, `bootstrapper_confidence`, `toolchain.package_manager`, `deployment_defaults`.
3. `references/bootstrapper-config.yaml` — nadpisania `cwd_strategy` po stronie bootstrapper dla poszczególnych starterów + wyszukiwanie `language_family → audit_command`. Dołączone do umiejętności.
4. `references/handoff-consumer.md` — dołączone. Ładowane w Kroku 0.
5. `references/refusal-protocol.md` — dołączone. Ładowane, gdy wystąpi dowolny warunek odmowy.
6. `references/pre-scaffold-verification.md` — dołączone. Ładowane w Kroku 1.
7. `references/scaffold-merge.md` — dołączone. Ładowane w Kroku 2.
8. `references/post-scaffold-verification.md` — dołączone. Ładowane w Kroku 3.
9. `references/verification-log-schema.md` — dołączone. Ładowane w Kroku 4.

## Początkowa odpowiedź

Gdy ta umiejętność zostanie wywołana:

1. **Jeśli podano argument ścieżki** (np. `/10x-bootstrapper @context/foundation/tech-stack-v2.md` lub `/10x-bootstrapper path/to/tech-stack.md`), usuń początkowy `@`, jeśli występuje, i użyj ścieżki dosłownie jako lokalizacji przekazania dla tego uruchomienia.
2. **Jeśli nie podano argumentu**, domyślnie ustaw ścieżkę przekazania na `context/foundation/tech-stack.md`.

Przenoś rozwiązaną ścieżkę przez Krok 0; pozostała część przepływu pracy działa na niej jako `<handoff-path>`.

## Przepływ pracy

### Krok 0 — Warunek wstępny przekazania

Sprawdź warunek wstępny przekazania względem rozwiązanej ścieżki:

```bash
test -f "<handoff-path>"
```

**Jeśli brak**, wykonaj dokładnie to i ZATRZYMAJ się — bez awaryjnego wywiadu, bez inline mini-przekazania, bez czytania rozmowy w poszukiwaniu zastępczego wyboru stosu:

```bash
echo -n "/10x-tech-stack-selector" | pbcopy 2>/dev/null || echo -n "/10x-tech-stack-selector" | clip.exe 2>/dev/null || echo -n "/10x-tech-stack-selector" | xclip -selection clipboard 2>/dev/null || true
```

```powershell
# PowerShell (Windows)
Set-Clipboard "/10x-tech-stack-selector"
```

Wypisz dosłownie (podstaw rozwiązaną ścieżkę; jeśli użyto wartości domyślnej, jest to `context/foundation/tech-stack.md`):

```
Bootstrapper requires a tech-stack hand-off at `<handoff-path>`. Run `/10x-tech-stack-selector` first, then re-invoke.
```

Następnie ZATRZYMAJ się. Kontekst rozmowy **nie** jest rozwiązaniem awaryjnym — nawet jeśli wybór stosu został omówiony wcześniej na czacie, umiejętność wymaga pliku na dysku. Pełny zestaw warunków odmowy i ciągów schowka znajdziesz w `references/refusal-protocol.md`.

**Jeśli istnieje**, przeczytaj go W CAŁOŚCI (bez `limit`/`offset`) i kontynuuj. Sparsuj frontmatter zgodnie z `references/handoff-consumer.md` i rozwiąż wybraną kartę przez wyszukiwanie `starter_id` względem `/skills/10x-tech-stack-selector/references/starter-registry.yaml`. Jeśli wyszukiwanie się nie powiedzie, uruchom odmowę z powodu rozjazdu rejestru z `references/refusal-protocol.md` i ZATRZYMAJ się.

Powtórz użytkownikowi wykorzystane pola jako podsumowanie do potwierdzenia lub korekty:

```
Hand-off received:
  Starter:        <starter_id> — <name>
  Project name:   <project_name>
  Package manager:<package_manager | "(card default)" if omitted>
  Language:       <hints.language_family>
  Confidence:     <hints.bootstrapper_confidence>
  Path taken:     <hints.path_taken>
  Deployment:     <hints.deployment_target>
  Feature flags:  <comma list of has_* set to true, or "none">
```

Zapytaj użytkownika: „Proceed with this hand-off, or correct something first?”

Opcje:
- **Proceed (Recommended)** — Kontynuuj z przekazaniem w odczytanej postaci.
- **Correct a value** — Zapytaj, które pole nadpisać dla tego uruchomienia; plik na dysku pozostaje bez zmian.
- **Stop — fix the hand-off first** — Zakończ. Uruchom ponownie /10x-tech-stack-selector, aby zaktualizować tech-stack.md, a następnie wywołaj ponownie.

Jeśli wybrano „Correct a value”: zapytaj, które pole, przechwyć nadpisanie i kontynuuj z nadpisaniem zastosowanym wyłącznie dla tej sesji. Następnie uruchom zabezpieczenie zapełnionego cwd z `references/refusal-protocol.md` (ostrzeż i poproś o potwierdzenie, jeśli cwd zawiera już odcisk charakterystyczny dla szkieletu, taki jak `package.json`, `Cargo.toml`, `Gemfile`, `pyproject.toml` itd.).

### Krok 1 — Weryfikacja przed utworzeniem szkieletu

Przed uruchomieniem CLI startera wykonaj lekkie sprawdzenie aktualności opisane w `references/pre-scaffold-verification.md`. Przeczytaj teraz tę referencję. Ten etap jest tylko do odczytu — bez klonowania, bez instalacji, bez zmian w systemie plików — oraz ma charakter edukacyjny, a nie blokujący: każde ustalenie to WARN-AND-CONTINUE.

Sekwencja:

1. Na podstawie wybranej karty wyprowadź nazwę pakietu npm z `cmd_template`, jeśli `hints.language_family == js` i szablon wywołuje CLI `create-*` (np. `npm create next-app` → `create-next-app`, `npm create astro` → `create-astro`, `npm create vite` → `create-vite`). Jeśli szablon zaczyna się od `git clone`, pomiń krok npm.
2. Jeśli wyprowadzono nazwę pakietu, uruchom `npm view <package> version` i `npm view <package> time.modified`.
3. Na podstawie wybranej karty sparsuj `docs_url`. Jeśli wskazuje na `github.com/<owner>/<repo>`, uruchom `gh api repos/<owner>/<repo> --jq '.pushed_at'`.
4. Oblicz istotność według progów z `pre-scaffold-verification.md` (fresh / aged / stale).
5. Wypisz w rozmowie jedną linię podsumowania. Poprzedź ją jednolinijkowym ostrzeżeniem „Heads-up”, jeśli którykolwiek sygnał jest stale. Nigdy nie blokuj — niezależnie od tego przejdź do Kroku 2.
6. Przygotuj w pamięci rekordu weryfikacyjnego rozwiązaną nazwę pakietu (jeśli istnieje), adres URL repozytorium GitHub (jeśli istnieje), oba znaczniki czasu i oba poziomy istotności. Krok 4 zapisuje ten rekord na dysku.

Jeśli wywołanie sieciowe się nie powiedzie, zapisz błąd w logu i kontynuuj z częściowym rekordem — zobacz „Failure mode” w referencji.

Wyszukaj teraz `cwd_strategy` dla wybranego `starter_id` w `references/bootstrapper-config.yaml` (domyślnie `subdir-then-move`, jeśli id nie jest wymienione). Krok 2 go potrzebuje. Jednocześnie wyszukaj `audit_commands[<hints.language_family>]` z tego samego pliku i przygotuj je dla Kroku 3 (wartość `null` oznacza, że Krok 3 pominie audyt i odnotuje pominięcie w logu).

### Krok 2 — Utworzenie szkieletu i scalenie

Przeczytaj teraz `references/scaffold-merge.md`. Zawiera pełny mechanizm trzech strategii cwd, macierz konfliktów, reguły podstawiania oraz ścieżkę HARD-STOP w przypadku błędu CLI.

Sekwencja:

1. Rozwiąż `cmd_template` z wybranej karty. Podstaw `{name}` i `{pm}` zgodnie ze strategią objętą zakresem (zobacz `scaffold-merge.md` § Substitution rules). Zapasową wartością `{pm}` jest `toolchain.package_manager` karty, jeśli przekazanie pomija to pole.
2. Rozgałęź według `cwd_strategy` (rozwiązanej w Kroku 1 z `bootstrapper-config.yaml`, domyślnie `subdir-then-move`):
   - **`subdir-then-move`** — uruchom rozwiązaną komendę z `{name}=.bootstrap-scaffold`. Przy kodzie wyjścia 0 zastosuj macierz konfliktów, przenosząc pliki w górę do cwd, a następnie usuń `.bootstrap-scaffold/`.
   - **`native-cwd`** — uruchom rozwiązaną komendę z `{name}=.` bezpośrednio w cwd. Bez kroku scalania. Przed uruchomieniem: wypisz pliki, których CLI za chwilę dotknie, i pokaż je w rozmowie przed exec.
   - **`git-clone`** — uruchom rozwiązaną komendę z `{name}=.bootstrap-scaffold`. Przy kodzie wyjścia 0 usuń `.bootstrap-scaffold/.git/` przed zastosowaniem macierzy konfliktów i przeniesieniem plików w górę. Następnie usuń `.bootstrap-scaffold/`.
3. Niezależnie od wyniku przechwyć stdout, stderr i kod wyjścia do rekordu weryfikacyjnego w pamięci.
4. **Błąd CLI to HARD-STOP.** Jeśli kod wyjścia jest różny od zera, uruchom ścieżkę obsługi błędu CLI w `scaffold-merge.md` § CLI failure handling: pozostaw `.bootstrap-scaffold/` na miejscu, nie stosuj macierzy konfliktów, zapisz częściowy `verification.md` z `phase_3_status: failed`, ustaw schowek na `/10x-bootstrapper`, wypisz podsumowanie błędu i ZATRZYMAJ się. Nie przechodź do Kroku 3.
5. Przy kodzie wyjścia 0 wypisz jedną linię podsumowania zgodnie z formatem w `scaffold-merge.md` § Surfacing the result. Przygotuj w pamięci rekordu weryfikacyjnego log przenoszenia plik po pliku. Przejdź do Kroku 3.

Zabezpieczenie zapełnionego cwd z Kroku 0 (`refusal-protocol.md` § (d)) zostało już uruchomione przed tym krokiem. Macierz konfliktów stanowi siatkę bezpieczeństwa: istniejące pliki stają się sąsiadującymi plikami `.scaffold`, `context/` jest zawsze zachowywany, a `.gitignore` jest scalany przez dopisanie.

Rozmawiając z użytkownikiem, tłumacz nazwy strategii na prosty język („utwórz szkielet w katalogu tymczasowym, a następnie przenieś pliki wyżej”, „utwórz szkielet bezpośrednio w bieżącym katalogu”, „sklonuj repozytorium startera bez zachowywania jego historii git”) zamiast powtarzać dosłownie wewnętrzne etykiety.

### Krok 3 — Weryfikacja po utworzeniu szkieletu

Przeczytaj teraz `references/post-scaffold-verification.md`. Ten etap uruchamia komendę audytu rozwiązaną w Kroku 1 (`audit_commands[<hints.language_family>]` z `bootstrapper-config.yaml`) i przypisuje ustaleniom poziomy istotności.

Sekwencja:

1. Jeśli rozwiązana komenda audytu to `null`, pomiń audyt i przygotuj w rekordzie weryfikacyjnym ustrukturyzowaną notatkę „no built-in audit tool for <language_family>”. Wypisz linię pominięcia zgodnie z formatem Output w referencji. Przejdź do Kroku 4.
2. W przeciwnym razie uruchom rozwiązaną komendę z cwd (lub odpowiedniego katalogu instalacji zależności, jeśli szkielet tak ustrukturyzował projekt). Przechwyć stdout, stderr i kod wyjścia. Kod wyjścia narzędzia audytu ma wyłącznie charakter informacyjny — bootstrapper NIE zatrzymuje się przy niezerowym kodzie wyjścia audytu.
3. Sparsuj dane wyjściowe zgodnie z blokiem wywołania dla danego ekosystemu w referencji. Przypisz ustalenia do poziomów CRITICAL / HIGH / MODERATE / LOW.
4. Jeśli narzędzie obsługuje rozróżnienie bezpośrednie vs przechodnie, oblicz ten podział.
5. Wypisz w rozmowie jedną linię podsumowania zgodnie z formatem Output w referencji. Liczby CRITICAL i HIGH pokaż inline; MODERATE i LOW tylko w logu.
6. Przygotuj w pamięci rekordu weryfikacyjnego pełne zestawienie (surowe dane wyjściowe, sparsowane liczby, szczegóły każdego ustalenia, podział bezpośrednie/przechodnie).

Niedostępne narzędzie, błąd sieci lub błąd parsowania: WARN-AND-CONTINUE zgodnie z blokiem Failure mode w referencji. Obecne ustalenia CRITICAL: WARN-AND-CONTINUE — bootstrapper informuje, decyzję podejmuje użytkownik.

### Krok 4 — Zapisz verification.md i zakończ

Przeczytaj teraz `references/verification-log-schema.md`. Ten krok zapisuje ślad audytowy uruchomienia na dysku i wypisuje końcowe podsumowanie.

Sekwencja:

1. Upewnij się, że istnieje `context/changes/bootstrap-verification/`. Utwórz katalog, jeśli go nie ma (bez `change.md` — folder zawiera wyłącznie log).
2. Jeśli `context/changes/bootstrap-verification/verification.md` już istnieje, uruchom zabezpieczenie WARN-AND-CONFIRM z `references/refusal-protocol.md` § (e). Przy „Overwrite” kontynuuj. Przy „Save as verification-v2.md” zwiększ numer do następnego dostępnego slotu `verification-vN.md`. Przy „Abort” zatrzymaj się bez zapisu.
3. Ułóż treść pliku zgodnie z `references/verification-log-schema.md`: frontmatter (z `phase_3_status: ok` dla normalnych uruchomień, `failed` dla przypadku częściowego logu HARD-STOP), następnie `## Hand-off`, `## Pre-scaffold verification`, `## Scaffold log`, `## Post-scaffold audit`, `## Hints recorded but not acted on`, `## Next steps`. Sekcja `Hints recorded but not acted on` pobiera każdą wskazówkę z flag `handoff-consumer.md` przekazania jako „surfaces but does not act on in v1”.
4. Zapisz plik. Jeśli zapis się nie powiedzie (błąd systemu plików, odmowa uprawnień), awaryjnie wypisz pełną treść na czacie zgodnie z blokiem failure-mode schematu.
5. Wypisz w rozmowie końcowe podsumowanie:

   ```
   Bootstrapped <starter_id> into the current directory. Verification log: context/changes/bootstrap-verification/verification.md.

   Pre-scaffold: <one-line recency summary>.
   Scaffold:    <one-line scaffold summary>.
   Audit:       <one-line audit summary>.

   Next: a future skill will set up agent context (the project's AI configuration file (AGENTS.md), AGENTS.md). For now, your project is scaffolded and verified — happy hacking.
   ```

6. Zakończ. Nie ustawiaj schowka do ponowienia przy pomyślnym uruchomieniu; łańcuch jest kompletny dla v1.

W przypadku częściowego logu HARD-STOP (błąd CLI w Kroku 2) Krok 4 nadal działa, ale ze skróconą strukturą treści w schemacie (sekcja `Audit not run`, `phase_3_status: failed`). Schowek jest ustawiany na `/10x-bootstrapper` do ponowienia przez ścieżkę błędu Kroku 2, a nie przez ten krok.

## Wynik

Co umiejętność tworzy zewnętrznie:

- **Pliki projektu ze szkieletem w cwd** — zapisane przez CLI startera, z sąsiadującymi plikami `.scaffold` tam, gdzie polityka konfliktów wykryła kolizję. `context/` w cwd jest zachowywany dosłownie.
- **`context/changes/bootstrap-verification/verification.md`** — ślad audytowy uruchomienia. Schemat w `references/verification-log-schema.md`. Jeden plik na uruchomienie; ponowne uruchomienia nadpisują go (z zabezpieczeniem WARN-AND-CONFIRM).
- **Podsumowania rozmowy na każdym kroku** — echo do potwierdzenia lub korekty z Kroku 0, podsumowanie aktualności z Kroku 1, podsumowanie tworzenia szkieletu z Kroku 2 (z uwagami o sąsiadujących plikach `.scaffold` i obsłudze `.gitignore`), podsumowanie audytu z Kroku 3, końcowe podsumowanie z Kroku 4 ze wskazaniem kolejnych kroków.
- **Wskaźnik w schowku tylko na ścieżkach błędów** — `/10x-tech-stack-selector` dla odmów z powodu brakującego przekazania i rozjazdu rejestru, `/10x-bootstrapper` dla ponowienia HARD-STOP błędu CLI z Kroku 2. Przy pomyślnym uruchomieniu schowek nie jest ustawiany.

Czego umiejętność NIE tworzy w v1:

- **`AGENTS.md` / plik konfiguracji AI projektu (AGENTS.md)** — odroczone do przyszłej umiejętności M1L4 („Memory Architecture”).
- **Pliki przepływu pracy CI** (`.github/workflows/ci.yml` itd.) — odroczone do tej samej przyszłej umiejętności.
- **`git init`** ani żadnej historii git — bootstrapper zakłada, że użytkownik zarządza własnym repozytorium. Strategia `git-clone` jawnie usuwa sklonowane `.git/` przed przeniesieniem plików wyżej, aby historia upstreamowego startera nie przeniknęła.
- **Automatyczne naprawy / automatyczne łatki dla ustaleń audytu** — bootstrapper informuje; decyzję podejmuje użytkownik.

## Referencje

- `references/handoff-consumer.md` — które klucze frontmatter przekazania bootstrapper wykorzystuje, pokazuje i ignoruje.
- `references/refusal-protocol.md` — warunki odmowy, teksty i ciągi schowka.
- `references/bootstrapper-config.yaml` — nadpisania `cwd_strategy` dla poszczególnych starterów + mapa `language_family → audit_command`.
- `references/pre-scaffold-verification.md` — lekkie sprawdzenie aktualności.
- `references/scaffold-merge.md` — mechanizm `.bootstrap-scaffold/`, trzy strategie cwd, macierz konfliktów.
- `references/post-scaffold-verification.md` — dyspozycja audytu według języka + poziomowanie istotności.
- `references/verification-log-schema.md` — struktura `context/changes/bootstrap-verification/verification.md`.

## Krytyczne zabezpieczenia

1. **Przekazanie jest warunkiem wstępnym, a nie rozwiązaniem awaryjnym.** Bez inline mini-przekazania, bez czytania historii rozmowy w poszukiwaniu zastępczych pól. Plik na dysku jest kontraktem.

2. **Bootstrapper konsumuje rejestr; nie jest jego właścicielem.** Kanoniczny rejestr starterów znajduje się w `/10x-tech-stack-selector`. Rozjazd między `starter_id` wskazywanymi przez bootstrapper a rejestrem jest błędem CI (`scripts/validate-starter-registry-sync.mjs`).

3. **`context/` jest zawsze zachowywany.** Polityka konfliktów jest rygorystyczna: nic pod `context/` w cwd nigdy nie jest nadpisywane przez szkielet. Pełną macierz konfliktów znajdziesz w `references/scaffold-merge.md` (Faza 3).

4. **Błąd CLI to HARD-STOP.** Niezerowy kod wyjścia w Kroku 2 zatrzymuje umiejętność, pozostawia `.bootstrap-scaffold/` na miejscu do inspekcji i zapisuje częściowy log weryfikacyjny. Wszystkie pozostałe fazy używają WARN-AND-CONTINUE — ustalenia weryfikacyjne mają charakter edukacyjny, a nie blokujący.

5. **v1 nie generuje `AGENTS.md` / pliku konfiguracji AI projektu (AGENTS.md).** Ta praca przechodzi do przyszłej umiejętności M1L4 („Memory Architecture”). v1 pokazuje wartości wskazówek, takie jak `bootstrapper_confidence: best-effort` i `quality_override: true`, w podsumowaniu rozmowy, ale nie podejmuje żadnego działania kompensującego.

6. **Wewnętrzne etykiety umiejętności pozostają wewnętrzne.** Rozmawiając z użytkownikiem, nigdy nie odwołuj się do numerów kroków (`Step 0`, `Step 2`), nazw strategii dosłownie (`subdir-then-move`, `native-cwd`, `git-clone`) bez kontekstu ani wewnętrznych ścieżek pól (`hints.deployment_target`). Tłumacz na prosty język: „the scaffold step”, „your deployment target”, „how the CLI scaffolds in your current directory”, „by cloning a starter repo”.