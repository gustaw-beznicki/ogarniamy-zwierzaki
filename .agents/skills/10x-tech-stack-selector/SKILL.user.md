---
name: 10x-tech-stack-selector
description: >
  Pick a starter and a stack for a greenfield project after the PRD is written.
  Reads context/foundation/prd.md, reasons over a language-aware starter
  registry with four agent-friendly quality gates, and writes the
  context/foundation/tech-stack.md hand-off. Use when the user asks "what
  stack should I use", "pick a stack", "choose framework",
  "co wybrać do projektu". Use AFTER /10x-prd, BEFORE
  /10x-scaffold-adapter.
---
# Selektor stosu technologicznego: od PRD do startera

Ta umiejętność jest trzecim ogniwem w łańcuchu bootstrap (`/10x-shape → /10x-prd → /10x-tech-stack-selector → /10x-scaffold-adapter → /10x-bootstrapper`). Jej jedyne zadanie: przekształcić zapisany PRD w rekomendowany starter lub zestaw starterów oraz niewielkie, czytelne dla maszyn przekazanie. `/10x-scaffold-adapter` zamienia ten wybór na świeże, oparte na oficjalnej dokumentacji instrukcje CLI; dopiero `/10x-bootstrapper` je wykonuje.

Umiejętność jest **facylitatorem decyzji działającym na podstawie wyselekcjonowanego rejestru**, a nie silnikiem rekomendacji opartym na pierwszych zasadach. Odczytuje priory z PRD, zadaje najwyżej ~6 pozostałych pytań na ścieżce niestandardowej (lub przechodzi bezpośrednio do zweryfikowanej rekomendacji na ścieżce standardowej), analizuje karty starterów uwzględniające język w `references/starter-registry.yaml` oraz stosuje cztery bramki jakości z twardym filtrowaniem. Rozbudowane uzasadnienie pozostaje w rozmowie; przekazanie do pliku jest minimalne.

Rejestr starterów w `references/starter-registry.yaml` jest **jedynym źródłem prawdy o wyborach dostępnych w selektorze**. Celowo nie zawiera komend ani instrukcji instalacji. `/10x-scaffold-adapter` musi potwierdzić bieżący sposób użycia w najnowszej oficjalnej dokumentacji i lokalnym `--help`; `/10x-bootstrapper` nie czyta rejestru.

## Kiedy używać, kiedy pomijać

**Używaj, gdy**: istnieje `context/foundation/prd.md`, a użytkownik jest gotowy wybrać stos technologiczny. Frazy wyzwalające: "what stack should I use", "pick a starter", "choose a framework", "co wybrać", "what should I build this in", "can you recommend a stack". Używaj również, gdy użytkownik prosi o porównanie ("React vs Vue vs Svelte") i ma PRD na dysku — umiejętność wymusza ścieżkę niestandardową i przeprowadza przez warianty frameworków.

**Pomiń, gdy**: brakuje `context/foundation/prd.md` — umiejętność odmawia i przekierowuje do `/10x-shape` + `/10x-prd`. Pomiń także, gdy użytkownik jest w trakcie implementacji w istniejącej bazie kodu i pyta o dodanie biblioteki lub zastąpienie pojedynczej zależności — to obszar `/10x-frame`, a nie wybór stosu.

## Relacja z innymi umiejętnościami

- `/10x-shape` — tworzy `shape-notes.md`, poprzednik PRD. Jest dwa etapy przed tą umiejętnością.
- `/10x-prd` — tworzy `context/foundation/prd.md`, kanoniczne dane wejściowe. Zawsze jest upstream.
- `/10x-scaffold-adapter` — bezpośredni konsument downstream. Odczytuje `context/foundation/tech-stack.md`, sprawdza aktualną oficjalną dokumentację i lokalne CLI, po czym zapisuje deklaratywny adapter dla każdego komponentu.
- `/10x-bootstrapper` — wykonuje wyłącznie zatwierdzone instrukcje z adapterów i scala zweryfikowany staging z projektem. Nie odkrywa CLI samodzielnie.

