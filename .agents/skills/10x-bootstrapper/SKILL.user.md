---
name: 10x-bootstrapper
description: >
  Scaffold a project after a fresh CLI adapter has been researched and written.
  Reads context/foundation/tech-stack.md plus scaffold-adapters/manifest.md,
  executes user-confirmed CLI instructions through normal sandboxed tool calls,
  preserves context/, and writes a verification log. Use AFTER
  /10x-scaffold-adapter.
---
# Bootstrapper: wykonaj świeży adapter i bezpiecznie scal projekt

Ta umiejętność jest wykonawcą końca łańcucha:

```text
/10x-tech-stack-selector → /10x-scaffold-adapter → /10x-bootstrapper
```

Nie odkrywa CLI, nie korzysta ze statycznego `cmd_template`, nie naprawia adaptera i nie przekazuje komend do ogólnego skryptu. Czyta świeży adapter Markdown, pokazuje instrukcje użytkownikowi i wykonuje każdy proces osobnym, widocznym wywołaniem terminala z normalnym sandboxem i approvalami.

## Wymagane wejścia

1. Hand-off: domyślnie `context/foundation/tech-stack.md`; opcjonalny argument ścieżki działa dosłownie po usunięciu początkowego `@`.
2. `context/foundation/scaffold-adapters/manifest.md`.
3. Każdy adapter komponentu wskazany w manifeście.
4. `references/adapter-consumer.md`, `references/refusal-protocol.md`, `references/scaffold-merge.md` i `references/verification-log-schema.md`.

Brak hand-offu przekierowuje do `/10x-tech-stack-selector`. Brak, blokada lub nieaktualność adaptera przekierowuje do `/10x-scaffold-adapter`. Użyj dokładnych komunikatów z `references/refusal-protocol.md` i zatrzymaj się; historia rozmowy nie zastępuje plików.

## Przepływ

### 1. Zweryfikuj łańcuch i adaptery

Przeczytaj hand-off, manifest i wszystkie adaptery W CAŁOŚCI. Następnie zastosuj `references/adapter-consumer.md`:

- oblicz SHA-256 dokładnych bajtów hand-offu i porównaj z manifestem oraz adapterami,
- upewnij się, że wszystkie komponenty są obecne, ich `starter_id` i `target_dir` odpowiadają hand-offowi, a ścieżki nie nachodzą na siebie,
- odrzuć `blocked` i `docs-only`, brakujące sekcje, komendy w frontmatter i adaptery starsze niż `fresh_for_days`,
- ponownie sprawdź executable, wersję i fingerprint właściwego `--help`,
- jeśli jakikolwiek dowód się zmienił, nie aktualizuj adaptera inline — skieruj do `/10x-scaffold-adapter`.

`smoke-tested` jest ścieżką rekomendowaną. Dla `local-cli-confirmed` pokaż brakujący poziom dowodu i poproś o jawne potwierdzenie kontynuacji. `docs-only` i `blocked` nigdy nie mają override'u w tym skillu.

### 2. Pokaż jeden plan wykonania

Dla każdego komponentu pokaż:

- adapter, oficjalne źródła i czas wygenerowania,
- lokalną oraz najnowszą stabilną wersję CLI,
- status adaptera,
- logiczną nazwę, staging i docelowy katalog,
- dokładną komendę scaffoldingu,
- osobne komendy dependency setup, build/smoke i audit,
- oczekiwane pliki, operacje sieciowe/cache oraz znane ryzyka.

Zapytaj: „Proceed with these adapter instructions, or stop and regenerate them?”

- **Proceed (Recommended)** — tylko gdy wszystkie adaptery są `smoke-tested`.
- **Proceed with caution** — dostępne, gdy brak `blocked`/`docs-only`, ale co najmniej jeden status to `local-cli-confirmed`.
- **Stop and regenerate** — zakończ i wskaż `/10x-scaffold-adapter`.

Potwierdzenie dotyczy dokładnie pokazanych instrukcji. Zmiana komendy, wersji, cwd lub celu wymaga ponownego pokazania planu i potwierdzenia.

### 3. Zabezpiecz cwd i staging

Przeczytaj `references/refusal-protocol.md`. Sprawdź fingerprinty istniejących projektów w każdym `target_dir`. Przy zapełnionym celu pokaż znalezione pliki i poproś o potwierdzenie polityki konfliktów.

