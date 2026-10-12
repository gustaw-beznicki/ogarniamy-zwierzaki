---
name: 10x-configure-hook
description: >
  Turn the quality gates from context/foundation/test-plan.md into working
  agent hooks and prove them before handing off. Detects the harness from the
  repo (your AI coding assistant, Cursor, Codex, GitHub Copilot), audits existing hook
  config, maps each gate to a moment (per edit, end of turn, commit, push,
  CI), generates config and scripts in that harness's signal protocol, and
  runs every script against a deliberately broken file. Trigger phrases:
  "configure hooks", "set up agent hooks", "lint after every edit", "hook
  fires but the agent ignores it", "skonfiguruj hooki", "hook po edycji",
  "agent nie widzi błędów z hooka". Use AFTER /10x-test-plan (M3L1). Does
  not write git pre-commit/pre-push config — it only recommends it.
---
# 10x Configure Hook — bramki jakości jako hooki agenta, sprawdzone

Ta umiejętność przekształca bramki, o których projekt już zdecydował, w hooki, które agent faktycznie odbiera. Hook jest użyteczny tylko wtedy, gdy jego komunikat dociera do agenta kanałem odczytywanym przez harness dla danego zdarzenia. Większość uszkodzonych hooków działa poprawnie i wypisuje do kanału, którego nikt nie nasłuchuje — dlatego ta umiejętność nigdy nie przekazuje hooka, którego nie uruchomiła.

Pracuj w tej kolejności. Każdy krok ma wynik, który wykorzystuje następny krok; raport końcowy (Krok 8) je zbiera.

## Zasady krytyczne

1. **Przeprowadź wywiad z repozytorium, nie z użytkownikiem.** Wszystko widoczne na dysku — harness, toolchain, istniejące hooki, plan testów — jest odczytywane, nie pytane. Pytaj tylko o to, na co repozytorium nie potrafi odpowiedzieć.
2. **Kanał sygnału zależy od harnessa i zdarzenia.** Nigdy nie zakładaj, że kody wyjścia, stderr, stdout lub pole JSON znaczą to samo w dwóch harnessach. Fakty są w `references/<harness>.md`; korzystaj z nich, a gdy możesz przeglądać, preferuj aktualną dokumentację, jeśli jest z nimi sprzeczna.
3. **Per-edit oznacza edytowany plik.** Hook per-edit odczytuje ścieżkę edytowanego pliku z payloadu i sprawdza tylko ten plik. Kontrole całego projektu przenieś na koniec tury lub później.
4. **Udowodnij, a potem przekaż.** Każdy wygenerowany skrypt jest uruchamiany z przykładowym payloadem na celowo uszkodzonym pliku i na czystym, zanim zostanie napisany raport.
5. **Scalaj, nigdy nie nadpisuj.** Istniejąca konfiguracja hooków jest audytowana i scalana; diff jest pokazywany przed zapisem.

---

## Krok 1 — Warunki wstępne: przeczytaj plan testów

Poszukaj `context/foundation/test-plan.md`. Znajdź sekcję bramek jakości **po tytule** („Quality Gates” lub bliski wariant) — nigdy po numerze sekcji; numeracja zmienia się między wersjami schematu.

Z tabeli bramek zapisz po jednym wierszu na bramkę: nazwę bramki, miejsce jej uruchamiania („local”, „local (agent loop)”, „CI on PR”…), wymagalność („required”, „required after §3 Phase N”, „recommended after…”, „optional”) oraz to, co wykrywa. Wiersze dotyczące pętli agenta — „post-edit hook”, „lint + typecheck”, „unit + integration” — są głównym wejściem.

Zapisz także **jawne odroczenia**: każdą bramkę lub wiersz oznaczony „deferred”, „not v1”, „later”, „out of scope” lub podobnie, wraz z cytowanym sformułowaniem i powodem podanym przez plan. Odroczenia są decyzjami, nie lukami.