## Wymagane dane wejściowe

1. Plik PRD — istnieje, jest czytelny, zgodny ze schematem PRD (`/skills/10x-shape/references/prd-schema.md`). Domyślna lokalizacja: `context/foundation/prd.md`. Użytkownik MOŻE przekazać inną ścieżkę jako argument (zobacz „Initial Response” poniżej). Umiejętność odczytuje **frontmatter** jako priory (`product_type`, `target_scale`, `timeline_budget`, `project`) i może odczytywać sekcje treści (`## Functional Requirements`, `## Non-Goals`) do audytu funkcji oraz wykrywania momentów sokratejskich, gdy FR-y PRD wskazują funkcję, której rekomendowany starter nie zawiera.
2. `references/starter-registry.yaml` — dołączony do umiejętności. Ładowany w momencie podejmowania decyzji.
3. `references/residual-interview.md` — dołączony. Ładowany w momencie rozmowy kwalifikacyjnej.
4. `references/handoff-schema.md` — dołączony. Ładowany w momencie zapisu.
5. `references/agent-friendly-criteria.md` — dołączony. Ładowany w momencie filtrowania.
6. `references/decision-flow.md` — dołączony. Ładowany w momencie podejmowania decyzji.

## Initial Response

Gdy ta umiejętność jest wywoływana:

1. **Jeśli podano argument ścieżki** (np. `/10x-tech-stack-selector @context/foundation/prd-v2.md` lub `/10x-tech-stack-selector path/to/prd.md`), usuń początkowy `@`, jeśli występuje, i użyj ścieżki dosłownie jako lokalizacji PRD dla tego uruchomienia.
2. **Jeśli nie podano argumentu**, ustaw domyślną ścieżkę PRD na `context/foundation/prd.md`.

Przenieś rozwiązaną ścieżkę do kroku 0; reszta procesu działa na niej jako `<prd-path>`.

## Proces

### Step 0 — warunek wstępny PRD

Sprawdź warunek wstępny PRD względem rozwiązanej ścieżki:

```bash
test -f "<prd-path>"
```

Jeśli plik **nie istnieje**, wykonaj dokładnie to i ZATRZYMAJ SIĘ — bez zastępczej rozmowy kwalifikacyjnej, bez wbudowanego mini-PRD, bez odczytywania historii rozmowy w poszukiwaniu zastępczych priorów:

```bash
echo -n "/10x-shape" | pbcopy 2>/dev/null || echo -n "/10x-shape" | clip.exe 2>/dev/null || echo -n "/10x-shape" | xclip -selection clipboard 2>/dev/null || true
```

```powershell
# PowerShell (Windows)
Set-Clipboard "/10x-shape"
```

Wypisz dosłownie (podstaw rozwiązaną ścieżkę; jeśli użyto wartości domyślnej, będzie to `context/foundation/prd.md`):

```
Tech-stack-selector requires a PRD at `<prd-path>`. Run `/10x-shape` first, then re-invoke.
```

Następnie ZATRZYMAJ SIĘ. Kontekst rozmowy **nie** jest rozwiązaniem zastępczym — nawet jeśli zawartość PRD była wcześniej omawiana na czacie, umiejętność wymaga pliku na dysku.

Jeśli plik **istnieje**, odczytaj go W CAŁOŚCI (bez częściowych odczytów) i przejdź do kroku 1.

### Step 1 — załaduj priory PRD

Sparsuj frontmatter PRD. Wyodrębnij:

- `project` → inicjuje `project_name` w przekazaniu (przekształć je do kebab-case dla przekazania, jeśli nie jest już w kebab-case).
- `product_type` → steruje wyszukiwaniem rozgałęzienia ścieżki Q0.
- `target_scale.users` → waga priorów (small/medium/large/enterprise).
- `timeline_budget.mvp_weeks` → waga priorów (krótkie harmonogramy faworyzują sprawdzone w boju + popularne startery).

