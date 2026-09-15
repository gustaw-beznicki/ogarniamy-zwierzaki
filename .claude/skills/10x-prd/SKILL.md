---
name: 10x-prd
description: >
  Generate context/foundation/prd.md from shape-notes.md (or raw notes) against
  the locked PRD schema. Auto-routes to greenfield (10 sections) or brownfield
  (11 sections) template based on context_type in shape-notes.md or cwd
  auto-detection. Use when the user has shaping notes ready and wants a
  schema-conformant PRD written to disk. Trigger phrases: "write the PRD",
  "generate PRD", "create the PRD from notes", "stwórz PRD", "turn notes into a
  PRD", "PRD from shape-notes". Use AFTER /10x-shape, not in place of it.
argument-hint: "[path-to-notes-file]"
allowed-tools:
  - Read
  - Write
  - Bash
  - AskUserQuestion
  - TaskCreate
  - TaskUpdate
---
# PRD: Wygeneruj context/foundation/prd.md z shape-notes

Ta umiejętność jest drugim ogniwem w łańcuchu bootstrap. Dla greenfield: `/10x-shape → /10x-prd → 10x-tech-stack-selector → bootstrapper`. Dla brownfield: `/10x-shape → /10x-prd → 10x-stack-assess → 10x-health-check`. Jej jedyne zadanie: pobrać plik ukształtowanych notatek i wygenerować `context/foundation/prd.md`, który jest zgodny z zablokowanym schematem PRD, kierując każdą lukę do `## Open Questions` zamiast wymyślać treść.

Umiejętność automatycznie kieruje do właściwego szablonu na podstawie `context_type` w danych wejściowych:
- **greenfield** → 11-sekcyjny szablon PRD (produkt budowany od podstaw)
- **brownfield** → 12-sekcyjny szablon PRD (zmiana różnicowa w istniejącym systemie)

Ta umiejętność jest **generatorem dokumentów**, a nie facylitatorem discovery. NIGDY nie wymyśla decyzji domenowych, reguł logiki biznesowej, kryteriów sukcesu ani user stories. Wszystko, czego brakuje w danych wejściowych, trafia dosłownie do `## Open Questions`, aby człowiek mógł to rozstrzygnąć.

Zablokowany schemat, z którym zgodna jest ta umiejętność, znajduje się w `../10x-shape/references/prd-schema.md` (względem tego `SKILL.md`). Przeczytaj go przed wygenerowaniem jakiegokolwiek artefaktu i ponownie sprawdź wygenerowany plik względem niego przed zapisaniem na dysk.

## Kiedy używać, kiedy pominąć

**Użyj, gdy**: użytkownik uruchomił `/10x-shape` (a `context/foundation/shape-notes.md` istnieje z blokiem checkpoint), LUB użytkownik ma surowy plik notatek, który chce przekształcić w szkic PRD, LUB użytkownik wyraźnie prosi o (ponowne) wygenerowanie `context/foundation/prd.md`.

**Pomiń, gdy**: użytkownik nadal prowadzi ideację i nie ma żadnych notatek — najpierw wskaż `/10x-shape`. Pomiń również, gdy użytkownik chce ręcznie *edytować* istniejący PRD — ta umiejętność zapisuje całe pliki; precyzyjne edycje są poza zakresem.

## Relacja z innymi umiejętnościami

- `/10x-shape` — generuje `shape-notes.md`, kanoniczne dane wejściowe. Zawsze preferowane źródło nadrzędne dla tej umiejętności.
- `10x-tech-stack-selector` — odbiorca downstream `prd.md` dla **greenfield**. Odczytuje frontmatter na poziomie produktu jako priory i prowadzi własny pozostały wywiad dotyczący składu zespołu, preferencji językowych, wdrożenia oraz kształtu CI/CD.
- `10x-stack-assess` — odbiorca downstream `prd.md` dla **brownfield**. Ocenia istniejący stack względem bramek jakości przyjaznych agentom.
- `/10x-frame`, `/10x-plan` — niepowiązane; PRD jest artefaktem fundamentowym, a nie planem dla pojedynczej zmiany.

## Początkowa odpowiedź

Gdy ta umiejętność zostanie wywołana:

1. **Jeśli podano argument ścieżki** (np. `/10x-prd @notes/raw.md` lub `/10x-prd context/foundation/shape-notes.md`), przechwyć go jako ścieżkę wejściową. Przejdź do Kroku 1.
2. **Jeśli nie podano argumentu**, domyślnie ustaw ścieżkę wejściową na `context/foundation/shape-notes.md` i przejdź do Kroku 1. Nie pytaj jeszcze — Krok 1 obsługuje przypadek braku danych wejściowych.

## Proces