Jeżeli `.bootstrap-scaffold/` już istnieje, zatrzymaj się bez modyfikacji. Nie usuwaj pozostałości poprzedniego uruchomienia.

Utwórz wyłącznie katalogi staging wskazane w adapterach, wszystkie pod `.bootstrap-scaffold/`. Nigdy nie twórz stagingu w katalogu docelowym, `context/`, `.git/`, home ani poza workspace.

### 4. Wykonaj instrukcje jako jawne procesy

Dla każdego komponentu:

1. Uruchom dokładną komendę scaffoldu jako pojedynczy proces w cwd opisanym przez adapter.
2. Nie używaj `eval`, `shell=True`, `bash -c`, `sh -c`, potoków, przekierowań, `&&`, `;`, substytucji poleceń ani automatycznych instalatorów.
3. Nie wczytuj komendy do Pythona, YAML executora ani innego interpretera planu.
4. Pozwól normalnemu narzędziu terminalowemu wymusić sandbox i zgody dla sieci/cache.
5. Przechwyć dokładną komendę, cwd, stdout/stderr i exit code.
6. Wykonaj dependency setup i build/smoke jako kolejne osobne procesy tylko wtedy, gdy adapter je zawiera.

Najpierw wygeneruj i zweryfikuj wszystkie komponenty w stagingu. Nie scalaj żadnego z nich, dopóki każdy obowiązkowy scaffold i identity check nie przejdzie.

Niezerowy exit code scaffoldu/builda lub niezgodna tożsamość to HARD-STOP. Zachowaj cały staging, zapisz częściowy log i nie uruchamiaj kolejnych komponentów ani merge.

### 5. Zweryfikuj tożsamość i scal

Przeczytaj `references/scaffold-merge.md`. Dla każdego komponentu sprawdź wymagane ścieżki, zabronione tokeny stagingowe oraz zgodność logicznej nazwy. Kod `0` bez przejścia identity check nie oznacza sukcesu.

Po sukcesie wszystkich komponentów przygotuj pełny plan merge. `context/**` jest zawsze zachowywany, `.gitignore` jest append-merge z deduplikacją, a każda inna kolizja trafia do sąsiada `.scaffold`. Pokaż plan, a następnie wykonaj go plik po pliku i zapisz dziennik.

### 6. Audyt i log

Uruchom instrukcję audytu z każdego adaptera osobnym procesem po scaleniu (lub pomiń, jeśli adapter dokumentuje brak właściwego narzędzia). Audyt jest informacyjny; jego błąd lub findings nie cofają poprawnego scaffoldu.

Przeczytaj `references/verification-log-schema.md` i zapisz `context/changes/bootstrap-verification/verification.md`. Log musi zawierać adaptery i ich hashe, źródła, wszystkie dokładne komendy/cwd/exit codes, identity checks, merge log oraz audyty. `phase_3_status: ok` wymaga poprawnego scaffoldingu i tożsamości wszystkich komponentów.

Na sukces wypisz komponenty, docelowe katalogi, wynik build/smoke i audytu oraz ścieżkę logu. Nie ustawiaj schowka.

## Granice

1. Adapter jest instrukcją, nie automatyczną autoryzacją.
2. Bootstrapper nie przegląda internetu w celu naprawienia instrukcji; świeżość należy do `/10x-scaffold-adapter`.
3. Nie wykonuj `cmd_template` z rejestru starterów ani komend zapisanych w frontmatter.
4. Nie instaluj/aktualizuj globalnych CLI i nie uruchamiaj wdrożeń.
5. Nie scaffoldinguje się częściowo: wszystkie komponenty przechodzą staging i identity check przed pierwszym merge.
6. `context/` i istniejące pliki użytkownika nigdy nie są nadpisywane.
7. Nie generuj AGENTS.md, CLAUDE.md, workflow CI ani historii git.

## Referencje

- `references/adapter-consumer.md` — walidacja manifestu, świeżości i instrukcji.
- `references/refusal-protocol.md` — brak/starość/blokada adaptera i kolizje.
- `references/scaffold-merge.md` — staging, identity checks i konflikt-safe merge.
- `references/verification-log-schema.md` — pełny oraz częściowy log.