Jeśli pliku brakuje albo nie ma sekcji bramek jakości: wyprowadź kandydatów na bramki z własnych skryptów repozytorium (Krok 3) i podaj w raporcie „No test plan found — gates derived from the toolchain”. Zasugeruj `/10x-test-plan` dla rzeczywistej decyzji o bramkach, ale nie zatrzymuj się.

## Krok 2 — Wykryj harness(y)

Każdy plik referencyjny zaczyna się od sekcji **Detection signals**. Przeczytaj te sekcje dla każdego pliku w `references/` i dopasuj je do repozytorium. Typowe sygnały:

- Twój asystent kodowania AI: katalog konfiguracji narzędzia AI (`settings.json`, `settings.local.json`, `skills/`), plik konfiguracji AI projektu (AGENTS.md).
- Cursor: `.cursor/` (`hooks.json`, `rules/`, `skills/`), `.cursor/rules/10x-course.mdc`.
- Codex: `.codex/` (`hooks.json`, `config.toml`), `.agents/skills/`, `AGENTS.md`.
- GitHub Copilot: `.github/hooks/`, `.github/skills/`, `.github/copilot-instructions.md`.
- Manifest 10x-cli: katalog konfiguracji narzędzia AI, `.cursor/`, `.github/` lub `.agents/.10x-cli-manifest.json` — jego pole `"tool"` określa harness, dla którego zainstalowano artefakty kursu.

Katalogi konfiguracji specyficzne dla harnessa i manifest są silnymi sygnałami. `AGENTS.md` i `.agents/skills/` są słabe: wiele harnessów odczytuje `AGENTS.md`, a 10x-cli zapisuje również `.agents/skills/` w repozytoriach twojego asystenta kodowania AI.

**Rozstrzygnięcie remisu:** katalog, z którego załadowano tę umiejętność, określa harness, który konfigurujesz. Wymień każdy inny znaleziony harness wraz ze ścieżką dowodu; zapytaj raz — przedstawiając wykryte opcje — tylko wtedy, gdy potrzebny jest także inny harness lub ma już konfigurację hooków.

## Krok 3 — Wykryj toolchain

Czytaj manifest i konfigurację, nie polegaj na pamięci. Dla każdej bramki zapisz dokładne polecenie używane przez to repozytorium:

- **Lint / format** — linter projektu oraz to, czy przyjmuje ścieżkę pojedynczego pliku (ESLint, Biome, Ruff, golangci-lint, RuboCop…). Preferuj lokalny binarny program (`npx`, `pnpm exec`, `uv run`, …), którego repozytorium już używa. Wyprowadź sprawdzane rozszerzenia plików z konfiguracji lintowania, a nie z przykładów (np. dodaj `.astro`, gdy skonfigurowano `eslint-plugin-astro`, analogicznie `.vue` / `.svelte`). **Brak lintera:** kontrolą per-edit są diagnostyki typecheckera przefiltrowane do edytowanego pliku oraz `node --check` dla zwykłego JavaScript (`check-edited-file.sh` w referencji asystenta kodowania AI); zarekomenduj linter w raporcie, nie instaluj go.
- **Typecheck** — polecenie dla całego projektu (`tsc --noEmit`, `astro check`, `mypy`, `cargo check`, …) oraz przybliżony czas jego działania. Frameworki z generowanymi typami wymagają najpierw kroku synchronizacji (`tsc` kończy się błędem dla modułów `astro:*`, dopóki `astro sync` nie wygeneruje `.astro/`; `astro check` sam wykonuje synchronizację, ale jest wolniejszy). Generowane katalogi są zwykle ignorowane przez git, więc synchronizacja naprawiająca bazowy stan należy do skryptu końca tury, a nie do jednorazowego polecenia.
- **Baseline** — uruchom każdą kontrolę końca tury raz na niezmodyfikowanym drzewie. Jeśli już jest czerwono (brak wygenerowanych typów, zagnieżdżony pakiet z niezainstalowanymi zależnościami wciągnięty przez główne `**/*` include, …): napraw warunek wstępny (wygeneruj typy), ogranicz zakres polecenia (tsconfig projektu, filtr ścieżek, diagnostyki ograniczone do zmienionych plików) albo zgłoś to jako blokadę. Nigdy nie przekazuj hooka Stop, który blokuje na czystym drzewie.
  **Czerwono z powodu hosta, nie kodu** (test przypięty do dokładnej wersji narzędzia, test trybu pliku zależny od `umask`, współdzielona lokalna usługa): skrypt wykrywa ten warunek, pomija tylko dotkniętą część i informuje o tym w swoim wyjściu (wzorzec `SKIPPED` w referencji). Napraw środowisko poza hookiem, jeśli możesz; ciche pominięcie jest nigdy nieakceptowalne.