### Krok 1: Znajdź dane wejściowe

Rozwiąż ścieżkę wejściową:

- Jeśli przekazano argument, użyj go dosłownie (usuń początkowy `@`, jeśli występuje).
- W przeciwnym razie domyślnie użyj `context/foundation/shape-notes.md`.

Przetestuj rozwiązaną ścieżkę:

```bash
test -f "<resolved-path>"
```

Jeśli plik istnieje, przeczytaj go W CAŁOŚCI (bez `limit`/`offset`) i przejdź do Kroku 1.5.

Jeśli plik nie istnieje, zapytaj:

AskUserQuestion:
- question: "Nie znaleziono pliku wejściowego pod `<resolved-path>`. Jak chcesz postąpić?"
  header: "Dane wejściowe?"
  options:
  - label: "Najpierw uruchom /10x-shape (Zalecane)"
    description: "Zatrzymaj się tutaj. Uruchom /10x-shape, aby wygenerować shape-notes.md, a następnie ponownie wywołaj /10x-prd."
  - label: "Wklej surowe notatki"
    description: "Poczekam, aż wkleisz dowolne posiadane notatki. Kontrola cienkich danych wejściowych ostrzeże o brakujących sygnałach."
  - label: "Anuluj"
    description: "Zakończ bez zmian."
  multiSelect: false

Przy „Najpierw uruchom /10x-shape”: wypisz „Zatrzymywanie. Uruchom `/10x-shape`, aby wygenerować shape-notes.md, a następnie ponownie wywołaj `/10x-prd`.” i ZATRZYMAJ.

Przy „Wklej surowe notatki”: wyświetl prompt „Wklej swoje notatki poniżej. Zakończ pustą linią.” i przechwyć tekst użytkownika jako dane wejściowe w pamięci. Przejdź do Kroku 1.5 z tą treścią.

Przy „Anuluj”: ZATRZYMAJ bez zmian.

### Krok 1.5: Określ typ kontekstu

Określ, czy wygenerować PRD greenfield czy brownfield:

1. **Jeśli dane wejściowe mają `context_type:` we frontmatter** — użyj tej wartości bezpośrednio. Potwierdzenie nie jest potrzebne.
2. **Jeśli we frontmatter nie ma `context_type:`** (surowe notatki, wklejone dane wejściowe) — automatycznie wykryj na podstawie cwd:

   Użyj tego samego wykrywania wielosygnałowego co `/10x-shape` (Krok 0.7): sprawdź historię git (Tier 1), lockfile (Tier 2), pliki manifestu (Tier 3) oraz sygnały dodatkowe (katalogi źródłowe, konfiguracje frameworków). Każde trafienie Tier 1 lub Tier 2 → zaproponuj brownfield. Tylko Tier 3 → zaproponuj brownfield z flagą niejednoznaczności. Brak sygnałów → zaproponuj greenfield.

   Potwierdź z użytkownikiem:

   AskUserQuestion:
   - question: "W danych wejściowych nie znaleziono context_type. Na podstawie markerów cwd wygląda to na [greenfield|brownfield]. Zgadza się?"
     header: "Kontekst"
     options:
     - label: "[Wykryty tryb] — poprawnie (Zalecane)"
       description: "Wygeneruj PRD [greenfield|brownfield]."
     - label: "[Inny tryb] — nadpisz"
       description: "Zamiast tego wygeneruj PRD [other]."
     multiSelect: false

Zapisz rozstrzygnięty `context_type` do użycia w Krokach 2 i 3. Przejdź do Kroku 2.

### Krok 2: Oceń dane wejściowe

Oceń dane wejściowe za pomocą heurystyki 0–4 shaped-vs-thin. Każdy sygnał daje 1 punkt:

**Sygnały greenfield:**

1. **Obecny blok frontmatter `checkpoint:`** — najsilniejszy sygnał, że pochodzi to z `/10x-shape`. Szukaj dosłownego klucza `checkpoint:` wewnątrz ogrodzenia YAML frontmatter na początku pliku.
2. **Co najmniej jedno wymaganie w formacie FR-NNN** — użyj grep dla `^- FR-\d{3}: ` (wypunktowana linia, trzycyfrowy indeks z zerami wiodącymi, dwukropek-spacja).
3. **Co najmniej jeden blok Given/When/Then** — użyj grep dla `\*\*Given\*\*` ORAZ `\*\*When\*\*` ORAZ `\*\*Then\*\*` w dowolnym miejscu body.
4. **Jawne uchwycenie logiki biznesowej** — istnieje sekcja `## Business Logic` ORAZ jej pierwsza niepusta linia jest pojedynczym zdaniem deklaratywnym (heurystyka: ≤ 200 znaków, kończy się na `.`, nie jest równa `# TODO: domain rule — see Open Questions` i nie jest pusta/placeholderem).