Odczytaj treść PRD na potrzeby kontekstu audytu funkcji: przeskanuj `## Functional Requirements` pod kątem funkcji wymuszających technologię (auth, payments, realtime, AI/LLM, background jobs, file storage, i18n). Później przedstaw je jako listę kontrolną w Q1.

Powtórz priory użytkownikowi:

```
PRD priors:
  Project:       <project>
  Product type:  <product_type>
  Scale:         <target_scale.users>
  Timeline:      <timeline_budget.mvp_weeks> weeks
                 (after-hours: <timeline_budget.after_hours_only>)

  Detected feature signals from FRs:
    - <feature> (FR-NNN)
    - ...
```

Zapytaj użytkownika:

"Czy te priory są poprawne, czy chcesz coś skorygować, zanim przejdziemy dalej?"

Opcje:
- **Correct — proceed (Recommended)**: Kontynuuj z tymi priorami.
- **Correct a value**: Zapytaj, które pole skorygować, a następnie zaktualizuj nadpisanie w pamięci (PRD na dysku pozostaje bez zmian).
- **Stop — fix the PRD first**: Zakończ. Uruchom ponownie /10x-prd, aby poprawić priory, a następnie ponownie wywołaj /10x-tech-stack-selector.

Jeśli wybrano "Correct a value": zapytaj, które pole, zapisz nadpisanie i kontynuuj z nadpisaniem zastosowanym tylko dla tej sesji.

### Step 2 — rozgałęzienie ścieżki Q0 + pozostała rozmowa kwalifikacyjna

Załaduj `references/residual-interview.md` i postępuj zgodnie z opisanym tam przepływem Q.

Rozmowa kwalifikacyjna ma dwie ścieżki:

- **Ścieżka standardowa** (domyślnie rekomendowana w Q0): użytkownik akceptuje zweryfikowaną rekomendację dla swojej komórki `(product_type, language_family)`. Q1–Q3 i Q6 są pomijane. Q4 (wdrożenie), Q5 (CI/CD) oraz potwierdzenie nazwy projektu nadal są wykonywane; samokontrola Q8 jest pomijana (rekomendowana ścieżka sama w sobie jest bezpieczniejszym wyborem).
- **Ścieżka niestandardowa** (użytkownik wybiera zaprojektowanie własnej): pełne przejście Q1–Q6 oraz warunkowe Q7 (runner testów) i samokontrola Q8 przed przekazaniem.

Q0 wyprowadza `language_family` z jawnej zawartości PRD, jeśli jest obecna, w przeciwnym razie pyta raz w Q0 (frontmatter PRD nie zawiera tech_preferences). Mapa recommended-defaults na początku `references/starter-registry.yaml` rozwiązuje `(product_type, language_family) → starter_id`. Jeśli komórka ma zweryfikowaną wartość domyślną, przedstaw ją po nazwie wraz z jednoliniowym uzasadnieniem dopasowania i wartością `bootstrapper_confidence` startera. Jeśli komórka nie ma wartości domyślnej (mapa pokazuje `<none>`), wymuś ścieżkę niestandardową z jednozdaniową informacją ("No vetted recommended default exists for `<product_type, language_family>`; we'll walk the full residual interview.").

Domyślna opcja Q0 jest **redakcyjna, a nie niejawna**: podaj rekomendowany starter z góry i poproś o wyraźne potwierdzenie. Użytkownik musi świadomie zaakceptować lub wybrać inną ścieżkę — nigdy nie akceptuj domyślnie bez pytania.

### Step 3 — podejmij decyzję