- **Testy** — runner oraz to, czy ma tryb testów powiązanych (`vitest related <file> --run`, `jest --findRelatedTests <file> --passWithNoTests`, `pytest` ze ścieżką, …). Odnotuj, co runner robi, gdy nie ma pasującego testu (exit 0 lub nie). **Brak trybu powiązanego** (`node --test`, `bun test`): wybierz pliki testowe importujące edytowany moduł za pomocą `git grep` po plikach testowych (`related-tests-by-import.sh` w referencji asystenta kodowania AI); jeden poziom importu, przegląd końca tury i bramka commitu pokrywają resztę. Zmierz raz cały zestaw testów jednostkowych: jeśli kończy się w około 30 s lub mniej, skrypt końca tury uruchamia wszystkie (wykrywa wtedy również czerwony test w module, który agent jedynie importował); w przeciwnym razie uruchamia testy powiązane ze zmienionymi plikami.
- **Menedżer git-hooków** — Husky, lint-staged, Lefthook, pre-commit lub brak. Istniejące hooki git nigdy nie są modyfikowane przez tę umiejętność.
- **Wymagania wstępne skryptu** — `jq` dla skryptów bash albo Node/Python, jeśli skrypt nie-bash jest bezpieczniejszy (Windows bez Git Bash).

## Krok 4 — Przypisz bramki do momentów; przeprowadź audyt istniejących elementów

**Momenty**, najpierw najtańszy sygnał:

| Moment | Należy tutaj | Dociera do agenta |
| --- | --- | --- |
| Per edit | Lint/format edytowanego pliku; powiązane testy, gdy kończą się w sekundy | Tak |
| End of turn | Lint + testy dla każdego pliku zmienionego w tej turze; typecheck całego projektu | Tak |
| Pre-commit | Lint + testy na plikach staged (wychwytuje edycje wykonane bez agenta) | Nie |
| Pre-push | Cięższe zestawy, lokalne e2e | Nie |
| CI | Integracja, współdzielony stan, infrastruktura, której nie masz lokalnie | Nie |

Heurystyka szybkości: **im wolniejsza kontrola, tym rzadszy moment.** Kilka sekund pasuje do per edit; dziesiątki sekund lub zakres całego projektu należą do końca tury; minuty należą do commitu, push lub CI.

Moment per-edit widzi tylko narzędzia edycji harnessa. Agenci przepisują też pliki poleceniami powłoki (`cat > file <<EOF`, `sed -i`), których żaden hook per-edit nie otrzymuje. Koniec tury jest zatem siatką bezpieczeństwa dla całej tury, nie tylko momentem typecheck: ponownie sprawdza każdy zmieniony plik z `git diff`.

**Odroczenia.** Gdy plan testów jawnie odracza bramkę, która stałaby się hookiem (na przykład „post-edit hook — deferred, not v1”), zacytuj użytkownikowi odroczenie i jego powód, a następnie zapytaj raz: skonfigurować mimo to, czy uszanować odroczenie? Zapisz odpowiedź jako `override` lub `skip` w raporcie. Nie pytaj ponownie dla każdego hooka.

