---
name: 10x-stack-assess
description: >
  Assess an existing project's stack for agent-friendliness against the 4
  quality gates (typed, convention-based, popular, well-documented); writes
  context/foundation/stack-assessment.md with per-component scores, gaps, and
  ready-to-paste CLAUDE.md/AGENTS.md entries. Trigger phrases: "assess my
  stack", "is my stack agent-friendly", "oceń mój stack", "stack assessment".
  Use AFTER /10x-prd (brownfield), BEFORE /10x-health-check.
---
# Ocena stosu: oceń istniejący stos pod kątem przyjazności dla agentów

Ta umiejętność jest odpowiednikiem `/10x-tech-stack-selector` dla projektów brownfield. Podczas gdy tech-stack-selector pomaga użytkownikom greenfield **wybrać** stos, stack-assess pomaga użytkownikom brownfield **ocenić** ich stos. Wykorzystuje te same cztery bramki jakości przyjazne agentom (`references/agent-friendly-criteria.md`), ale stosuje je jako perspektywę oceny, a nie filtr wyboru.

Umiejętność znajduje się w łańcuchu brownfield: `/10x-shape → /10x-prd → /10x-stack-assess → /10x-health-check`. Jej jedyne zadanie: ocenić istniejący stos względem bramek jakości i stworzyć ustrukturyzowaną ocenę z konkretnymi strategiami kompensacji.

Kluczową wartością dla brownfield jest **ścieżka kompensacji** — gdy bramka nie przejdzie, umiejętność nie zaleca wymiany stosu. Dokumentuje, co dodać do plików instrukcji (pliku konfiguracji AI projektu (AGENTS.md)), aby agent mógł działać skutecznie pomimo luki.

## Kiedy używać, kiedy pominąć

**Użyj, gdy**: użytkownik ma istniejący projekt i chce ocenić, jak dobrze jego stos wspiera przepływy pracy agentów AI. Katalog projektu powinien zawierać rozpoznawalne znaczniki projektu (`package.json`, `Cargo.toml`, `pyproject.toml`, `go.mod`, `Gemfile`, `composer.json`, `*.csproj`, `pubspec.yaml`). Opcjonalnie może istnieć `context/foundation/prd.md` (brownfield) — jeśli jest obecny, umiejętność używa go do osadzenia oceny w kontekście (np. określenia komponentów znajdujących się w zakresie zmian).

**Pomiń, gdy**: użytkownik rozpoczyna nowy projekt od zera — przekieruj do `/10x-tech-stack-selector`. Pomiń również, gdy użytkownik chce tylko audytu zależności lub skanowania bezpieczeństwa bez perspektywy bramek jakości — to obszar `/10x-health-check`.

## Relacja z innymi umiejętnościami

- `/10x-shape` — etap wcześniejszy. Tworzy `shape-notes.md` z `context_type: brownfield`.
- `/10x-prd` — etap wcześniejszy. Tworzy `context/foundation/prd.md` (szablon brownfield). Opcjonalne dane wejściowe — umiejętność może działać bez PRD.
- `/10x-tech-stack-selector` — równoległy etap greenfield. Te same bramki jakości, inne zadanie (wybór vs ocena).
- `/10x-health-check` — odbiorca na dalszym etapie. Odczytuje `context/foundation/stack-assessment.md`, aby skupić kontrole stanu na zidentyfikowanych lukach.

## Wymagane dane wejściowe

1. Istniejąca baza kodu w cwd z co najmniej jednym rozpoznawalnym znacznikiem projektu.
2. `references/agent-friendly-criteria.md` — dołączony. Cztery bramki jakości i ścieżka kompensacji.

## Opcjonalne dane wejściowe

1. `context/foundation/prd.md` — jeśli jest obecny i ma `context_type: brownfield`, umiejętność używa sekcji `## Scope of Change` i `## Current System Overview` z PRD, aby skupić ocenę na odpowiednich komponentach stosu.

## Początkowa odpowiedź

Gdy ta umiejętność zostanie wywołana:

1. **Jeśli podano argument ścieżki** (np. `/10x-stack-assess @context/foundation/prd.md`), usuń początkowy znak `@`, jeśli występuje, i użyj ścieżki jako lokalizacji PRD dla tego uruchomienia. PRD jest opcjonalnym kontekstem, a nie warunkiem wstępnym — umiejętność działa bez niego.
2. **Jeśli nie podano argumentu**, sprawdź `context/foundation/prd.md`. Jeśli jest obecny i ma `context_type: brownfield`, wczytaj go jako kontekst. Jeśli go nie ma, kontynuuj bez kontekstu PRD.