Załaduj `references/decision-flow.md` i `references/agent-friendly-criteria.md`. Załaduj `references/starter-registry.yaml` i odczytaj tylko karty istotne dla ograniczonego zbioru kandydatów (filtrowanego według `language_family` i `product_type` zgodnie z krokiem A procesu decyzyjnego) — nie wszystkie 25 wpisów, aby ograniczyć koszt promptu.

Wykonaj proces decyzyjny:

- **Ścieżka standardowa** — wybór recommended_defaults jest już liderem; przejdź do kroku E (pokaż `bootstrapper_confidence`) i pomiń filtrowanie/ocenianie.
- **Ścieżka niestandardowa** — wykonaj krok A (filtruj według language_family + product_type + funkcji must-have + zgodności wdrożeniowej), krok B (odrzuć wpisy niespełniające dowolnego kryterium `agent_friendly.*`, z zastrzeżeniem dotyczącym rodziny językowej), krok C (analizuj pozostałe karty, ważąc team_profile + tech_preferences + timeline_budget), krok D (lider + 1–2 alternatywy z `alternatives_to_consider`), krok E (pokaż bootstrapper_confidence).

Jeżeli decyzja jest rzeczywiście wielokomponentowa, nie próbuj kodować całej architektury w pojedynczym `starter_id`. Zdefiniuj osobny komponent dla każdego niezależnie scaffoldowanego artefaktu, wybierz kartę startera dla każdego z nich i nadaj każdemu stabilne `id`, `project_name` oraz względny `target_dir`. Katalogi docelowe nie mogą się pokrywać ani zagnieżdżać. Ustaw `hints.language_family: multi`; top-level `starter_id`, `package_manager` i `project_name` pozostają lustrzanym opisem komponentu głównego dla kompatybilności wstecznej. Jeśli nie da się wskazać co najmniej dwóch konkretnych komponentów, nie używaj wartości `multi`.

Przedstaw wyzwania sokratejskie tam, gdzie wskazuje proces decyzyjny: wariant frameworka Q6 na ścieżce niestandardowej, `tech_preferences` wskazuje starter, który nie przechodzi ≥1 bramki jakości, starter rekomendowany domyślnie nie zawiera funkcji wskazanej przez użytkownika w FR-ach PRD lub wybrany starter ma `bootstrapper_confidence: best-effort` ORAZ użytkownik działa solo (dodatkowe ostrzeżenie).

Format odpowiedzi w rozmowie:

```
Recommendation: <starter_id> — <name>
Confidence:     <verified | first-class | best-effort>

<one-paragraph rationale tying the PRD priors and the user's answers to the lead card>

Alternatives worth a glance:
  - <starter_id_a> — <one-line tradeoff>
  - <starter_id_b> — <one-line tradeoff>

<if a flag was raised during the interview (preference vs quality, missing
 feature, scaffolding-friction warning): a one-line summary of what surfaced,
 how the user resolved it, and whether they're proceeding with a known-friction
 stack>
```

### Step 4 — zapisz przekazanie

Załaduj `references/handoff-schema.md`. Najpierw zbuduj zawartość przekazania w pamięci.

Rozwiąż `package_manager` z `toolchain.package_manager` wybranej karty. Pole jest otwartym stringiem (cokolwiek wskazuje karta — `npm`, `uv`, `poetry`, `bundle`, `gradle`, `cargo`, `go-modules`, `composer`, `dotnet` itd.); w ekosystemach bez zewnętrznego wyboru (np. Go) karta może pominąć to pole — w takim przypadku pomiń je również we frontmatter przekazania.

Rozwiąż `hints.deployment_target` z Q4. Jeśli użytkownik wybrał "I don't know yet — pick the recommended default for me", ustaw pierwszą wartość `deployment_default` karty (NIE dosłowny string `unspecified`).

Ustaw `hints.path_taken`: `standard` lub `custom`. Wypełnij `hints.self_check_answers` 5 wartościami boolowskimi z Q8, jeśli uruchomiono ścieżkę niestandardową; wypisz `null`, jeśli wybrano ścieżkę standardową.