**Przeprowadź audyt istniejącej konfiguracji hooków przed zaproponowaniem czegokolwiek.** Przeczytaj pliki konfiguracji hooków harnessa (ścieżki w referencji) oraz każdy skrypt w jego katalogu hooks, w tym skrypty, do których nie odwołuje się żadna konfiguracja (np. osierocony skrypt hooks katalogu konfiguracji narzędzia AI, który wypisuje tylko do stdout). Dla każdego istniejącego hooka nazwij konkretne wady, na przykład:

- lint całego projektu lub `--fix .` przy każdej edycji (wolne, dotyka plików, których agent nie edytował, zgłasza stare błędy jako szum);
- polecenie, którego błąd kończy się kodem traktowanym przez harness jako nieblokujący albo wypisuje do kanału, który nigdy nie dociera do agenta (np. exit 1 + stdout, gdy harness przekazuje stderr tylko przy exit 2);
- timeouty wyglądające jak milisekundy (`10000`, `30000`), gdy harness oczekuje sekund;
- typecheck całego projektu per edit zamiast na końcu tury;
- hook końca tury bez zabezpieczenia retry (może zapętlać się aż do limitu harnessa);
- ta sama kontrola zarejestrowana dwa razy (Cursor i Copilot mogą importować pliki konfiguracji narzędzia AI — zobacz ich referencje).

Przedstaw mapę momentów i wyniki audytu razem jako propozycję. Poczekaj na zgodę użytkownika przed zapisaniem plików — **chyba że żądanie jest już taką zgodą**: użytkownik poprosił o skonfigurowanie konkretnych hooków w tym repozytorium, a propozycja nie dodaje niczego, o co nie prosił (brak nadpisania odroczenia, brak zastąpionego hooka, brak zmiany poza katalogiem hooks i konfiguracją). Wtedy przedstaw propozycję i kontynuuj; pytaj tylko o to, co wykracza poza żądanie.

## Krok 5 — Załaduj referencję harnessa

Przeczytaj `references/<harness>.md` dla każdego harnessa, który będziesz konfigurować:

- `references/anthropic.md` — twój asystent kodowania AI
- `references/cursor.md`
- `references/codex.md`
- `references/copilot.md`
- `references/other-harnesses.md` — Devin Desktop, Gemini CLI, OpenCode, Kiro, Junie: tylko linki i uwagi o możliwościach. Dla tych wyjaśnij ograniczenie i wskaż dokument zamiast generować konfigurację, której nie możesz udowodnić.

Każda referencja zaczyna się od `Verified on:` oraz kanonicznych URL-i dokumentacji. Jeśli możesz przeglądać, otwórz podlinkowany dokument i ponownie sprawdź pole payloadu dla edytowanego pliku, kanał docierający do agenta i jednostkę timeoutu. W razie konfliktu stosuj aktualną dokumentację i zapisz rozbieżność w raporcie („reference says X, doc now says Y”).

## Krok 6 — Wygeneruj konfigurację i skrypty

Postępuj według minimalnych przykładów z referencji; dostosuj polecenia do toolchaina z Kroku 3.