## Przepływ pracy

### Krok 0 — Warunek wstępny cwd

Wykryj znaczniki projektu:

```bash
find . -maxdepth 1 \( -name "package.json" -o -name "Cargo.toml" -o -name "pyproject.toml" -o -name "go.mod" -o -name "Gemfile" -o -name "composer.json" -o -name "*.csproj" -o -name "pubspec.yaml" \) 2>/dev/null
```

Jeśli **nie znaleziono znaczników**, wyświetl:

```
No project markers found in the current directory. /10x-stack-assess requires an existing codebase.
If you're starting from scratch, use /10x-tech-stack-selector instead.
```

Następnie ZATRZYMAJ.

Jeśli znaleziono znaczniki, przejdź do Kroku 1.

### Krok 1 — Wykryj komponenty stosu

Sprawdź pliki projektu, aby zidentyfikować stos. Wykrywanie opiera się na plikach — sprawdzaj to, co jest na dysku, nie zgaduj.

**Źródła wykrywania według rodziny języków:**

| Rodzina języków | Pliki znaczników | Co wyodrębnić |
|---|---|---|
| JS/TS | `package.json`, `tsconfig.json`, `next.config.*`, `astro.config.*`, `vite.config.*`, `svelte.config.*`, `nuxt.config.*`, `angular.json`, `.eslintrc*`, `prettier.config.*`, `jest.config.*`, `vitest.config.*`, `playwright.config.*` | Język (JS vs TS — obecność `tsconfig.json`), framework, narzędzie budowania, runner testów, linter, formatter, menedżer pakietów (z pliku lock: `package-lock.json` → npm, `yarn.lock` → yarn, `pnpm-lock.yaml` → pnpm, `bun.lockb` → bun) |
| Python | `pyproject.toml`, `setup.py`, `setup.cfg`, `requirements.txt`, `Pipfile`, `poetry.lock`, `uv.lock` | Framework (Django, FastAPI, Flask — z zależności), sprawdzanie typów (mypy/pyright w zależnościach lub konfiguracji), runner testów (pytest/unittest), menedżer pakietów |
| Rust | `Cargo.toml` | Edycja, zależności dla frameworka webowego (Actix, Axum, Rocket), framework testowy |
| Go | `go.mod` | Wersja Go, framework webowy (Gin, Echo, Fiber, Chi, stdlib), framework testowy |
| Ruby | `Gemfile` | Framework (Rails, Sinatra), wersja Ruby, sprawdzanie typów (Sorbet/RBS), framework testowy (RSpec, Minitest) |
| PHP | `composer.json` | Framework (Laravel, Symfony), wersja PHP, sprawdzanie typów (PHPStan/Psalm), framework testowy (PHPUnit, Pest) |
| .NET | `*.csproj`, `*.sln` | Framework (wersja .NET, ASP.NET), język (C#/F#), framework testowy (xUnit, NUnit) |
| Dart | `pubspec.yaml` | Framework (Flutter, serwer Dart), framework testowy |

**Dodatkowe sygnały do sprawdzenia:**

- CI/CD: `.github/workflows/`, `.gitlab-ci.yml`, `Jenkinsfile`, `.circleci/config.yml`, `cloudbuild.yaml`
- Wdrażanie: `Dockerfile`, `docker-compose.yml`, `fly.toml`, `vercel.json`, `netlify.toml`, `wrangler.toml`, `render.yaml`, `railway.json`, `Procfile`
- Pliki instrukcji: plik konfiguracji AI projektu (AGENTS.md), `.cursor/rules`, `.github/copilot-instructions.md`
- Jakość konfiguracji: `.editorconfig`, `.prettierrc*`, `.eslintrc*`, `tsconfig.json` (sprawdzenie trybu strict)

Przedstaw użytkownikowi wykryty stos:

```
Detected stack:
  Language:        <language> (<typed: yes/no>)
  Framework:       <framework> (<version if detectable>)
  Build tool:      <build tool>
  Test runner:     <test runner or "not detected">
  Package manager: <package manager>
  CI/CD:           <provider or "not detected">
  Deployment:      <target or "not detected">
  Instruction files: <list or "none">
```

Zapytaj użytkownika: „Czy to wykrywanie jest poprawne? Czy czegoś brakuje lub coś jest nieprawidłowe?”

- Poprawne — kontynuuj (zalecane): Kontynuuj z tym wykrytym stosem.
- Popraw coś: Poprawię wykrywanie przed punktacją.

Jeśli „Popraw coś”: zapytaj, który komponent poprawić, zastosuj nadpisanie w pamięci i kontynuuj.

### Krok 2 — Oceń względem bramek jakości

Wczytaj `references/agent-friendly-criteria.md`.

Dla każdego wykrytego komponentu (język, framework, narzędzie budowania, runner testów) oceń go względem czterech bramek. Punktacja jest na poziomie komponentu, a nie projektu — projekt może mieć język typowany, ale framework nieoparty na konwencjach.

**Zasady punktacji:**

#### Bramka 1: Typowany

- **Zaliczone**: język domyślnie używa jawnych typów (TypeScript, Rust, Go, Java, Kotlin, C#, Dart) LUB projekt ma skonfigurowane sprawdzanie typów (Python + mypy/pyright w zależnościach/konfiguracji, Ruby + Sorbet/RBS, PHP + PHPStan/Psalm).
- **Niezaliczone**: JavaScript bez TypeScript, Python bez skonfigurowanego sprawdzania typów, Ruby bez Sorbet/RBS, PHP bez analizy statycznej.
- **Dowody**: wskaż konkretny plik/konfigurację potwierdzającą ocenę (np. „obecny `tsconfig.json` z `strict: true`” lub „brak `mypy` w `pyproject.toml [tool.mypy]` lub zależnościach deweloperskich”).

#### Bramka 2: Oparty na konwencjach

- **Zaliczone**: framework zawiera silne założenia dotyczące układu folderów, routingu i konfiguracji (Next.js App Router, Rails, Django, Spring Boot, Astro, Angular, Laravel, .NET).
- **Niezaliczone**: framework jest minimalny/nie narzuca opinii, a projekt nie ma udokumentowanych konwencji (Express, Koa, Flask bez blueprintów, Sinatra, czysty Vite + React).
- **Częściowo zaliczone**: minimalny framework, ALE projekt ma udokumentowane konwencje w plikach instrukcji (pliku konfiguracji AI projektu (AGENTS.md)) lub widocznym dokumencie konwencji. Oceń jako zaliczone-z-uwagą.
- **Dowody**: wskaż siłę konwencji frameworka lub ich brak.

#### Bramka 3: Popularny w danych treningowych

- **Ocena według rodziny języków** (kluczowa — zobacz `references/agent-friendly-criteria.md`). Oceniaj w ramach rodziny języków, a nie globalnie.
- **Zaliczone**: framework jest popularnym wyborem w swoim ekosystemie językowym (React, Next.js, Vue, Angular w JS; Django, FastAPI, Flask w Pythonie; Rails w Ruby; Spring w Javie; Laravel w PHP; .NET w C#; Flutter w Darcie).
- **Niezaliczone**: niszowy lub bardzo nowy framework z ograniczonymi danymi treningowymi w ramach własnej rodziny języków.
- **Dowody**: podaj nazwę frameworka i jego pozycję w ekosystemie językowym.

#### Bramka 4: Dobrze udokumentowany

- **Zaliczone**: framework ma aktualną, wersjonowaną oficjalną dokumentację.
- **Niezaliczone**: dokumentacja jest rozproszona, nieaktualna lub wiki utrzymywane przez społeczność nie jest zsynchronizowane.
- **Dowody**: odnotuj obserwację dotyczącą jakości dokumentacji.

**Wyświetl macierz punktacji:**

```
Quality Gate Assessment:

| Component  | Typed | Convention | Training Data | Documented | Verdict    |
|------------|-------|------------|---------------|------------|------------|
| Language   | ✓/✗   | —          | —             | —          | pass/fail  |
| Framework  | —     | ✓/✗        | ✓/✗           | ✓/✗        | pass/fail  |
| Build tool | —     | ✓/✗        | ✓/✗           | ✓/✗        | pass/fail  |
| Test runner| —     | —          | ✓/✗           | ✓/✗        | pass/fail  |

Legend: ✓ = pass, ✗ = fail, ~ = partial, — = not applicable
```

### Krok 3 — Zidentyfikuj strategie kompensacji

Dla każdej niezaliczonej bramki utwórz konkretną strategię kompensacji. Kompensacja oznacza konkretne wpisy do dodania do plików instrukcji (pliku konfiguracji AI projektu (AGENTS.md)), aby agent mógł działać skutecznie pomimo luki.

**Szablony kompensacji według niepowodzenia bramki:**

**Typowany: niezaliczone** →
- Dodaj do pliku konfiguracji AI projektu (AGENTS.md) konwencję jawnych adnotacji typów („Cały nowy kod musi zawierać adnotacje typów na granicach funkcji”)
- Dodaj regułę walidacji na granicach („Używaj Zod/Pydantic/JSON Schema na granicach API”)
- Jeśli Python: dodaj rekomendację konfiguracji mypy
- Jeśli JS: dodaj ścieżkę migracji do TypeScript lub podpowiedzi typów JSDoc

**Oparty na konwencjach: niezaliczone** →
- Udokumentuj konwencje struktury folderów w pliku konfiguracji AI projektu (AGENTS.md) („Trasy znajdują się w src/routes/, middleware w src/middleware/, ...”)
- Udokumentuj konwencje nazewnictwa („Pliki: kebab-case, eksporty: PascalCase dla komponentów, camelCase dla funkcji”)
- Udokumentuj kolejność rejestracji middleware/wtyczek
- Udokumentuj wzorzec obsługi błędów

**Popularny w danych treningowych: niezaliczone** →
- Dodaj do pliku konfiguracji AI projektu (AGENTS.md) przykłady idiomów specyficznych dla frameworka
- Podaj link do oficjalnej dokumentacji w pliku instrukcji
- Dodaj reguły „preferuj wzorzec X zamiast Y” dla wyborów specyficznych dla frameworka
- Zaznacz, że agent może potrzebować więcej wskazówek dla tego frameworka

**Dobrze udokumentowany: niezaliczone** →
- Przypnij wersję frameworka w pliku instrukcji
- Dodaj linki do najlepszej dostępnej dokumentacji
- Uwzględnij przykłady inline typowych wzorców
- Zaznacz niuanse specyficzne dla wersji

Każdy wpis kompensacji musi być **gotowy do wklejenia** do pliku instrukcji — nie ogólna porada, lecz faktyczny tekst reguły.

### Krok 4 — Ustal ogólny werdykt

Na podstawie macierzy punktacji i dostępnej kompensacji:

- **ready**: wszystkie bramki są zaliczone dla wszystkich komponentów. Stos jest przyjazny agentom od razu po uruchomieniu.
- **ready-with-compensation**: niektóre bramki są niezaliczone, ale wszystkie niepowodzenia mają jasne strategie kompensacji. Stos działa z udokumentowanymi konwencjami.
- **significant-friction**: wiele bramek jest niezaliczonych ORAZ kompensacja jest rozbudowana (np. nietypowany język + framework nieoparty na konwencjach + niszowy w danych treningowych). Agent będzie potrzebować istotnego prowadzenia.

Werdykt ma charakter informacyjny, a nie blokujący. Nawet `significant-friction` nie oznacza „zmień stos” — oznacza „przeznacz więcej czasu na tworzenie plików instrukcji i oczekuj większej liczby cykli korekty agenta”.

### Krok 5 — Zapisz ocenę

Sprawdź kolizję:

```bash
test -f context/foundation/stack-assessment.md
```

Jeśli plik istnieje, zapytaj użytkownika: „context/foundation/stack-assessment.md już istnieje. Jak chcesz kontynuować?”

- Nadpisz (zalecane): Zastąp istniejącą ocenę. Poprzednia wersja zostanie utracona, chyba że została zatwierdzona w repozytorium.
- Zapisz jako stack-assessment-v2.md: Zachowaj historię. Nowa ocena trafi do kolejnego dostępnego slotu wersji.
- Przerwij: Zakończ bez zapisywania. Ocena z rozmowy zostanie zachowana wyłącznie na czacie.

Zbuduj plik wyjściowy:

```markdown
---
project: <project name from package.json/Cargo.toml/etc or directory name>
assessed_at: <ISO 8601 timestamp>
agent_readiness: <ready | ready-with-compensation | significant-friction>
context_type: brownfield
stack_components:
  language: <language>
  framework: <framework>
  build_tool: <build tool>
  test_runner: <test runner or null>
  package_manager: <package manager>
  ci_provider: <provider or null>
  deployment_target: <target or null>
gates_passed: <N>
gates_failed: <N>
---

## Stack Components

<detected stack details — one paragraph per component, noting version where detectable>

## Quality Gate Assessment

<the scoring matrix from Step 2, with evidence for each score>

### Gate Details

<per-gate breakdown with evidence citations — which file/config proved each score>

## Gaps & Compensation

<for each failed gate: what failed, why it matters for agent workflows, and the concrete compensation strategy>

### Recommended Instruction File Additions

<ready-to-paste project's AI configuration file (AGENTS.md) entries for each compensation strategy, formatted as markdown rule blocks the user can copy directly>

## Summary

<overall verdict, key strengths, key gaps, and recommended next step (/10x-health-check)>
```

Zapisz do `context/foundation/stack-assessment.md` (tworząc `context/foundation/`, jeśli nie istnieje).

Po zapisie skopiuj polecenie następnego kroku i ogłoś:

```bash
echo -n "/10x-health-check" | pbcopy 2>/dev/null || echo -n "/10x-health-check" | clip.exe 2>/dev/null || echo -n "/10x-health-check" | xclip -selection clipboard 2>/dev/null || true
```

```powershell
# PowerShell (Windows)
Set-Clipboard "/10x-health-check"
```

Wyświetl:

```
═══════════════════════════════════════════════════════════
  STACK ASSESSED
═══════════════════════════════════════════════════════════

  Project:       <project name>
  Readiness:     <ready | ready-with-compensation | significant-friction>
  Gates passed:  <N> / <total>

  ► Assessment:  context/foundation/stack-assessment.md
  ► Next:        /10x-health-check  (✓ copied to clipboard)
═══════════════════════════════════════════════════════════
```

ZATRZYMAJ. Nie przechodź automatycznie do `/10x-health-check` — użytkownik uruchamia je, gdy jest gotowy.

## Wynik

Zapisywany jest pojedynczy plik: `context/foundation/stack-assessment.md` (lub `stack-assessment-vN.md`, jeśli wybrano zapis wersjonowany).

## Referencje

- `references/agent-friendly-criteria.md` — cztery bramki jakości, zastrzeżenie dotyczące poszczególnych rodzin języków, ścieżka kompensacji.

## Kluczowe zabezpieczenia

1. **Cwd jest warunkiem wstępnym.** Umiejętność wymaga istniejącej bazy kodu z rozpoznawalnymi znacznikami projektu. Nie twórz oceny wyłącznie na podstawie kontekstu rozmowy.

2. **Oceniaj, nie zalecaj wymiany.** Umiejętność nigdy nie zaleca zmiany stosu. Ocenia to, co istnieje, i dostarcza strategie kompensacji. Użytkownik wybrał swój stos z powodów, których umiejętność nie zna — uszanuj ten wybór.

3. **Ocena według rodziny języków dla bramki 3.** Oceniaj „popularność w danych treningowych” w ramach rodziny języków, a nie globalnie. Django jest popularne w Pythonie; fakt, że ma mniej pobrań npm niż React, jest nieistotny.

4. **Kompensacja jest konkretna, a nie ogólna.** Każdy wpis kompensacji musi być regułą pliku instrukcji gotową do wklejenia. „Dodaj lepszą dokumentację” nie jest kompensacją; „Dodaj do pliku konfiguracji AI projektu (AGENTS.md): `## Routing — Routes are registered in src/routes/index.ts. Each route file exports a default Hono handler. Middleware runs in registration order.`” jest kompensacją.

5. **Dowody dla każdej oceny.** Każde zaliczenie lub niezaliczenie bramki musi wskazywać konkretny plik, sekcję konfiguracji lub ich brak, które uzasadniają ocenę. Żadnych ocen opartych na przeczuciu.

6. **Wewnętrzne etykiety umiejętności pozostają wewnętrzne.** Rozmawiając z użytkownikiem, nigdy nie odwołuj się do numerów bramek („Bramka 1”), liter kroków („Krok 2”) ani wewnętrznych nazw pól (`agent_readiness`, `gates_passed`). Używaj zwykłego języka: „bezpieczeństwo typów twojego stosu”, „ogólna gotowość na agentów”, „ile kryteriów spełnia twój stos”.

7. **Wyłącznie uniwersalny język.** W dostarczanej treści nie umieszczaj prywatnych ścieżek vault ani brandingu specyficznego dla organizacji.