Jeśli stos ma wiele komponentów, dodaj top-level `components` zgodnie z `references/handoff-schema.md`. Każdy `starter_id` musi istnieć w rejestrze. `target_dir` ma być jawną, względną ścieżką bez `.`/`..`, katalogów zastrzeżonych i nakładania się na inny komponent. Ustaw top-level `hints.bootstrapper_confidence` na najsłabszy poziom spośród komponentów (`best-effort` < `first-class` < `verified`).

Sprawdź kolizję:

```bash
test -f context/foundation/tech-stack.md
```

Jeśli plik nie istnieje, zapisz `context/foundation/tech-stack.md` ze zwalidowaną zawartością.

Jeśli plik istnieje, zapytaj użytkownika:

"context/foundation/tech-stack.md already exists. How would you like to proceed?"

Opcje:
- **Overwrite (Recommended)**: Zastąp istniejący tech-stack.md nowym wyborem. Poprzednia wersja zostanie utracona, chyba że została zatwierdzona w repozytorium.
- **Save as tech-stack-v2.md**: Zachowaj historię. Nowy wybór trafi do kolejnego dostępnego slotu tech-stack-vN.md.
- **Abort**: Zakończ bez zapisu. Uzasadnienie rozmowy zostanie zachowane wyłącznie na czacie.

Rekomendowaną opcją domyślną jest tutaj "Overwrite", ponieważ tech-stack-selector jest jednorazową decyzją dla projektu; wiele wersji zwykle oznacza, że użytkownik ponownie rozważa wybór, a w takim przypadku utrata poprzedniego wyboru jest zamierzona. Wersjonowany zapis jest mechanizmem awaryjnym.

Po zapisaniu skopiuj polecenie następnego kroku i ogłoś:

```bash
echo -n "/10x-scaffold-adapter" | pbcopy 2>/dev/null || echo -n "/10x-scaffold-adapter" | clip.exe 2>/dev/null || echo -n "/10x-scaffold-adapter" | xclip -selection clipboard 2>/dev/null || true
```

```powershell
# PowerShell (Windows)
Set-Clipboard "/10x-scaffold-adapter"
```

Wypisz:

```
═══════════════════════════════════════════════════════════
  TECH STACK SELECTED
═══════════════════════════════════════════════════════════

  Starter:        <starter_id>
  Path taken:     <standard | custom>
  Confidence:     <verified | first-class | best-effort>

  ► Hand-off:  context/foundation/tech-stack.md
  ► Next:      /10x-scaffold-adapter  (✓ copied to clipboard)
═══════════════════════════════════════════════════════════
```

ZATRZYMAJ SIĘ. Nie przechodź automatycznie do `/10x-scaffold-adapter` — użytkownik uruchamia go, gdy jest gotowy.

## Output

Zapisywany jest pojedynczy plik: `context/foundation/tech-stack.md` (lub `tech-stack-vN.md`, jeśli wybrano zapis wersjonowany).

Frontmatter zgodny ze schematem w `references/handoff-schema.md`:

```yaml
---
starter_id: <key from registry>
package_manager: <card-prescribed string; may be omitted for some ecosystems>
project_name: <kebab-case>
components: <optional list; required when hints.language_family is multi>
hints:
  language_family: js | python | ruby | java | go | rust | php | dotnet | dart | multi
  team_size: solo | small | mixed
  deployment_target: <starter-prescribed string>
  ci_provider: github-actions | gitlab-ci | circleci | cloudflare-builds
  ci_default_flow: auto-deploy-on-merge | manual-promotion
  bootstrapper_confidence: verified | first-class | best-effort
  path_taken: standard | custom
  quality_override: <bool>
  self_check_answers: <object | null>
  has_auth: <bool>
  has_payments: <bool>
  has_realtime: <bool>
  has_ai: <bool>
  has_background_jobs: <bool>
---

## Why this stack

<one paragraph, ≤ 200 words>
```