- **Skrypt per-edit** — odczytuje payload ze stdin, wyciąga ścieżkę edytowanego pliku przy użyciu pól wskazanych przez referencję, kończy się powodzeniem, gdy ścieżki brakuje albo typ pliku nie jest sprawdzany, i uruchamia kontrolę tylko na **tym pliku**. Przy błędzie wysyła krótki nagłówek oraz wyjście narzędzia kanałem, który dociera do agenta.
- **Skrypt end-of-turn** — przegląda turę: zbiera zmienione i nowe pliki (`git diff --name-only HEAD` plus nieśledzone), kończy się wcześniej, gdy ich nie ma, uruchamia kontrole per-edit na wszystkich (lint na objętych plikach; powiązane testy albo cały zestaw jednostkowy, gdy Krok 3 uznał go za szybki), następnie typecheck całego projektu i zgłasza wszystkie błędy w jednym komunikacie. Uwzględnia flagę retry harnessa (odpowiednik wskazany przez referencję), dzięki czemu agent dostaje **jedną** szansę na naprawę, a następnie może zakończyć. To, czego nie może naprawić, pozostawia bramce commitu. Utrzymaj łączny czas poniżej timeoutu (domyślnie 120 s); jeśli przegląd jest wolniejszy, najpierw ogranicz testy do wyłącznie powiązanych.
- **Zwykłe wyjście** — wyjście hooka odczytuje model, nie terminal: `tsc --pretty false`, `grep --color=never`, `NO_COLOR=1 FORCE_COLOR=0` dla wszystkiego innego. Skrypty dziedziczą środowisko powłoki użytkownika (np. `GREP_OPTIONS=--color=always` psuje filtr grep), więc ustawiaj je jawnie.
- **Payloady są niezaufanymi kształtami danych wejściowych.** Akceptuj każdy udokumentowany kształt: toleruj brakujące pola, puste stdin i alternatywne nazwy pól; nigdy nie powoduj błędu hooka dlatego, że pola nie ma. Brakująca ścieżka oznacza „nothing to check”, nie błąd.
- **Timeouty** w jednostce oczekiwanej przez harness (zwykle sekundy): około 30 dla lint per-edit, 60 dla powiązanych testów, 120 dla typecheck — następnie dostrój.
- **Ścieżki** — rozwiązuj checkout zgodnie z referencją, nigdy wyłącznie przez `cd` do zmiennej project-dir. W twoim asystencie kodowania AI zmienna katalogu projektu wskazuje katalog, w którym rozpoczęła się sesja, i staje się nieaktualna po wejściu do worktree, a edycja w sąsiednim worktree nie należy ani do tej zmiennej, ani do `cwd`: polecenie znajduje skrypt przez `git rev-parse --show-toplevel`, skrypt per-edit pobiera checkout z edytowanego pliku (ścieżki względne rozwiązywane względem `cwd`; pliki innych repozytoriów pomijane), a skrypt end-of-turn przegląda payload `cwd` oraz każdy checkout zarejestrowany dla `session_id`. Uczyń skrypty wykonywalnymi (`chmod +x`).
- **Brak cichego sukcesu** — exit 0 oznacza „sprawdzone i czyste” albo „nothing to check”. Brakujące narzędzie (`jq`, niezainstalowany linter), typechecker, który nie został uruchomiony, lub wrapper taki jak `[ -x script ] && exec script; exit 0` muszą widocznie kończyć się błędem (kanał informacji zwrotnej lub komunikat widoczny dla użytkownika), nigdy przechodzić.
- **Konfiguracja commitowana** — sprawdź, czy pliki hooków nie są ignorowane (na przykład użyj `git check-ignore -v` na pliku konfiguracji narzędzia AI). Ignorowany katalog hooks istnieje tylko na jednym dysku; zaproponuj ponowne uwzględnienie z referencji i pokaż diff `.gitignore`.
- **Windows** — odnotuj powłokę używaną przez harness (Git Bash, PowerShell, pole polecenia per-OS). Jeśli bash lub `jq` są niedostępne, wygeneruj zamiast tego skrypt Node.
- **Istniejąca konfiguracja** — scal z nią, zachowaj niepowiązane hooki, zastąp tylko wadliwe wpisy wskazane w Kroku 4 i pokaż diff przed zapisem. Skrypt pozostawiony przez zastąpiony lub osierocony hook pozostaje na dysku: wymień go w raporcie i zarekomenduj usunięcie; nigdy nie usuwaj go po cichu.
- **Podwójna rejestracja** — hooki konfiguracji narzędzia AI są uruchamiane również przez Cursor (import domyślnie włączony), przez Copilot CLI i przez VS Code, gdy `chat.useClaudeHooks` jest włączone. Jeśli jedno z nich jest używane, a pliki konfiguracji narzędzia AI już zawierają hooki, nie rejestruj tej samej kontroli ponownie w jego natywnej konfiguracji bez uwzględnienia importu (zobacz `references/cursor.md`, `references/copilot.md`).