**Sygnały brownfield** (zastępują sygnał 1, gdy `context_type: brownfield`):

1. **Obecny blok frontmatter `checkpoint:` ORAZ `context_type: brownfield`** — najsilniejszy sygnał, że pochodzi to z `/10x-shape` w trybie brownfield. Sprawdź także sekcję `## Current System` w body.
2–4. Tak samo jak dla greenfield.

Oblicz sumę. Udokumentuj heurystykę wyraźnie w rozmowie, aby przyszły maintainer mógł ją dostroić:

```
Ocena danych wejściowych (heurystyka, 4 sygnały, po 1 punkcie):
  [✓|✗] Blok checkpoint we frontmatter     — <znaleziono|brak>
  [✓|✗] Wymagania w formacie FR-NNN         — <znaleziono N FRs|brak>
  [✓|✗] User stories Given/When/Then        — <znaleziono|brak>
  [✓|✗] Jawna jednozdaniowa reguła biznesowa — <znaleziono|brak>

  Wynik: <N>/4
```

**Wynik ≥ 2**: dane wejściowe są wystarczająco ukształtowane; przejdź do Kroku 3 bez komunikatu.

**Wynik < 2**: wyzwól ostrzeżenie o cienkich danych wejściowych. Nazwij wyraźnie każdy brakujący sygnał (NIE wypisuj ogólnego „twoje notatki są zbyt skąpe” — nazwij, czego brakuje i dlaczego jest to istotne):

```
Te dane wejściowe uzyskały wynik <N>/4 w heurystyce ukształtowania. Brakujące sygnały:

  - <nazwa sygnału>: <jednoliniowa konsekwencja dla wygenerowanego PRD>
  - ...

PRD wygenerowany z cienkich danych wejściowych będzie mieć wiele placeholderów
`# TODO` oraz długą sekcję `## Open Questions`. To prawidłowy stan pośredni,
ale jeśli masz czas, aby najpierw uruchomić /10x-shape, wynikowy PRD będzie
znacznie silniejszy.
```

Następnie zapytaj:

AskUserQuestion:
- question: "Jak chcesz postąpić?"
  header: "Cienkie dane wejściowe"
  options:
  - label: "Najpierw uruchom /10x-shape (Zalecane)"
    description: "Zatrzymaj się tutaj. Użyj /10x-shape, aby uzupełnić brakujące sygnały, a następnie ponownie wywołaj /10x-prd."
  - label: "Mimo to kontynuuj"
    description: "Wygeneruj PRD z tego, co jest. Brakujące elementy trafią dosłownie do ## Open Questions."
  - label: "Anuluj"
    description: "Zakończ bez zmian."
  multiSelect: false

Przy „Najpierw uruchom /10x-shape”: wypisz komunikat przekierowujący i ZATRZYMAJ. Przy „Mimo to kontynuuj”: kontynuuj do Kroku 3 z zapisanym `score < 2`, aby późniejsze kroki wiedziały, że należy oczekiwać TODOs. Przy „Anuluj”: ZATRZYMAJ.

### Krok 3: Wygeneruj PRD

Przeczytaj referencję schematu W CAŁOŚCI jeszcze raz (`../10x-shape/references/prd-schema.md`), aby potwierdzić, że lista pól i nazwy sekcji nie uległy rozjazdowi.

Zbuduj treść PRD **najpierw w pamięci** (jeszcze nie na dysku):

#### 3a. Frontmatter

Wypełnij każde wymagane pole frontmatter zgodnie ze schematem:

- `project` — wyodrębnij z wejściowego frontmatter `project:`, jeśli jest obecne; w przeciwnym razie z nagłówka Title (`# <Project>`); w przeciwnym razie `# TODO: project — see Open Questions`.
- `version` — `1` dla pierwszego PRD zapisanego przez tę umiejętność. Krok kolizji (Krok 4) podnosi tę wartość, jeśli użytkownik wybierze wersjonowany zapis.
- `status` — `draft`. Nigdy nie promuj do `reviewed`/`locked`; to decyzja downstream.
- `created` — dzisiejsza data w formacie `YYYY-MM-DD` (użyj `Bash: date +%Y-%m-%d`).
- `context_type` — `greenfield` lub `brownfield` (z Kroku 1.5).
- `product_type` — pobierz z danych wejściowych, jeśli dostępne; w przeciwnym razie `# TODO: product_type — see Open Questions` (i dodaj wpis Open Question).
- `target_scale`, `timeline_budget` — ta sama zasada. Jeśli dane wejściowe mają to pole, skopiuj je dosłownie; jeśli nie, wygeneruj `# TODO: <field> — see Open Questions` i dodaj pasujące Open Question. Dla brownfield `timeline_budget` używa `delivery_weeks` zamiast `mvp_weeks`.