## References

- `references/starter-registry.yaml` — kanoniczne karty starterów + mapa `recommended_defaults`.
- `references/residual-interview.md` — rozgałęzienie ścieżki Q0 + przejście Q1–Q8.
- `references/handoff-schema.md` — kontrakt frontmatter `tech-stack.md`.
- `references/agent-friendly-criteria.md` — cztery bramki jakości + zastrzeżenie dotyczące rodziny językowej.
- `references/decision-flow.md` — kroki A–E dla obu ścieżek.

## Krytyczne zabezpieczenia

1. **PRD jest warunkiem wstępnym, a nie rozwiązaniem zastępczym.** Żadnego wbudowanego mini-PRD, żadnego odczytywania rozmowy w poszukiwaniu zastępczych priorów. Plik na dysku jest kontraktem.

2. **Domyślna opcja Q0 jest redakcyjna.** Podaj rekomendację z góry; wymagaj wyraźnego potwierdzenia. Nigdy nie akceptuj domyślnie po cichu.

3. **Ścieżka standardowa kontra niestandardowa jest wiążąca.** Standardowa przechodzi bezpośrednio do rekomendacji + Q4/Q5/nazwy projektu. Niestandardowa wykonuje pełne przejście wraz z samokontrolą Q8. Nie mieszaj ich — ścieżka wybrana przez użytkownika w Q0 jest tym, co rejestruje `hints.path_taken`.

4. **`bootstrapper_confidence` ma charakter informacyjny, nigdy blokujący.** Pewność `best-effort` NIE wyklucza startera z rekomendacji. Jest historycznym sygnałem do selekcji; świeży status dowodów utworzony przez `/10x-scaffold-adapter` ma pierwszeństwo przy wykonaniu.

5. **Rejestr nie jest kodem wykonawczym.** Każdy `starter_id` w handoffie musi istnieć w rejestrze, ale żadna komenda z rejestru nie może trafić bezpośrednio do wykonania. Aktualny kontrakt CLI powstaje dopiero w `/10x-scaffold-adapter` na podstawie oficjalnych źródeł i lokalnego `--help`.

6. **Wyłącznie uniwersalny język.** Brak prywatnych ścieżek vault lub brandingu specyficznego dla organizacji w publikowanej zawartości. `pnpm validate:no-vault-paths` wymusza to w CI. Rejestr recommended-defaults jest z założenia wielojęzyczny; żaden pojedynczy starter nie jest „tą” rekomendowaną ścieżką.

7. **Wewnętrzne etykiety umiejętności pozostają wewnętrzne.** Podczas rozmowy z użytkownikiem nigdy nie odwołuj się do numerów Q (`Q0`, `Q3`, `Q6`), liter kroków (`Step A`, `Step B`, …, `Step E`) ani fraz autorów, takich jak "path-fork", "residual interview", "Socratic moment", "decision flow". Te etykiety organizują dokumenty referencyjne do nawigacji w czasie wykonania; użytkownik nie ma możliwości powiązania ich z czymkolwiek widocznym. Przetłumacz je na prosty język przed wypisaniem — „ten wybór” zamiast „the path-fork”, „pytanie o framework” zamiast „Q6”, „alternatywa, którą warto zaznaczyć” zamiast „a Socratic moment”, „Pominę pytania o audyt funkcji, profil zespołu i preferencje technologiczne” zamiast „I'll skip Q1–Q3”. To samo dotyczy wewnętrznych ścieżek pól w rozmowie: `hints.deployment_target` / `agent_friendly.typed` / `bootstrapper_confidence` to nazwy pól w przekazaniu / rejestrze, a nie frazy, które należy mówić użytkownikowi — „cel wdrożenia”, „czy stos używa jawnych typów”, „jak płynne będzie generowanie szkieletu” to tłumaczenia przeznaczone dla użytkownika.