## Krok 7 — Udowodnij przed przekazaniem, jako commitowany test

Dowodem jest **plik testowy commitowany do repozytorium**, uruchamiany przez własny runner testów repozytorium, a nie jednorazowa sekwencja poleceń powłoki. Hooki się zmieniają; dowód, którego nikt nie może ponownie uruchomić, przestaje cokolwiek udowadniać po pierwszej edycji.

Nazwij go `agent-hooks.test.*` i umieść obok istniejących testów narzędziowych repozytorium (w `scripts/__tests__/`, `tools/ci/` lub `tests/`, …), w runnerze, którego repozytorium już używa. Każdy przypadek przekazuje payload przez pipe (kształt z referencji) do rzeczywistego skryptu i sprawdza kod wyjścia oraz kanał wyjścia. Pracuj na tymczasowej kopii lub jednorazowym repozytorium/worktree git, nigdy na plikach w drzewie dewelopera. Stubuj tylko to, co jest wolne lub nieobecne w środowisku testowym (binarny linter na `PATH`, który oznacza marker), zachowaj rzeczywiste git, `jq` i skrypty.

Przypadki:

1. **Uszkodzony plik** — celowy, oczywisty błąd w pliku objętym kontrolą (nieużywana zmienna, niepoprawny typ). Oczekuj blokującego lub zwrotnego sygnału udokumentowanego przez referencję oraz komunikatu wskazującego plik i problem. Dla skryptów uruchamiających testy błąd musi być behawioralny (nieudaną asercją): błędy wyłącznie typów nie powodują błędu Vitest, który usuwa typy.
2. **Czysty plik** — oczekuj powodzenia i braku blokującego wyjścia. Dla skryptów end-of-turn wymaga to zielonego baseline z Kroku 3.
3. **Pomijany typ** — plik nieobjęty kontrolą (np. `README.md`). Oczekuj powodzenia.
4. **Nieistniejący plik** — payload wskazujący ścieżkę, która nie istnieje. Oczekuj powodzenia.
5. **Pusty payload** — `{}` oraz payload nieparsowalny lub bez ścieżki (np. `not json`, wywołanie narzędzia bez ścieżki). Oczekuj powodzenia.
6. **Koniec tury z ustawioną flagą retry** — oczekuj powodzenia nawet wtedy, gdy błąd występuje.
7. **Edycja omijająca hook per-edit** — błąd lint zapisany bezpośrednio na dysku, bez payloadu per-edit, skrypt end-of-turn. Oczekuj, że zablokuje i wskaże ten plik. Gdy nie ma w ogóle zmienionych plików, oczekuj powodzenia bez uruchamiania żadnej kontroli.
8. **Rozwiązywanie checkoutu** — zmienna project-dir wskazuje inny checkout zawierający błąd: hooki sprawdzają checkout sesji i pozostają zielone. Ścieżka względna rozwiązuje się względem `cwd`. Uszkodzony plik innego repozytorium jest pomijany. Edycja w sąsiednim worktree jest przeglądana przez skrypt end-of-turn. Każde polecenie z konfiguracji, uruchomione z worktree przy zmiennej project-dir ustawionej na nieistniejącą ścieżkę, dociera do wykonywalnego skryptu.
9. **Brak cichego sukcesu** — brakujący linter lub typechecker: oczekuj sygnału błędu, nie powodzenia.
10. **Kształty specyficzne dla harnessa** — dodatkowe przypadki wymienione przez referencję (np. Codex: payload niebędący patchem, patch wieloplikowy, `cwd` w podkatalogu).

Celowo zepsuj sam test raz: cofnij jedną poprawkę w skrypcie (np. usuń zabezpieczenie retry) i obserwuj, jak dopasowany przypadek staje się czerwony, a następnie ją przywróć. Podłącz test do bramki już uruchamianej przez repozytorium (zestaw jednostkowy, `verify`, pre-push), ograniczając go do zmian w katalogu hooks, jeśli zestaw jest wolny. Nie dodawaj zadania CI.