**NIE wypełniaj** `team_profile`, `tech_preferences` ani `deployment_constraint` we frontmatter PRD, nawet gdy notatki wejściowe je zawierają. Te pola są zbierane przez downstreamowy etap tech-stack-selection (greenfield) lub stack-assessment (brownfield), a nie przez PRD. Jeśli dane wejściowe je zawierają, podsumuj je w komunikacie przekazania z Kroku 5 pod „forward to tech-stack/stack-assess”, aby użytkownik wiedział, że treść jest kierowana dalej, a nie po cichu pomijana — ale NIE umieszczaj ich we frontmatter PRD.

Nazwy kluczy pól są kluczowe zgodnie ze schematem. Wartości pól nie.

#### 3b. Wymagane sekcje (w kolejności schematu)

Lista sekcji zależy od `context_type`:

**Greenfield (10 sekcji):**

Wygeneruj dokładnie tych 10 nagłówków na poziomie `##`, w dokładnie tej kolejności (kontrakt nazw sekcji schematu określa, według czego parsery downstream dzielą dokument):

1. `## Vision & Problem Statement`
2. `## User & Persona`
3. `## Success Criteria` (z `### Primary` / `### Secondary` / `### Guardrails`)
4. `## User Stories`
5. `## Functional Requirements`
6. `## Non-Functional Requirements`
7. `## Business Logic`
8. `## Access Control`
9. `## Non-Goals`
10. `## Open Questions`

**Brownfield (11 sekcji):**

Wygeneruj dokładnie tych 11 nagłówków na poziomie `##`, w dokładnie tej kolejności:

1. `## Current System Overview` — co istnieje teraz: kluczowa architektura, tech stack, baza użytkowników. Ta sekcja nie ma odpowiednika greenfield; ustanawia punkt bazowy, względem którego wszystkie kolejne sekcje opisują zmiany.
2. `## Problem Statement & Motivation` — co jest nieprawidłowe/brakuje, dlaczego teraz. Ujęcie różnicowe: skupia się na luce między stanem obecnym a pożądanym.
3. `## User & Persona` — kogo dotyczy zmiana (istniejących użytkowników + nowych, jeśli są). Dla brownfield podkreśl istniejących użytkowników, których doświadczenie się zmienia.
4. `## Success Criteria` (z `### Primary` / `### Secondary` / `### Guardrails`) — jak wiemy, że zmiana zadziałała. Guardrails powinny wyraźnie uwzględniać istniejące zachowanie, które nie może ulec regresji.
5. `## User Stories` — co zmienia się dla użytkownika. Ujęcie różnicowe: Given/When/Then opisuje nowe zachowanie, z wyraźnymi uwagami o tym, co wcześniej było inne.
6. `## Scope of Change` — co jest modyfikowane/dodawane/usuwane. Jawna różnica: sklasyfikuj każdy element jako `new`, `modified` lub `removed`. Zastępuje to domyślne założenie „wszystko jest nowe” z greenfieldowego `## Functional Requirements`.
7. `## Constraints & Compatibility` — kompatybilność wsteczna, migracja danych, istniejące integracje, zachowane zachowanie. Sekcja specyficzna dla brownfield, która explicite określa zachowanie.
8. `## Business Logic Changes` — dodania/modyfikacje reguł domenowych (nie pełny model domenowy). Jeśli zmiana dotyczy wyłącznie infrastruktury (bez zmiany logiki domenowej), zaznacz to wyraźnie.
9. `## Access Control Changes` — zmiany uprawnień, jeśli występują. Jeśli brak zmian, podaj: „No access control changes.”
10. `## Non-Goals` — czego NIE zmieniamy. Krytyczne dla brownfield: wyraźnie nazywa aspekty istniejącego systemu poza zakresem.
11. `## Open Questions`

**NIE generuj** sekcji `## Data Model`, `## Data Model Changes`, `## Implementation Decisions`, `## Testing Strategy` ani `## Deployment & CI/CD` w żadnym trybie — te obszary nie są częścią schematu PRD. Encje i ich cykle życia wyłaniają się z FRs i User Stories oraz są ustalane podczas wyboru stacku / planowania implementacji, a nie w PRD. Jeśli notatki wejściowe zawierają treść dotyczącą modelu danych lub implementacji, podsumuj ją w komunikacie przekazania z Kroku 5 pod „forward to technical-roadmap”, aby użytkownik wiedział, że jest kierowana dalej, a nie po cichu pomijana — ale NIE generuj tych sekcji w PRD.

#### Zasady treści sekcji (oba tryby)

Dla każdej sekcji:

- **Jeśli dane wejściowe zawierają pasującą treść** — przepisz ją wiernie do sekcji. Zachowaj sformułowania użytkownika. Konwertuj formatowanie tylko wtedy, gdy schemat wymaga określonego kształtu (np. format FR-NNN, Given/When/Then dla user stories, trzy podsekcje Success Criteria). Nie parafrazuj, nie podsumowuj ani nie „ulepszaj” słów użytkownika.
- **Jeśli dane wejściowe zawierają częściową treść** — przepisz to, co jest, a następnie zakończ `# TODO: <what's missing> — see Open Questions` wewnątrz sekcji i dodaj pasujący numerowany wpis w `## Open Questions`.
- **Jeśli dane wejściowe nie zawierają pasującej treści** — wygeneruj tylko nagłówek oraz `# TODO: <section name> — see Open Questions` i dodaj pasujący numerowany wpis w `## Open Questions`.

Jeśli `/10x-shape` zapisał cytaty blokowe Sokratesa pod FRs, zachowaj je dosłownie — są kluczowe dla downstreamowego review.

Jeśli shape-notes.md zawierał blok `## Quality cross-check` (z Kroku 7 `/10x-shape`), odzwierciedl każdą lukę w `## Open Questions` jako numerowany wpis nazywający brakujący element i jego konsekwencję.

**Zasady treści specyficzne dla brownfield:**

- FRs z `Change: preserved` stają się jawnymi elementami zachowania w `## Scope of Change`, a nie w `## Non-Goals`.
- `## Current System Overview` mapuje się z sekcji `## Current System` w shape-notes.
- `## Constraints & Compatibility` mapuje się z sekcji `## Constraints & Preserved Behavior` w shape-notes.
- Konwencja ujęcia różnicowego: sekcje opisują to, co się zmienia, a nie cały system. „The auth model adds Google OAuth alongside existing email login” — nie „The system supports email login and Google OAuth.”

**Twarda zasada — nigdy nie wymyślaj**: jeśli dane wejściowe nie zawierają jednozdaniowej reguły biznesowej, sekcja `## Business Logic` / `## Business Logic Changes` MUSI brzmieć `# TODO: domain rule — see Open Questions`, a Open Questions MUSI zawierać „What is the one-sentence business rule? — TBD by user. Block: yes (PRD is hollow until resolved).” Nie pisz zastępczej reguły. Nie „ekstrapoluj” reguły z nazw encji pojawiających się w FRs lub User Stories. Cały sens tej umiejętności polega na ujawnianiu luk, a nie ich maskowaniu.

Ta sama zasada dotyczy: kryteriów sukcesu, user stories, priorytetów FR, celów NFR, kontroli dostępu, non-goals. Jeśli czegoś nie ma w danych wejściowych, trafia do Open Questions.

#### 3c. Samokontrola przed zapisem

Przed jakimkolwiek zapisem na dysk wykonaj przebieg samokontroli względem listy wymaganych sekcji schematu ORAZ lint na poziomie treści dla wycieku technicznego:

**Kontrole strukturalne:**

1. Sparsuj treść PRD w pamięci. Wyodrębnij każdy nagłówek `## `.
2. Porównaj z kanoniczną listą sekcji dla aktywnego `context_type` (10 dla greenfield, 11 dla brownfield). Zweryfikuj, że WSZYSTKIE sekcje są obecne, w kolejności, z dokładną pisownią. PRD NIE może zawierać `## Data Model` ani `## Data Model Changes` — te sekcje zostały wycofane.
3. Zweryfikuj, że frontmatter deklaruje wszystkie wymagane klucze zgodnie ze schematem (`project`, `version`, `status`, `created`, `context_type`, `product_type`, `target_scale`, `timeline_budget`).
4. Zweryfikuj, że `## Success Criteria` zawiera podsekcje `### Primary`, `### Secondary`, `### Guardrails` (lub, jeśli ich brakuje, że są oznaczone jako TODO z odpowiadającymi wpisami Open Questions).

**Lint na poziomie treści dla wycieku technicznego:**

5. Przeskanuj treści wszystkich sekcji na poziomie `##` (z wyłączeniem brownfieldowego `## Current System Overview`, gdzie nazwanie istniejącego stacku jest dozwolone) pod kątem tokenów wskazujących, że szczegóły implementacyjne wyciekły do PRD. Traktuj każde trafienie jako wyciek, chyba że jest częścią dosłownego cytatu użytkownika jawnie kierowanego do Open Questions:

   - **Nazwy vendorów / usług hostowanych**: `OpenRouter`, `Stripe`, `Auth0`, `Supabase`, `Firebase`, `Vercel`, `Cloudflare`, `AWS`, `GCP`, `Azure`, `OpenAI`, `Anthropic` itd. (dowolny produkt/usługa będąca nazwą własną).
   - **Notacja schema / ORM**: `(FK)`, `nullable`, sufiksy kolumn `_hash`, `_at` prezentowane jako listy pól, `password_hash`, `cascade`, `soft-delete`, `hard-delete`, `migration`, `backfill`.
   - **Lokalizacja runtime**: `client-side`, `server-side`, `on the edge`, `in the cache`, `in the worker`.
   - **Mechanizm egzekwowania**: `per IP`, `per user-agent`, `token bucket`, `rate-limit per <axis>`.
   - **Element UI** (gdy jest użyty do określenia NFR, a nie user story): `spinner`, `progress bar`, `streaming response`, `modal`, `toast`.
   - **Transport / protokół**: `WebSocket`, `gRPC`, `GraphQL`, `REST endpoint`, `webhook`, `SSE`.
   - **Czasowniki implementacyjne w regułach domenowych**: „the LLM does X”, „the SRS library decides Y”, „the database stores Z” (nazywanie komponentu wykonującego regułę zamiast określenia samej reguły).

   Dla każdego trafienia wygeneruj ustrukturyzowane ostrzeżenie. NIE przepisuj po cichu — przerwij zapis, aby użytkownik mógł zobaczyć, co wyciekło.

Jeśli jakakolwiek kontrola strukturalna LUB lint nie powiedzie się, **przerwij zapis** i zgłoś:

```
Samokontrola generowania PRD NIE POWIODŁA SIĘ:

  Strukturalne:
    - Brakująca sekcja: <name>
    - Sekcja poza kolejnością: <name> (oczekiwana pozycja N, znaleziona pozycja M)
    - Brakujący klucz frontmatter: <key>
    - Obecna wycofana sekcja: <name>

  Wyciek techniczny (lint treści):
    - <section name>: "<offending phrase>" — <category, e.g. vendor name / schema notation / runtime location>
    - ...

PRD NIE został zapisany. W przypadku błędów strukturalnych: schemat i generator
uległy rozjazdowi — ponownie przeczytaj ../10x-shape/references/prd-schema.md i uzgodnij.
W przypadku wycieków: notatki wejściowe zawierają szczegóły implementacyjne, których PRD
nie posiada. Albo (a) przepisz problematyczne sformułowania jako obserwowalne z zewnątrz
właściwości / decyzje zakresowe i uruchom ponownie, albo (b) przenieś wyciekającą treść do
bloków `## Forward: ...` shape-notes, aby wykorzystała ją umiejętność downstream.
```

Następnie ZATRZYMAJ. Nie przechodź do Kroku 4.

Jeśli wszystkie kontrole przejdą pomyślnie, przejdź do Kroku 4 ze zwalidowaną treścią.

### Krok 4: Sprawdzenie kolizji

```bash
test -f context/foundation/prd.md
```

Jeśli plik nie istnieje, zapisz do `context/foundation/prd.md` i przejdź do Kroku 5.

Jeśli plik istnieje, zapytaj:

AskUserQuestion:
- question: "context/foundation/prd.md już istnieje. Jak chcesz postąpić?"
  header: "Kolizja"
  options:
  - label: "Zapisz jako prd-vN.md (Zalecane)"
    description: "Zachowaj historię. Nowy PRD trafi do kolejnego dostępnego slotu prd-vN.md. Nieversionowany prd.md pozostanie bez zmian."
  - label: "Nadpisz prd.md"
    description: "Zastąp istniejący prd.md. Poprzednia wersja zostanie utracona (chyba że została przez Ciebie zatwierdzona)."
  - label: "Przerwij"
    description: "Zakończ bez zapisów. Bez rozstrzygnięcia kolizji."
  multiSelect: false

Przy „Zapisz jako prd-vN.md”: wybierz `N`, skanując `context/foundation/` pod kątem plików pasujących do `prd-v*.md`. Traktuj niewersjonowany `prd.md` jako v1. Następny slot to `N = (max existing N or 1) + 1`. Zapisz zwalidowaną treść do `context/foundation/prd-v<N>.md` i podnieś pole frontmatter `version:` wewnątrz treści do `<N>`. Przejdź do Kroku 5.

Przy „Nadpisz prd.md”: zapisz zwalidowaną treść do `context/foundation/prd.md`. Zachowaj `version: 1` (nadpisanie jest zastąpieniem, a nie nową wersją). Przejdź do Kroku 5.

Przy „Przerwij”: ZATRZYMAJ bez zapisów.

### Krok 5: Przekaż dalej

Po zapisaniu podsumuj, co zostało wygenerowane:

```
═══════════════════════════════════════════════════════════
  PRD WYGNEROWANY