Cofnij każdy celowy błąd i potwierdź, że `git status` pokazuje tylko pliki, które zamierzałeś utworzyć lub zmienić. Jeśli przypadek kończy się błędem, napraw skrypt i uruchom test ponownie. Nigdy nie zgłaszaj, że hook działa, wyłącznie na podstawie konfiguracji.

## Krok 8 — Raport

Wypisz zwięzły raport:

- **Wejścia** — ścieżka planu testów (lub „No test plan found — gates derived from the toolchain”), harness(y) ze ścieżkami dowodów, polecenia toolchaina, baseline końca tury (zielony, naprawiony, ograniczony zakresem lub blokada).
- **Odroczenia** — każde cytowane odroczenie oraz odpowiedź użytkownika (`override` / `skip`).
- **Audyt** — wady znalezione w istniejącej konfiguracji hooków i to, co je zastąpiło; pozostawione skrypty (osierocone lub zastąpione) wraz z rekomendacją usunięcia.
- **Skonfigurowane** — per harness: plik(i) konfiguracji, skrypty, zdarzenia, timeouty; scalony diff.
- **Dowód** — commitowany plik testowy, polecenie, które go uruchamia, jego wynik oraz celowe zepsucie, które zaobserwowano jako czerwone.
- **Wciąż wymagane od użytkownika** — kroki zaufania lub zatwierdzenia (np. przegląd Codex `/hooks`, zaufanie do workspace Cursor, ponowne uruchomienie sesji) oraz sposób potwierdzenia, że hook został załadowany (menu hooks harnessa lub log debugowania).
- **Dryf referencji** — każda rozbieżność między referencją a aktualną dokumentacją.
- **Poza zasięgiem** — czego te hooki nie mogą udowodnić: zachowania działającej aplikacji (komponent kliencki, który nigdy nie hydratuje się, routing, polityki bazy danych, skutki uboczne). Lint, typy i testy jednostkowe mogą wszystkie przejść, podczas gdy funkcja jest uszkodzona. Nazwij zagrożone powierzchnie repozytorium i wskaż E2E lub weryfikację w przeglądarce; nie konfiguruj tego tutaj.
- **Rekomendowane bramki git (niezapisane)** — co należy do pre-commit i pre-push dla tego repozytorium, z istniejącym menedżerem hooków, jeśli taki jest. Nie twórz ani nie edytuj konfiguracji git-hooków.
- **Następny krok** — poproś agenta o edycję pliku z błędem lint, a następnie błędem typu, i obserwuj, jak naprawia oba w tej samej sesji. Następnie poproś go o przepisanie pliku poleceniem powłoki z błędem lint i obserwuj, jak hook końca tury odsyła go z powrotem.

## Czego ta umiejętność NIE robi

- Nie zmienia bramek ani strategii ryzyka — tym zajmuje się `/10x-test-plan`.
- Nie pisze testów dla aplikacji, scenariuszy E2E ani pipeline’ów CI (jedynym pisanym testem jest test hooka z Kroku 7).
- Nie instaluje ani nie rekonfiguruje menedżerów git-hooków (Husky, Lefthook, pre-commit).
- Nie generuje konfiguracji dla harnessów, których nie potrafi udowodnić (zobacz `references/other-harnesses.md`).

## Interaktywne prompty — niezależne od hosta

Gdy ta umiejętność mówi „ask the user”, użyj dowolnego narzędzia pytań udostępnianego przez hosta; jeśli żadne nie istnieje, zapytaj zwykłym tekstem z oznaczonymi opcjami. Zapytaj najwyżej o: który harness (tylko gdy jest niejednoznaczność), czy nadpisać odroczenie (raz), oraz zgodę po przedstawieniu propozycji (pomijane, gdy żądanie jest już taką zgodą, Krok 4).

## Ton

Zwięzły, rozkazujący, konkretny. Nazywaj pliki, polecenia i kody wyjścia. Bez języka marketingowego.