═══════════════════════════════════════════════════════════

  Projekt:          [project from frontmatter]
  Typ kontekstu:    [greenfield | brownfield]
  Ścieżka:          [context/foundation/prd.md | context/foundation/prd-vN.md]
  Sekcje schematu:  [11 / 11 | 12 / 12] obecne
  Frontmatter:      <K populated, M as TODO>  (łącznie 8 kluczy)
  Open Questions:   <count> wpisów

  Sekcje w pełni wypełnione z danych wejściowych:
    - <list of section names with non-trivial content>

  Sekcje oznaczone TODO (zobacz Open Questions):
    - <list of section names with TODO placeholders>

═══════════════════════════════════════════════════════════
```

Następnie skopiuj do schowka komendę następnego kroku i ogłoś:

**Greenfield:**

```bash
echo -n "/10x-tech-stack-selector" | pbcopy 2>/dev/null || echo -n "/10x-tech-stack-selector" | clip.exe 2>/dev/null || echo -n "/10x-tech-stack-selector" | xclip -selection clipboard 2>/dev/null || true
```

```powershell
# PowerShell (Windows)
Set-Clipboard "/10x-tech-stack-selector"
```

```
► Dalej:   /10x-tech-stack-selector  (✓ skopiowano do schowka)

          Pobiera informacje o składzie zespołu, preferencjach językowych,
          liście technologii, których należy unikać, celu wdrożenia oraz
          kształcie pipeline CI/CD. Żadne z nich celowo nie znajduje się
          w tym PRD — PRD opisuje produkt, następny krok opisuje,
          jak go zbudować.
```

**Brownfield:**

```bash
echo -n "/10x-stack-assess" | pbcopy 2>/dev/null || echo -n "/10x-stack-assess" | clip.exe 2>/dev/null || echo -n "/10x-stack-assess" | xclip -selection clipboard 2>/dev/null || true
```

```powershell
# PowerShell (Windows)
Set-Clipboard "/10x-stack-assess"
```

```
► Dalej:   /10x-stack-assess  (✓ skopiowano do schowka)

          Ocenia Twój istniejący stack względem bramek jakości przyjaznych
          agentom i tworzy plan kompensacyjny. Następnie /10x-health-check
          audytuje stan zależności, zestaw testów oraz pokrycie CI/CD.
          Żadne z nich celowo nie znajduje się w tym PRD — PRD opisuje CO
          się zmienia, kolejne kroki oceniają, CZY Twój istniejący system
          jest gotowy.
```

Jeśli notatki wejściowe zawierały perspektywiczne zagadnienia (preferencje tech stacku, notatki implementacyjne, wskazówki wdrożeniowe), wymień je krótko, aby użytkownik wiedział, że są kierowane do następnego kroku, a nie pomijane:

```
  Przekaż do następnego kroku (nie w PRD):
    • [one-line summary per detected item]
```

Pomiń cały blok, jeśli dane wejściowe nie zawierały żadnego z tych elementów.

ZATRZYMAJ. Nie łącz automatycznie z inną umiejętnością.

## Kluczowe guardraile

1. **Generator, nie autor.** Ta umiejętność zapisuje całe pliki na podstawie danych wejściowych już zatwierdzonych przez użytkownika. Nie wymyśla logiki biznesowej, kryteriów sukcesu, user stories ani priorytetów FR. Brakująca treść trafia dosłownie do `## Open Questions`. Sekcja `## Business Logic` PRD jest obszarem podlegającym najsurowszej kontroli: jeśli dane wejściowe nie zawierają jednozdaniowej reguły, sekcja brzmi `# TODO: domain rule — see Open Questions`. Bez wyjątków.

2. **Schemat jest kontraktem.** `../10x-shape/references/prd-schema.md` definiuje klucze frontmatter, nazwy sekcji i kolejność sekcji. Czytaj go ponownie przy każdym wywołaniu. Ponownie waliduj PRD w pamięci względem niego w Kroku 3c przed zapisem. Rozjazd między tą umiejętnością a schematem to tryb awarii, któremu ta umiejętność ma zapobiegać.

3. **Otwartość stacku jest wiążąca — i szersza niż same nazwy stacku.** Zabronione słownictwo w wygenerowanym PRD obejmuje siedem kategorii, nie tylko frameworki:

   - **Frameworki, bazy danych, platformy hostingowe, konkretne biblioteki** — pierwotna zasada.
   - **Nazwy vendorów / usług hostowanych** — OpenRouter, Stripe, Auth0, Supabase, Firebase, Vercel, Cloudflare, AWS/GCP/Azure, OpenAI, Anthropic oraz każdy inny produkt lub usługa będące nazwą własną.
   - **Notacja schema / ORM** — listy na poziomie pól, `(FK)`, `nullable`, kolumny `_hash`, `password_hash`, `cascade-delete`, `soft-delete`, `hard-delete`, `migration`, `backfill`. (Encje naturalnie pojawiają się w FRs i User Stories; schema na poziomie kolumn jest zagadnieniem downstream.)
   - **Lokalizacja runtime** — `client-side`, `server-side`, `on the edge`, `in the cache`, `in the worker`. PRD opisuje to, co musi być prawdą na zewnętrznej granicy produktu, a nie miejsce w stacku, gdzie jest to egzekwowane.
   - **Mechanizm egzekwowania** — `per IP`, `per user-agent`, `token bucket`, `rate-limit per <axis>`. NFR jest właściwością; mechanizm jest decyzją projektową downstream.
   - **Element UI w NFRs** — `spinner`, `progress bar`, `streaming response`, `modal`, `toast`. NFRs określają obserwowalną dla użytkownika jakość (np. „continuous feedback during long operations”); element UI jest zagadnieniem downstream.
   - **Transport / protokół** — `WebSocket`, `gRPC`, `GraphQL`, `REST endpoint`, `webhook`, `SSE`. PRD opisuje przepływ informacji tak, jak doświadcza go użytkownik, a nie format komunikacji.

   Frontmatter PRD dotyczy wyłącznie poziomu produktu (`product_type`, `target_scale`, `timeline_budget` + metadata); rodzina języka, frameworki, wdrożenie, profil zespołu i każda lista technologii, których należy unikać, należą do kroku downstream (tech-stack-selector dla greenfield, stack-assess dla brownfield), a NIE do PRD. Jeśli dane wejściowe zawierają zabronione słownictwo, pozostaw je w blokach `## Forward: ...` shape-notes, aby wykorzystał je krok downstream — NIE przenoś go do frontmatter lub sekcji PRD. Wyjątek: brownfieldowe `## Current System Overview` może nazywać istniejący stack i vendorów, ponieważ opisuje stan obecny, a nie wybór stacku. Lint treści z Kroku 3c mechanicznie egzekwuje ten guardrail.

4. **Kolizje sprzyjają historii.** Prompt dotyczący kolizji rekomenduje wersjonowany zapis (`prd-vN.md`) zamiast nadpisania. Utracone wcześniejsze wersje są nieodwracalnym trybem awarii; zduplikowany plik w `context/foundation/` nim nie jest.

5. **Samokontrola przerywa przy rozjeździe.** Jeśli PRD w pamięci nie zawiera sekcji, ma sekcję w złej kolejności lub nie ma klucza frontmatter, zapis jest PRZERYWANY — nie naprawiany po cichu. Błąd wskazuje konkretny rozjazd, aby maintainer mógł uzgodnić schemat i umiejętność.

6. **Wyłącznie uniwersalny język.** Brak odniesień do 10xDevs / cohort / certification w jakimkolwiek komunikacie dla użytkownika lub artefakcie zapisywanym na dysk. Ta umiejętność jest generycznym generatorem PRD.

7. **Nigdy nie łącz automatycznie.** Przekazanie dalej jest ogłoszeniem, a nie wywołaniem. Użytkownik wybiera, kiedy (i czy) uruchomić następny krok (10x-tech-stack-selector dla greenfield, 10x-stack-assess dla brownfield). Automatyczne łączenie pominęłoby review wygenerowanego PRD przez człowieka.

## Uwagi

- To umiejętność **generatora dokumentów**. Wynikiem jest `context/foundation/prd.md` (lub `prd-vN.md`), kropka.
- Referencja schematu (`../10x-shape/references/prd-schema.md`) jest jedynym źródłem prawdy. Każda nazwa pola, nazwa sekcji lub klucz frontmatter, do którego odwołuje się ten body, MUSI istnieć w dokumencie schematu — jeśli nie istnieje, najpierw popraw dokument schematu.
- Heurystyka cienkich danych wejściowych (Krok 2) jest celowo konserwatywna. Fałszywe pozytywy (ostrzeżenie dla ukształtowanych danych wejściowych) można naprawić przez override „Proceed anyway”; fałszywe negatywy (ciche generowanie z cienkich danych wejściowych) tworzą puste PRD, które wprowadzają użytkownika w błąd. Dostrajaj heurystykę tak, aby ostrzegała częściej, a nie rzadziej.
- Wzorzec `# TODO: <field-name> — see Open Questions` jest kluczowy. Narzędzia downstream (umiejętności review, 10x-tech-stack-selector / 10x-stack-assess) mogą używać grep dla `^# TODO: `, aby policzyć nierozstrzygnięte luki i zdecydować, czy PRD jest gotowy do review.