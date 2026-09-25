---
name: 10x-roadmap
description: >
  Milestone-driven roadmap manager: open an outcome-scoped milestone from
  source materials (primary: the PRD), decompose it into vertical end-to-end
  slices in context/foundation/roadmap.md, track the milestone as connected
  slices complete, close it when every slice is done, and loop into the next
  milestone. Use AFTER /10x-prd (and after the tech-stack selection /
  bootstrap step, when applicable). Trigger phrases: "write the roadmap",
  "generate roadmap", "create the roadmap from PRD", "stwórz roadmapę",
  "open a milestone", "close the milestone", "milestone status", "what
  should I build first", "what's next on the roadmap". Do NOT use for
  per-change planning — that's /10x-plan's job.
---
# Mapa drogowa: mapa drogowa oparta na kamieniach milowych dla context/foundation/roadmap.md

Ta umiejętność jest pomostem między **produktem** (PRD lub innymi materiałami źródłowymi) a **planowaniem pojedynczych zmian** (`/10x-plan`) oraz działa jako **menedżer projektu na poziomie kamienia milowego**. Praca jest grupowana w **kamienie milowe**: partię powiązanych przekrojów o zakresie zdefiniowanym przez rezultat, z dokładnie jednym otwartym naraz, śledzonym w samym `roadmap.md`. Każde wywołanie najpierw przełącza się według stanu kamienia milowego (Krok 0): jeśli żaden kamień milowy nie jest otwarty, umiejętność prosi o materiały źródłowe i otwiera jeden; jeśli kamień milowy jest aktywny, raportuje status i rekomenduje następny ruch; jeśli każdy przekrój jest ukończony, zamyka kamień milowy i przechodzi do otwarcia następnego — na podstawie zaktualizowanych materiałów źródłowych lub własnego opisu użytkownika.

W obrębie otwartego kamienia milowego zadanie dekompozycji pozostaje bez zmian: przeczytaj materiały źródłowe, automatycznie zbadaj stan bazowy kodu, **wywnioskuj zdecydowaną propozycję sekwencjonowania** (główny cel, przekrój gwiazdy przewodniej, obszary inwestycji, najważniejsza blokada), ujawnij tylko rzeczywistą niepewność, której artefakty nie mogą rozstrzygnąć, i wygeneruj `context/foundation/roadmap.md`, który wymienia pionowe, widoczne dla użytkownika przekroje w kolejności zależności — gotowe do przekazania do `/10x-plan <change-id>`.

## Warstwa kamieni milowych — maszyna stanów znajduje się w pliku referencyjnym

Cykl życia kamienia milowego (stany, reguły wykrywania, przejścia, niezmienniki) jest określony w **`references/milestone-state.md`**, celowo utrzymywanym poza tym plikiem. **Czytaj go tylko wtedy, gdy wywołanie działa na poziomie kamienia milowego** — przy pierwszym uruchomieniu, wznowieniu/sprawdzeniu statusu, zamknięciu kamienia milowego lub otwarciu następnego. Czysta ponowna dekompozycja już otwartego kamienia milowego go nie wymaga.

Dwa fakty potrzebne przed podjęciem decyzji, czy go załadować:

- Stan jest **wyprowadzany wyłącznie z `roadmap.md`** (frontmatter `milestone_id` / `milestone_status` + statusy elementów). Nie ma pomocniczego pliku stanu.
- Identyfikatory kamieni milowych mają postać `M-<seq>` z `milestone_id` w kebab-case; kamienie milowe są **zdefiniowane przez rezultat, nigdy ograniczone czasowo** — kamień milowy zamyka się, gdy jego przekroje mają status `done`, a nie gdy mija data. To nie jest sprint.

**Podejście: zdecydowany rekomendujący, zwięzły wywiad.** Umiejętność działa jak starszy tech lead, który przeczytał PRD, zbadał kod i przychodzi z rekomendacją — ale nadal pyta człowieka o 2–3 kluczowe decyzje przed zatwierdzeniem. Reguły wywiadu (limit 3 pytań, mocne Recommends, bez chochołów, wyjątek custom-MVP) są określone raz, w Kroku 5.

Jest to umiejętność **dekompozycji + sekwencjonowania**, a nie niskopoziomowego planowania. NIGDY nie wybiera frameworków, ścieżek plików, schematów, bibliotek ani szczegółów implementacji — należą one do `/10x-plan`. NIGDY nie przypisuje estymacji czasu, rozmiarów t-shirtowych, punktów ani dat kalendarzowych dla ludzi — wykonanie agentowe jest nieliniowe, a estymacje oparte na budżecie czasowym byłyby kłamstwem. To, co ROBI, to: nazywa przekroje, porządkuje je według zależności i zdefiniowanego celu, ujawnia blokady oraz kieruje otwarte pytania tam, gdzie można je rozstrzygnąć.

Umiejętność jest **AI-native** na cztery konkretne sposoby: (1) wyraża kolejność jako graf zależności, a nie kalendarz; (2) oznacza przekroje, które mogą być wykonywane równolegle przez osobne uruchomienia agentów; (3) przekazuje „blokujące niewiadome” wyżej, gdzie człowiek może je rozstrzygnąć, zamiast pozwalać im po cichu przeniknąć do implementacji; (4) inwentaryzuje istniejący kod za pomocą subagentów zamiast pytać użytkownika, co już jest na miejscu.

## Kiedy używać, kiedy pomijać

**Użyj, gdy**: użytkownik chce otworzyć kamień milowy i go zdekomponować (typowe pierwsze źródło: nietrywialny `context/foundation/prd.md` z wypełnionymi FR i historyjkami użytkownika), sprawdzić status kamienia milowego/mapy drogowej albo zamknąć ukończony kamień milowy i otworzyć następny. Typowe wyzwalacze: właśnie ukończono `/10x-prd`, właśnie ukończono bootstrap, powrót do projektu z pytaniem „co dalej” albo zarchiwizowano wszystkie przekroje mapy drogowej.

**Pomiń, gdy**: PRD jest pusty (duże `## Open Questions`, `# TODO: domain rule`) — najpierw wskaż `/10x-prd` (lub wcześniejsze `/10x-shape`); mapa drogowa z pustego PRD odziedziczy tę pustkę. Pomiń także, gdy użytkownik chce szczegółowo zaplanować *pojedynczą* zmianę — do tego służy `/10x-plan`. Mapa drogowa jest liczbą mnogą; plan jest liczbą pojedynczą.

## Relacja z innymi umiejętnościami

- `/10x-shape` i `/10x-prd` — tworzą nadrzędny PRD konsumowany przez tę umiejętność. Jeśli `shape-notes.md` zawiera blok `## Forward: technical-roadmap` (gdzie shape odkłada treści związane z mapą drogową), ta umiejętność je podnosi.
- `10x-tech-stack-selector` — działa między `/10x-prd` a tą umiejętnością w łańcuchu bootstrap. Jeśli istnieje `context/foundation/tech-stack.md`, ta umiejętność czyta go jako dane wejściowe do wyprowadzenia `## Foundations` (szkielet auth, szkielet wdrożenia, obserwowalność — wszystko, co implikował krok wyboru stosu technologicznego) oraz do skrócenia badań stanu bazowego dla warstw już zadeklarowanych.
- `/10x-plan` — konsument downstream. Użytkownik wybiera element mapy drogowej i wywołuje `/10x-plan <change-id>`; ta umiejętność tworzy folder zmiany, przygotowuje szczegółowy plan i zmienia `Status` dopasowanego elementu mapy drogowej na `planning`. Mapa drogowa NIE tworzy z wyprzedzeniem folderów zmian; jeden przekrój może wygenerować wiele zmian, gdy `/10x-plan` odkryje, że element nadal jest zbyt szeroki (tylko pierwszy przesuwa wspólny status elementu).
- `/10x-implement` (oraz jego autonomiczny odpowiednik `/10x-goal-implement`) — dalej downstream. Gdy implementacja *zaczyna się* dla zmiany, której `Change ID` odpowiada elementowi mapy drogowej, zmienia `Status` tego elementu na `in-progress` — otwarty odpowiednik zmiany na `done` wykonywanej przez `/10x-archive`. Ta umiejętność sama nadal generuje tylko `proposed` / `ready` / `blocked`; pośrednie stany cyklu życia (`planning`, `in-progress`) są teraz zapisywane downstream w miarę przechodzenia zmiany przez plan → implementację. Każda zmiana downstream dopasowuje według `Change ID`, jest best-effort (brak dopasowania oznacza ciche pominięcie) i tylko postępowa (nigdy nie cofa bardziej zaawansowanego statusu).
- `/10x-archive` — zamyka pętlę na końcu. Gdy zmiana, której `Change ID` odpowiada elementowi mapy drogowej, zostaje zarchiwizowana, `/10x-archive` zmienia `Status` tego elementu na `done` (w `## At a glance` i w bloku treści elementu) oraz dodaje wpis do `## Done`. Ta umiejętność nigdy nie wypełnia z góry `## Done`; `/10x-archive` jest jego jedynym autorem.
- `/10x-frame`, `/10x-research` — ortogonalne. Działają na pojedynczej zmianie, nie na mapie drogowej.

## Odpowiedź początkowa — Krok 0: dyspozycja stanu kamienia milowego

Gdy ta umiejętność jest wywoływana, przeprowadź dyspozycję PRZED wykonaniem jakiejkolwiek pracy dekompozycyjnej:

1. **Zbadaj stan kamienia milowego** (tanio, plik referencyjny nie jest jeszcze potrzebny):

   ```bash
   test -f context/foundation/roadmap.md && head -20 context/foundation/roadmap.md
   ```

   - Brak pliku lub plik obecny bez klucza frontmatter `milestone_id` → **brak otwartego kamienia milowego** (pierwsze uruchomienie albo starsza mapa drogowa).
   - `milestone_status: open` → kamień milowy aktywny lub gotowy do zamknięcia (zależy od statusów elementów — przeczytaj cały plik, aby to ustalić).
   - `milestone_status: done` → poprzedni kamień milowy zamknięty, następny jeszcze nieotwarty.

2. **O ile kamień milowy nie jest otwarty z nieukończonymi elementami, a użytkownik wyraźnie nie poprosił o świeżą dekompozycję** — tj. przy pierwszym uruchomieniu, adaptacji starszego formatu, sprawdzeniu statusu/następnego ruchu, zamknięciu albo otwieraniu następnego kamienia milowego — **przeczytaj teraz `references/milestone-state.md`** i wykonaj odpowiadające przejście. Przejścia delegują z powrotem do Kroków 1–10 poniżej, gdy potrzebna jest dekompozycja.

3. **Jeśli kamień milowy jest otwarty, a użytkownik poprosił o regenerację dekompozycji** (lub przekazał argument ścieżki źródłowej, np. `/10x-roadmap @path/to/prd.md`), pomiń plik referencyjny: przechwyć ścieżkę (usuń wiodące `@`), w przeciwnym razie domyślnie użyj `context/foundation/prd.md` i przejdź bezpośrednio do Kroku 1. Regeneracja zachowuje frontmatter kamienia milowego oraz `## Milestone History` dosłownie i przenosi statusy elementów według `Change ID` (wyłącznie do przodu).

## Interaktywne prompty — niezależne od hosta

Za każdym razem, gdy procedura mówi *„zapytaj użytkownika”*, użyj dowolnego narzędzia do strukturalnych pytań interaktywnych udostępnianego przez agenta hosta (twój asystent kodowania AI → dowolne narzędzie pytające użytkownika pytanie z opisanymi opcjami; na innych hostach dowolne narzędzie pytające użytkownika pytanie z opisanymi opcjami). Jeśli żadne nie jest dostępne, przejdź do zwykłej wiadomości konwersacyjnej z listą opisanych opcji — nie blokuj procedury. Przy pierwszym pytaniu podaj, które narzędzie wybrałeś (lub że przeszedłeś do zwykłego czatu), aby użytkownik mógł cię skorygować.

Bloki pytań pojawiają się w Krokach 1, 3, 4, 5 i 9 oraz w przejściach kamieni milowych w `references/milestone-state.md` — są to krótkie uporządkowane wybory. Krok 5 zadaje każdą kotwicę jako osobne pytanie strukturalne; jego synteza podsumowująca jest zwykłym markdownem (bez dodatkowego pytania).

## Równoległe badanie stanu bazowego — niezależne od hosta

Za każdym razem, gdy procedura mówi o użyciu subagentów lub uruchamianiu równoległych sond, użyj dowolnego narzędzia do badań w tle / tworzenia zadań udostępnianego przez hosta (twój asystent kodowania AI → izolowany agent działający w tle, który zwraca podsumowanie; na innych hostach dowolne narzędzie tworzące izolowanego agenta i zwracające podsumowanie), rozdzielając sondy w jednym zbiorczym wywołaniu. Jeśli takie narzędzie nie istnieje, uruchom te same sondy sekwencyjnie w głównym kontekście. Każda ścieżka musi zwrócić ten sam kształt podsumowania stanu bazowego z dowodami plikowymi.

## Proces

### Krok 1: Pozyskaj i przeczytaj materiały źródłowe

**Podczas otwierania kamienia milowego** (pierwsze uruchomienie lub przejście do następnego kamienia milowego z `references/milestone-state.md`) zapytaj, na czym ma bazować kamień milowy — nie zakładaj, ale rekomenduj PRD:

Zapytaj użytkownika:

- question: "Jakie są materiały źródłowe dla tego kamienia milowego?"
  header: "Źródła"
  options:
  - label: "PRD w context/foundation/prd.md (Zalecane)"
    description: "Standardowa ścieżka: kamień milowy określony na podstawie FR i historyjek użytkownika z PRD. Najpierw uruchom /10x-prd, jeśli jeszcze nie istnieje."
  - label: "Inne dokumenty — podam ścieżki"
    description: "Specyfikacje, briefy, dokumenty badawcze. Przekroje będą śledzić ich treść, zapisaną jako kotwice zakresu w karcie kamienia milowego."
  - label: "Sam opiszę kamień milowy"
    description: "Opis swobodny, bez dokumentu. Wyodrębnię z niego kotwice zakresu MS-NN, do których będą odwoływać się przekroje."
  - label: "Anuluj"
    description: "Zakończ bez zmian."
  multiSelect: false

Dla kolejnych kamieni milowych `references/milestone-state.md` doprecyzowuje te opcje (zaktualizowany PRD kontra następna transza tego samego PRD). Gdy wywołanie zawierało jawny argument ścieżki, pomiń pytanie i użyj tej ścieżki.

Rozwiąż i zweryfikuj ścieżki wejściowe:

```bash
test -f "<resolved-path>"
```

Jeśli plik istnieje, **przeczytaj go W CAŁOŚCI** (bez `limit`/`offset`). Jeśli użytkownik wybrał samodzielny opis, przechwyć zamiast tego jego opis dosłownie — stanie się on kartą `## Milestone` z ponumerowanymi kotwicami zakresu `MS-NN`, a kontrola gotowości PRD z Kroku 3 zostanie zastąpiona kontrolą kotwic (< 2 możliwe do wyodrębnienia kotwice `MS-NN` → poproś użytkownika o doprecyzowanie opisu, a następnie ZATRZYMAJ, jeśli nie może).

Jeśli wskazany plik nie istnieje, zapytaj za pomocą wybranego narzędzia pytań interaktywnych:

Zapytaj użytkownika:

- question: "Nie znaleziono źródła w `<resolved-path>`. Jak chcesz kontynuować?"
  header: "Dane wejściowe?"
  options:
  - label: "Najpierw uruchom /10x-prd (Zalecane)"
    description: "Zatrzymaj tutaj. Uruchom /10x-prd, aby utworzyć prd.md, a następnie ponownie wywołaj /10x-roadmap."
  - label: "Podaj inną ścieżkę"
    description: "Poczekam, aż podasz ścieżkę."
  - label: "Anuluj"
    description: "Zakończ bez zmian."
  multiSelect: false

Po wybraniu „Najpierw uruchom /10x-prd”: wyświetl komunikat przekierowujący i ZATRZYMAJ.

### Krok 2: Przeczytaj dane uzupełniające (best effort)

Przeczytaj poniższe, jeśli istnieją; w przeciwnym razie odnotuj ich brak i kontynuuj:

- `context/foundation/shape-notes.md` — szukaj sekcji `## Forward: technical-roadmap`. Jeśli jest obecna, podnieś jej wypunktowania dosłownie jako kandydackie dane wejściowe mapy drogowej (użytkownik już umieścił je tam podczas shaping).
- `context/foundation/tech-stack.md` — informuje sekcję `## Foundations` ORAZ skraca sondy stanu bazowego (warstwa już tutaj zadeklarowana jest raportowana jako „zgodnie z tech-stack.md” bez ponownego badania).
- `context/foundation/roadmap.md` — jeśli już istnieje, zachowaj go dla Kroku 9 (obsługa kolizji). NIE modyfikuj go jeszcze.
- `context/foundation/lessons.md` — jeśli istnieje, przeskanuj pod kątem reguł dotyczących kolejności lub gotowości (np. „zawsze dostarczaj najpierw najbardziej ryzykowny przekrój”). Traktuj je jako priory, nie dogmat.

### Krok 3: Kontrola gotowości PRD

Przed generowaniem oceń PRD według heurystyki gotowości 0–4. Każdy sygnał wnosi 1 punkt:

1. **Vision & Problem Statement jest nietrywialne** — sekcja istnieje, zawiera ≥ 2 zdania i NIE zawiera `# TODO`.
2. **Co najmniej jedna wypełniona historyjka użytkownika** — istnieje nagłówek `### US-NN:` z blokiem Given/When/Then pod nim (nie `# TODO`).
3. **Co najmniej jeden FR `must-have`** — istnieje linia pasująca do `^- FR-\d{3}: .* (P|p)riority: must-have$`.
4. **Business Logic wypełnione** — pierwsza niepusta linia sekcji `## Business Logic` jest zdaniem deklaratywnym (nie `# TODO: domain rule`).

Wyraźnie udokumentuj heurystykę w rozmowie:

```
PRD readiness check (heuristic, 4 signals, 1 point each):
  [✓|✗] Vision & Problem Statement non-trivial
  [✓|✗] ≥ 1 populated user story
  [✓|✗] ≥ 1 must-have FR
  [✓|✗] Business Logic populated

  Score: <N>/4
  Open Questions in PRD: <count>
```

**Wynik ≥ 3**: PRD jest gotowy na mapę drogową; przejdź do Kroku 4.

**Wynik < 3**: wyraźnie ostrzeż. Nazwij, czego brakuje i dlaczego jest to ważne dla mapy drogowej (NIE ogólne „twój PRD jest zbyt skąpy”):

```
This PRD scored <N>/4 on the roadmap-readiness heuristic. Missing signals:

  - <signal name>: <one-line consequence for the roadmap>
  - ...

A roadmap generated from a hollow PRD will have many slices marked Status:
blocked with their first Unknown being a PRD gap. That's a valid intermediate
state — the roadmap surfaces what's blocking — but if you have time to firm
up the PRD first, the resulting roadmap will be substantially more actionable.
```

Następnie zapytaj za pomocą wybranego narzędzia pytań interaktywnych:

Zapytaj użytkownika:

- question: "Jak chcesz kontynuować?"
  header: "Skąpy PRD"
  options:
  - label: "Najpierw dopracuj PRD (Zalecane)"
    description: "Zatrzymaj tutaj. Rozwiąż Open Questions / TODO w PRD, a następnie ponownie wywołaj /10x-roadmap."
  - label: "Kontynuuj mimo to"
    description: "Generuj na podstawie tego, co jest. Puste obszary pojawią się jako zablokowane przekroje z luką PRD jako Unknown."
  - label: "Anuluj"
    description: "Zakończ bez zmian."
  multiSelect: false

Po „Najpierw dopracuj PRD”: wyświetl przekierowanie i ZATRZYMAJ. Po „Kontynuuj mimo to”: kontynuuj z zapisanym wynikiem, aby Krok 6 mógł oznaczyć słabe obszary.

### Krok 4: Automatyczne badanie stanu bazowego

Ocena „co już jest na miejscu” nie powinna spadać na użytkownika — kod jest źródłem prawdy. Użyj wybranego narzędzia badań w tle / tworzenia zadań, jeśli jest dostępne, aby równolegle zinwentaryzować każdą warstwę. Jeśli takie narzędzie nie istnieje, uruchom te same sondy sekwencyjnie w głównym kontekście. Każda sonda zwraca jednoakapitowy werdykt: **present** (z dowodami plikowymi), **absent** albo **partial** (szkielet istnieje, ale nie jest podłączony). Następnie przedstaw inwentaryzację użytkownikowi do potwierdzenia, zanim zasili Foundations.

**Warstwy do zbadania** (pomiń warstwę, jeśli `tech-stack.md` już podaje wybór dla tej warstwy — raportuj „zgodnie z tech-stack.md: <choice>” zamiast sondowania):

| Warstwa | Czego szuka sonda |
| -------------- | ----------------------------------------------------------------------------------------------------------------- |
| Frontend | Framework UI, narzędzia budowania, routing, biblioteki komponentów — zależności `package.json`, pliki konfiguracji frameworka |
| Backend / API | Framework serwera, trasy API, handlery żądań — entrypointy, pliki tras, kontrolery |
| Dane | Sterownik DB, ORM/query builder, narzędzia schematów/migracji, zasilone dane — pliki schematów, katalogi migracji |
| Auth | Integracja dostawcy auth, obsługa sesji/tokenów, middleware auth — konfiguracja auth, pliki middleware |
| Wdrożenie / infra | Cel hostingu, konfiguracja kontenerów, workflow CI/CD, infra-as-code — `Dockerfile`, `.github/workflows`, YAML wdrożenia |
| Obserwowalność | Biblioteka logowania, śledzenie błędów, metryki, dashboardy — importy sentry/datadog/otel, middleware logów |

**Uruchom wszystkie sondy w jednej zbiorczej delegacji, gdy host to obsługuje.** Każdy prompt jest krótki i samowystarczalny; delegowani agenci zwracają tylko po jednym akapicie, więc główny kontekst pozostaje mały. Przykład dla Auth:

> Zinwentaryzuj warstwę auth/identity w tym kodzie. Zgłoś w mniej niż 100 słowach: (1) czy istnieje integracja dostawcy auth? Nazwij ją. (2) Czy istnieją ścieżki kodu wydające lub weryfikujące sesje/tokeny? Przytocz plik:linię. (3) Czy istnieje middleware auth na poziomie tras? Przytocz. Jeśli warstwa jest nieobecna, powiedz „absent” — nie spekuluj. Nie sugeruj zmian. Nie zapisuj ani nie edytuj plików.

Dostosuj ten sam szablon do każdej warstwy. Zawsze wymagaj: werdyktu present/absent/partial, ≤ 100 słów, dowodów plikowych, gdy warstwa jest obecna, bez spekulacji, bez edycji.

Po powrocie wszystkich sond przedstaw użytkownikowi jednookranowe podsumowanie stanu bazowego:

```
Codebase baseline (auto-researched):

  Frontend:      <present | absent | partial> — <one line, with file pointer>
  Backend/API:   <…>
  Data:          <…>
  Auth:          <…>
  Deploy/infra:  <…>
  Observability: <…>
```

Następnie potwierdź:

Zapytaj użytkownika:

- question: "Czy ten stan bazowy odpowiada twojemu rozumieniu? Czy jest coś do poprawienia lub dodania, zanim wpłynie na Foundations?"
  header: "Stan bazowy"
  options:
  - label: "Wygląda dobrze — kontynuuj"
    description: "Użyj tego stanu bazowego jako danych wejściowych dla Foundations i sekcji ## Baseline mapy drogowej."
  - label: "Popraw jedną lub więcej warstw — wyjaśnię"
    description: "Korekta swobodna. Ponownie zapiszę warstwę/warstwy przed kontynuowaniem."
  - label: "Dodaj coś niewymienionego"
    description: "Swobodnie. Rzeczy pominięte przez sondy (zaplanowane, ale niepodłączone, szkielet z innego repozytorium itd.)."
  multiSelect: true

Zapisz potwierdzony stan bazowy. Bezpośrednio zasila Krok 6a (Foundations): warstwy **present** → Foundations je pomija; **absent** lub **partial** → otwiera się miejsce w Foundations. Zasila również dosłownie sekcję `## Baseline` mapy drogowej.

### Krok 5: Zwięzły wywiad — 2–3 pytania kotwiczące, każde z mocną rekomendacją

PRD ujmuje **produkt**. Stan bazowy (Krok 4) ujmuje **to, co już istnieje**. Ten krok tworzy ramę mapy drogowej — `main_goal`, `north_star`, obszary inwestycji, `top_blocker` — poprzez limitowany wywiad: maksymalnie **trzy pytania kotwiczące**, każde zawierające jedną mocną **Recommend** opartą na cytowanej linii artefaktu oraz 1–2 alternatywy z jednolinijkowym uzasadnieniem „dlaczego to również jest rozsądne”. Użytkownik wybiera Recommend, wybiera alternatywę albo swobodnie nadpisuje; obszary inwestycji są *wyprowadzane* z odpowiedzi, nie są o nie pytane. To optymalny punkt między dwoma trybami porażki, których doświadczyła ta umiejętność: **ciche auto-ramowanie** (podejmowanie kluczowych decyzji bez ludzkiej bramki) i **nieograniczone odkrywanie** (pytanie o to, na co artefakty już odpowiadają). Jeśli `shape-notes.md` zawierał blok `## Forward: technical-roadmap`, wykorzystaj go w Recommends — nie pytaj ponownie o treść, którą użytkownik już tam umieścił. Jeśli po osiągnięciu limitu kotwica nadal jest nierozstrzygnięta, **podejmij decyzję**, używając Recommend, zapisz ją we frontmatter z jednolinijkowym uzasadnieniem i kontynuuj — użytkownik może nadpisać ją w dowolnym momencie.

**5a. Wywnioskuj rekomendacje i alternatywy, które są rzeczywiście rozsądne.**

Dla każdej poniższej kotwicy wyprowadź *zarówno* Recommend, jak i alternatywy — oparte na konkretnych cytatach z frontmatter PRD / `## Vision` / `## Success Criteria` / `## NFRs` / `## Open Questions` / stanu bazowego / `tech-stack.md`. Alternatywa jest „rozsądna” tylko wtedy, gdy wspiera ją rzeczywisty sygnał w artefaktach LUB jest powszechnym, możliwym do obrony domyślnym wyborem dla kształtu produktu. **Nie wymieniaj chochołów.** Jeśli tylko jedna wartość jest wiarygodna (brak rzeczywistej alternatywy możliwej do wsparcia artefaktami), powiedz to — ta kotwica zostanie przedstawiona z pojedynczą Recommend i awaryjną opcją „nadpisz własnymi słowami”.

- **`main_goal`** — wybierz spośród `market-feedback` | `quality` | `low-complexity` | `speed` | `learn` | `other`. Sygnały: `timeline_budget` (ciasny → speed lub low-complexity), `target_scale` (mały → low-complexity; masowy rynek → quality), sformułowania Success Criteria („learn from real users” → market-feedback; „validate the riskiest assumption” → market-feedback; „no incidents at launch” → quality), ton Vision (eksploracyjne hobby → learn; twardy termin → speed). Alternatywy to *sąsiednie* wartości, które te same dowody mogą rozsądnie wspierać — np. `market-feedback` i `speed` często współistnieją, gdy PRD mówi „ship to learn fast”.

- **`north_star`** — najmniejszy widoczny dla użytkownika przepływ end-to-end, który dostarczony jako pierwszy udowadnia główną hipotezę Vision z PRD. Zwykle odnosi się do wysoko priorytetowego US-NN ORAZ głównego Success Criterion. Rozsądne alternatywy to *inne* kandydackie przekroje, które również odnoszą się do głównego Success Criterion lub wysoko priorytetowego US-NN, z mniejszą liczbą Prerequisites lub innymi konsekwencjami sekwencjonowania. Gdy istnieje więcej niż trzech kandydatów, przedstaw pierwszych trzech.

- **`top_blocker`** — wybierz spośród `skills` | `capacity` | `time` | `decisions` | `external` | `motivation` | `none`. Sygnały: ≥ 3 nierozwiązane `## Open Questions` PRD → `decisions`; ambitny zakres kontra niedopasowanie `timeline_budget` → `time` lub `capacity`; zależność od dostawcy wymieniona w PRD, ale jeszcze niezakontraktowana → `external`; tech-stack wymienia warstwę, której zespół nigdy nie wdrożył → `skills`; żaden sygnał nie występuje → `none`. Rozsądne alternatywy to *sąsiednie* typy blokad uruchamiane przez podobne sygnały — np. `time` i `capacity` często uruchamiają się oba przy napięciu zakres-kontra-termin.

- **Obszary inwestycji** (NIE są pytane — wyprowadzane w 5d) — dla każdego z `frontend`, `backend`, `data`, `infra`: zdecyduj `invest deeply` albo `go simple`. Sygnały: NFR PRD, które warunkują uruchomienie w warstwie (prywatność / opóźnienie / poprawność → inwestuj tam), luki stanu bazowego mapowane do FR must-have (brak auth + must-have dla wielu użytkowników → inwestuj w auth), Open Questions skoncentrowane w jednej warstwie (nierozstrzygnięte decyzje tam → inwestuj) oraz wybrany `main_goal` (`quality` wzmacnia warstwy prywatności/obserwowalności; `learn` wzmacnia nieznaną warstwę; `speed` / `low-complexity` domyślnie utrzymuje wszystko prosto). NIE promuj warstwy do „invest” bez wskazania sygnału PRD/stanu bazowego/main_goal.

**5b. Pomiń kotwicę tylko wtedy, gdy artefakt jest jednoznaczny.** Jeśli frontmatter PRD lub Success Criteria *dosłownie podaje* wartość (np. `timeline_budget: "1 week to ship"` plus „we need to launch before X” → `main_goal: speed`), pomiń to pytanie i ogłoś pominięcie z wybraną wartością oraz cytatem, który je przesądza. Nigdy nie pomijaj, gdy istnieje jakakolwiek wiarygodna alternatywa — potwierdzenie użytkownika przy rzeczywistym wyborze jest warte więcej niż zaoszczędzone sekundy. W praktyce zwykle zadasz 2–3 pytania; możesz zadać mniej, ale NIGDY więcej niż 3.

**5c. Przeprowadź wywiad — jedno pytanie strukturalne na kotwicę, po kolei.**

Dla każdej niepominiętej kotwicy — `main_goal`, potem `north_star`, potem `top_blocker` — użyj wybranego narzędzia pytań interaktywnych. Każde pytanie jest osobnym wywołaniem (sekwencyjnym, nie zbiorczym). Format:

Zapytaj użytkownika:

- question: "<plain-language anchor question, in the user's language>"
  header: "<short header — e.g., Cel | Gwiazda | Główne ryzyko / Goal | North star | Blocker>"
  options:
  - label: "<Recommend value> (Recommended)"
    description: "<One-line why, with the artifact quote/pointer that grounds the Recommend.>"
  - label: "<Alternative A value>"
    description: "Reasonable when <one-line condition the artifacts partially support>; you'd pick this when <sequencing/scope consequence>."
  - label: "<Alternative B value>"
    description: "Reasonable when <one-line condition>; you'd pick this when <consequence>."
  - label: "Something else — I'll explain"
    description: "Free-form. Name the value and the reason; I'll record both and sequence accordingly."
  multiSelect: false

Reguły bloku opcji:
- **Recommend zawsze jest opcją 1**, z przyrostkiem „(Recommended)” na etykiecie.
- **Każda alternatywa zawiera własną klauzulę „dlaczego rozsądna”** powiązaną z sygnałem artefaktu — nie „alternative: quality”, lecz „alternative: quality — reasonable when launch correctness matters more than first-user signal”. Alternatywa bez niej jest chochołem; usuń ją.
- **Maksymalnie 2 alternatywy** plus swobodna opcja awaryjna (łącznie 2–4 opcje). Dłuższe listy męczą użytkownika bez dodawania sygnału.
- **Opcje gwiazdy przewodniej nazywają kandydatów na przekroje, nie abstrakcyjne wartości** — każda etykieta ma postać `<US-NN candidate> — <one-line outcome>`.
- **Jeśli tylko jedna wartość jest wiarygodna** (5a nie znalazło rozsądnej alternatywy), przedstaw wyłącznie Recommend i „Something else — I'll explain” oraz ujawnij w tekście pytania: „the artifacts only support one reading here; flag if your read differs”.

**5d. Wyprowadź obszary inwestycji (bez pytania).**

Po uzyskaniu 2–3 odpowiedzi kotwiczących wyprowadź obszary inwestycji z: (1) wybranego `main_goal`, (2) NFR PRD warunkujących uruchomienie w warstwie, (3) luk stanu bazowego mapowanych do FR must-have, (4) koncentracji Open Questions. Ogłoś wyprowadzone inwestycje w podsumowaniu syntezy (5e). Użytkownik może je nadpisać jedną linią; nie jest proszony o wybór.

**5e. Podsumowanie syntezy — potwierdź bez pytania.**

Wyemituj pojedynczą zwykłą wiadomość markdown, która utrwala ramę. Bez nowych pytań. Odzwierciedlaj język użytkownika od początku do końca (polski PRD → polskie podsumowanie). Kształt:

```markdown
Locking in the roadmap framing:

- **Cel sekwencjonowania: `<main_goal>`.** <One-line rationale tying to the user's anchor answer and an artifact pointer.>
- **Gwiazda przewodnia: `<S-NN candidate> — <Outcome>`.** <One-line tying this slice to the primary Success Criterion or riskiest assumption.>
- **Główne ryzyko / blocker: `<top_blocker>`.** <One-line with the specific signal — count of Open Questions, named vendor, deadline mismatch, etc.>
- **Inwestycje: w `<layer>` głęboko; reszta lekko.** <One-line — derived from main_goal + NFR + baseline gap; not asked.>

Powiedz "go" żeby ruszyć dalej, albo nadpisz dowolną linię ("inwestycja powinna być w data, nie infra"). Nie będę pytał ponownie o to, co już ustaliliśmy.
```

Gdy użytkownik powie „go” lub pozostanie cicho po przekroczeniu granicy następnego kroku, kontynuuj z utrwaloną ramą. Nadpisania pojedynczych linii są akceptowane i ponownie zapisywane bez ponownego pytania o pozostałe kotwice.

**5f. Wyjątek dla niestandardowego kształtu MVP.**

„Niestandardowy kształt MVP” to produkt, który nie mapuje się na znany wzorzec: nie jest dashboardem SaaS, aplikacją CRUD, platformą treści, oczywistym wrapperem AI ani stroną marketingową. Sygnały: `## Vision` PRD opisuje nową interakcję lub domenę; `## User Stories` nie grupują się wokół znanej encji (create/read/update/delete a `<thing>`); `tech-stack.md` deklaruje nieoczywiste narzędzia (silniki gier, mosty sprzętowe, wyspecjalizowane runtime, nowe formy agentów); sformułowania użytkownika podkreślają nową mechanikę, a nie znany wzorzec.

Gdy PRD wygląda na niestandardowy:

1. **Otwórz wywiad, ujawniając to** w wiadomości poprzedzającej pierwsze pytanie kotwiczące: *„This PRD doesn't fit a familiar MVP pattern (no SaaS dashboard / CRUD / content / AI-wrapper shape). My Recommends for the next 2-3 questions are weaker than usual — push back hard if my read is off.”*
2. **Złagodź Recommend dotyczące `north_star` oraz każdego wyprowadzonego obszaru inwestycji.** Sformułuj opis Recommend jako *„My best read is X, but the artifact signal is thin”*, a nie *„PRD §Vision says X”*.
3. **Zezwól na maksymalnie dwie wymiany uzupełniające** oprócz trzech pytań kotwiczących. Niestandardowe MVP wynagradzają dialog; intuicja projektowa użytkownika wykonuje więcej pracy, niż potrafią wykonać artefakty. Dopytania mają formę swobodnego tekstu, a nie nowych pytań strukturalnych.

To jedyna ścieżka, w której umiejętność skłania się ku dialogowi zamiast od niego odchodzić — i jedyna ścieżka dopuszczająca dopytania. Całkowity limit w ramach tego wyjątku: 3 kotwice + 2 dopytania = 5 wymian; poza nim 3 pytania kotwiczące, bez dopytań, jedno podsumowanie syntezy.

**5g. Bariery sformułowań i języka (stosuj do każdego pytania kotwiczącego oraz podsumowania).**

- **Odzwierciedlaj język użytkownika od początku do końca.** Polski PRD → polskie pytania, opcje i podsumowanie. Tłumacz nazwy sekcji (`Open Questions` → `Otwarte pytania`, `Functional Requirements` → `Wymagania funkcjonalne`, `Non-Goals` → `Poza zakresem`, `Success Criteria` → `Kryteria sukcesu`). Żadnych angielskich fragmentów typu „north star”, „blocker”, „must-have” w polskim pytaniu lub etykiecie opcji — parafrazuj („gwiazda przewodnia”, „główne ryzyko”, „konieczne”).
- **Tłumacz żargon wewnętrzny umiejętności na zwykły język produktowy.** *„Privacy posture”* → *„polityka prywatności dostawcy AI”*. *„North star”* → *„pierwsza historyjka, która udowadnia, że produkt działa”*. *„Blocking unknowns”* → *„pytania bez odpowiedzi, które blokują dalsze planowanie”*. Użytkownik nie powinien nigdy otwierać dokumentacji tej umiejętności, aby zrozumieć pytanie.
- **Cytaty w opisach opcji muszą zasługiwać na swoje miejsce.** Cytat taki jak *„tech-stack wskazuje Astro + Supabase + OpenRouter”* jest zrzutem nazw, chyba że następna klauzula mówi, dlaczego ma to znaczenie dla *tej* kotwicy. Albo dopisz implikację w tej samej linii, albo usuń cytat.
- **Recommend musi być możliwe do obrony, nie agresywne.** Jednolinijkowe uzasadnienie Recommend opiera się na linii artefaktu, a nie pewnym tonie. Jeśli nie możesz wskazać cytatu, obniż rangę — przedstaw kotwicę z dwiema alternatywami o równej wadze (i swobodną opcją awaryjną), a użytkownik niech wybierze.

### Krok 6: Dekomponuj i uporządkuj sekwencję

To krok, w którym umiejętność zasługuje na swoje miejsce. Zbuduj treść mapy drogowej **w pamięci** (jeszcze nie na dysku).

**6a. Zidentyfikuj Foundations.** Foundation to przekrojowy warunek wstępny, który samodzielnie nie ma widocznego dla użytkownika rezultatu, ale odblokowuje nazwane pionowe przekroje, zmniejsza nazwaną blokującą niewiadomą albo tworzy infrastrukturę weryfikacyjną wymaganą przez nazwany przekrój. Jest kontraktem umożliwiającym, a nie pozwoleniem na poziome planowanie. Źródła:

- Decyzje `tech-stack.md` implikujące pracę nad szkieletem (dostawca auth → szkielet auth; wybrany cel wdrożenia → szkielet wdrożenia; wybrany monitoring → baza obserwowalności).
- `## Non-Functional Requirements` PRD wymagające infrastruktury (np. NFR „p95 < 800ms” implikuje podstawową instrumentację wydajności).
- `## Access Control` PRD, jeśli zawiera cokolwiek więcej niż „single user, no auth”.
- **Stan bazowy z Kroku 4** — wszystko raportowane jako **absent** lub **partial** jest kandydatem do Foundations. Wszystko raportowane jako **present** jest pomijane (i odnotowane w `## Baseline`).
- **„Where to invest” z Kroku 5** — wybory „invest deeply” promują foundation do własnego jawnego przekroju (np. „data layer — invest deeply” + brak stanu bazowego → jawna foundation projektowania danych F-NN, nie tylko niejawny krok migracji).

Nie wymyślaj foundations, których PRD nie implikuje (bez „set up Storybook”, chyba że coś tego wymaga). Nie twórz generycznej foundation „data layer”, „API layer”, „UI layer” lub „auth system”, chyba że potrafisz nazwać downstreamowy element `S-NN`, który odblokowuje, blokującą niewiadomą, którą zmniejsza, albo ścieżkę weryfikacji, którą umożliwia.

**Limit zakresu Foundation.** Foundation musi być najmniejszym przekrojowym elementem umożliwiającym, który pozwala kontynuować nazwany pionowy przekrój. Może ustanowić minimalny kontrakt, szkielet, politykę lub ścieżkę weryfikacji; NIE może ukończyć całej warstwy architektonicznej przed pracą skierowaną do użytkownika. Jeśli Outcome foundation brzmi jak „data layer/API/UI/auth is complete”, podziel je albo włącz minimalnie potrzebną pracę do pierwszego przekroju `S-NN`, który je konsumuje. Test: po dostarczeniu Foundation przynajmniej jedno downstreamowe `S-NN` powinno nadal integrować i wykorzystywać tę warstwę poprzez rzeczywistą możliwość użytkownika.

**Reguła progresywnego ujawniania.** Preferuj wprowadzanie elementów technicznych w chwili, gdy pierwszy przekrój skierowany do użytkownika ich potrzebuje. Foundation jest uzasadnione tylko wtedy, gdy odroczenie go sprawiłoby, że pierwszy pionowy przekrój jest niemożliwy do zaplanowania, niebezpieczny albo nieweryfikowalny. „Ta warstwa kiedyś będzie potrzebna” nie wystarcza.

Identyfikatory Foundation mają format `F-NN` (dwucyfrowe z zerem wiodącym, od `F-01`).

**6b. Zdekomponuj powierzchnię skierowaną do użytkownika na przekroje.** Przejdź przez `## User Stories` i `## Functional Requirements` PRD. Grupuj je w pionowe, end-to-end przekroje, gdzie każdy przekrój:

- Dostarcza **pojedynczą widoczną dla użytkownika możliwość**, sformułowaną jako „user can …”.
- Dotyka każdej warstwy potrzebnej, aby ta możliwość była rzeczywista (dane + logika + interfejs), od góry do dołu.
- Jest wystarczająco mały, by jedno wywołanie `/10x-plan` dawało wykonalny plan, ale wystarczająco duży, by przekrój sam w sobie był znaczący (przekrój to zwykle jedno US-NN, czasem dwa, gdy są ściśle powiązane — np. „create” i „list” tej samej encji).

NIE tnij poziomo („the database slice”, „the API slice”, „the UI slice”). Poziome przekroje są antywzorcem, któremu ta umiejętność ma zapobiegać. Domyślna dekompozycja jest vertical-first: każdy przekrój skierowany do użytkownika powinien tworzyć używalną możliwość, którą agent może zaimplementować i zweryfikować end-to-end. Praca pozioma jest dozwolona wyłącznie jako nazwana Foundation z jawnym powodem downstream.

Identyfikatory przekrojów mają format `S-NN` (dwucyfrowe z zerem wiodącym, od `S-01`).

Każde `F-NN` i `S-NN` otrzymuje także stabilne **Change ID** w kebab-case. Change ID jest mostem do `/10x-plan`, a później elementem backlogu w Jira/Linear. Preferuj zwięzłe, zorientowane na rezultat nazwy, takie jak `first-gated-generation`, `minimal-auth-for-generation` lub `srs-review-session`.

**Granularność i równowaga przekrojów.** Przekroje mapy drogowej powinny być w przybliżeniu porównywalne pod względem wysiłku planistycznego i wagi koncepcyjnej, mimo że nie zawierają estymacji. Unikaj jednego przekroju pochłaniającego większość PRD, gdy późniejsze przekroje są drobnymi elementami dopracowania. Jeśli jeden kandydacki przekrój odnosi się do wielu FR must-have lub wielu niepowiązanych historyjek użytkownika, podziel go według widocznych dla użytkownika rezultatów, faz workflow, person albo granic ryzyka, aż każde `S-NN` będzie czymś, o czym jedno `/10x-plan <change-id>` może spójnie rozumować.

Użyj tych wyzwalaczy podziału:

- Przekrój obejmuje więcej niż jedną podstawową akcję użytkownika (np. „import, edit, share, and report”).
- Przekrój łączy konfigurację, główny workflow i administrację w jednym elemencie.
- Przekrój spełnia większość FR must-have, podczas gdy inne przekroje mają po jednym drobnym FR.
- Linia Risk przekroju zawiera więcej niż jedno niezależne ryzyko.
- Przekrój potrzebuje niepowiązanych niewiadomych należących do różnych osób lub warstw.

NIE dziel według warstwy, aby naprawić rozmiar. Dziel według węższych pionowych rezultatów. Na przykład zastąp „complete recipe system” przez „user can save the first recipe”, „user can search saved recipes” i „user can share a recipe” — nie przez „recipe schema”, „recipe API” i „recipe UI”.

**6c. Zbuduj graf zależności.** Dla każdego przekroju i foundation zidentyfikuj Prerequisites:

- **Inne identyfikatory foundation** potrzebne przez przekrój (np. S-03 potrzebuje auth F-01).
- **Inne identyfikatory przekrojów**, których dane lub możliwości przekrój konsumuje (np. S-04 „rate a recipe” zależy od S-03 „see recipes”).
- **Stan zewnętrzny** (np. „a seeded ingredient table”). Konkretny, nie ogólnikowy.

Dla każdej foundation zidentyfikuj także **Unlocks**:

- jeden lub więcej downstreamowych pionowych przekrojów `S-NN`, które foundation bezpośrednio umożliwia, LUB
- jedną lub więcej blokujących Unknowns, które redukuje, LUB
- jedną lub więcej nazwanych ścieżek weryfikacji wymaganych przez downstreamowy przekrój.

Jeśli foundation nie ma jasnych Unlocks, usuń je albo włącz pracę do pierwszego pionowego przekroju, który jej potrzebuje.

Następnie dla każdego elementu wyprowadź **Parallel with** — przekroje, których Prerequisites są podzbiorem lub rodzeństwem Prerequisites tego przekroju i które od niego nie zależą. Agenci AI mogą rozdzielać pracę między nimi. Jeśli dwa przekroje nie dzielą zależności i żaden nie blokuje drugiego, są równoległe. Gdy blokadą nr 1 z Kroku 5 jest **capacity**, szczególnie hojnie obliczaj parallel-with — to najbardziej praktyczna dźwignia użytkownika.

**6d. Sortowanie topologiczne, z uprzedzeniem wobec głównego celu.** Najpierw Foundations (w kolejności zależności między nimi), następnie przekroje w kolejności zależności. Umieść przekrój **north star** tak wcześnie, jak pozwalają jego Prerequisites — nie odkładaj go dla symetrycznego porządku. Następnie rozstrzygaj remisy według main goal (Krok 5):

- **Market feedback** → remisy rozstrzygane na korzyść przekroju ujawniającego najbardziej ryzykowne założenie (często integrację albo logikę domenową). Wczesne ujawnienie ryzyka jest ważniejsze niż maksymalizacja wartości demonstracyjnej przekroju 1.
- **Quality / craft** → Foundations sekwencjonowane bardziej gorliwie; foundations obserwowalności i kontroli dostępu NIE są odkładane za przekroje skierowane do użytkownika.
- **Low complexity / quick win** → remisy rozstrzygane na korzyść najmniejszego wykonalnego przekroju; agresywne Parked.
- **Speed to launch** → najpierw ścisła ścieżka must-have; nieistotne elementy trafiają do Parked, nie są sekwencjonowane późno.
- **Learn the tech / explore** → remisy rozstrzygane na korzyść przekrojów, które najwcześniej wykorzystują nieznaną technologię; wartość nauki liczy się tu jako wartość użytkownika.

Jeśli `## Open Roadmap Questions` zawiera decyzję istotną dla sekwencjonowania (np. „do we ship for mobile first?”), NIE wybieraj sekwencji przesądzającej odpowiedź — pozostaw dotknięte przekroje ze `Status: blocked`, dopóki pytanie nie zostanie rozstrzygnięte.

**6e. Zidentyfikuj blokujące niewiadome.** Dla każdego przekroju wymień:

- **Blockers** (zewnętrzne, oczekujące) — akceptacja dostawcy, zasób projektowy, decyzja interesariusza. Jeśli nie ma, wpisz `—`. Odpowiedź „External” jako blokada nr 1 z Kroku 5 zasila je.
- **Unknowns** (pytania do zbadania) — rzeczy, na które mapa drogowa nie może odpowiedzieć i których `/10x-plan` również nie powinien próbować rozstrzygać. Każda niewiadoma zawiera: pytanie, właściciela, status blokowania (tak/nie — czy planowanie jest zablokowane do rozwiązania?). Odpowiedź „Decisions” jako blokada nr 1 z Kroku 5 zasila je.

Przekrój ze `Status: blocked` istnieje, gdy przynajmniej jedna Unknown ma `Block: yes`. Zadaniem mapy drogowej jest ujawnienie ich, aby użytkownik mógł je rozwiązać zanim `/10x-plan` zostanie zmarnowany na przekrój, którego nie można zaplanować.

**6f. Wygeneruj `## Open Roadmap Questions`.** Dwa źródła:

- `## Open Questions` PRD — skopiuj dosłownie, w razie potrzeby ponumeruj ponownie. Nadal są otwarte.
- Nowe pytania ujawnione podczas Kroku 5, które obejmują wiele przekrojów („should we actually ship for mobile?”).

Niewiadome per-przekrój pozostają w przekroju; przekrojowe trafiają tutaj.

**6g. Wygeneruj `## Parked`.** Podnieś `## Non-Goals` PRD. Dodaj także wszystko, co Krok 5 ujawnił jako odroczone — szczególnie gdy głównym celem jest **speed to launch** lub blokadą nr 1 jest **time/capacity**, ta sekcja rośnie. Każdy wpis: element w jednej linii, uzasadnienie w jednej linii.

**6h. Wyprowadź `## Streams` (pomoc nawigacyjna).** Strumienie są *wyprowadzonym widokiem* grafu zależności — NIE zastępują porządku topologicznego w `## Foundations` + `## Slices` i nie wprowadzają nowych ID. Ich zadanie: pokazać czytelnikowi zaproponowaną kolejność czytania między równoległymi ścieżkami na jednym ekranie. Wyprowadzenie: jeden strumień na foundation zakotwiczającą odrębny łańcuch Prerequisites (`F-NN` → przekroje wymieniające ją w Prerequisites, w kolejności zależności); przekrój bez warunku foundation jest własnym jednoelementowym strumieniem (nigdy koszykiem „Misc”); przekrój zależący od głów wielu strumieni dołącza do najbardziej wyprowadzonego, z połączeniem nazwanym w notatce tego strumienia („joins Stream A at S-01”) — nigdy nie jest duplikowany między strumieniami. Wygeneruj jeden wiersz tabeli markdown na strumień — `Stream | Theme | Chain | Note` — Chain łączący istniejące Roadmap IDs przez `→`, Theme opisowe, nie promocyjne („Review loop”, nie „The killer feature”), Note jedną klauzulą wiążącą strumień z `main_goal` lub nazywającą połączenie. Limit: 2–5 strumieni — więcej oznacza, że graf jest zbyt rozdrobniony (włącz jednoelementowe strumienie do strumienia sąsiedniej foundation); mniej niż 2 oznacza, że porządek topologiczny już czyta się przejrzyście, więc pomiń sekcję. Streams NIE są kanoniczne: przy każdym konflikcie wygrywa porządek topologiczny, a definicja strumienia jest błędna.

### Krok 7: Wygeneruj treść mapy drogowej

Użyj dokładnie tego szablonu (nazwy sekcji są kontraktem; narzędzia downstream i `/10x-plan` mogą ich szukać przez grep):

````markdown
---
project: <from PRD frontmatter>
version: 1
status: draft                    # draft | active | locked
created: <YYYY-MM-DD>
updated: <YYYY-MM-DD>
prd_version: <int from PRD frontmatter, or `—` for non-PRD sources>
main_goal: <market-feedback | quality | low-complexity | speed | learn | other>
top_blocker: <skills | capacity | time | decisions | external | motivation | none>
milestone_id: <kebab-case, outcome-oriented — e.g. first-usable-deck>
milestone_seq: <int, 1 for the first milestone>
milestone_status: open           # open | done
---

# Roadmap: <Project>

> Derived from <source materials> + auto-researched codebase baseline.
> Edit-in-place; archive when superseded.
> Slices below are listed in dependency order. The "At a glance" table is the index.

## Milestone

**M-<seq>: <Milestone name>** — Status: open

- **Intent:** <1-2 sentences: the outcome this milestone proves or delivers — outcome-scoped, no dates>.
- **Source materials:** <`context/foundation/prd.md` (v<N>) | listed doc paths | "user description (anchors below)">
- **Done when:** every F-NN and S-NN below is `done`<, plus any explicit acceptance line the user gave>.
- **Scope anchors:** <PRD IDs this milestone draws from (FR-NNN, US-NN ranges) — or, for description-sourced milestones, numbered `MS-NN` items distilled verbatim from the user's description:>
  - MS-01: <one scope statement>
  - MS-02: <…>
  (Omit the MS list entirely when the source is a PRD or other document.)

## Vision recap

<2-3 sentences lifted from PRD's Vision & Problem Statement. NOT a re-statement —
just enough that a reader can orient without opening prd.md.

If the recap leans on a product-strategy term — most commonly "wedge", but also
"beachhead", "primary metric", "validation milestone", "north star" — define it
inline on first use, in one short sentence in plain language. Example:
"The product wedge — the one trait that, if removed, makes the product
indistinguishable from a generic AI tool — is that cards must be both
AI-grounded in the learner's own pasted text and human-gated before they
land in the deck." A reader who has not taken a product-strategy course must
be able to read the section cold.>

## North star

**<Slice ID>: <Outcome>** — <one sentence on why this is the validation milestone, tied to main_goal>.

> A reader-facing one-liner explaining what "north star" means here: the smallest
> end-to-end slice whose successful delivery would prove the core product hypothesis
> — placed as early as Prerequisites allow because everything else only matters
> if this works. Include this gloss the FIRST time "north star" appears in the
> document body; do not repeat it later.

## At a glance

| ID    | Change ID              | Outcome (user can …)              | Prerequisites    | PRD refs       | Status   |
| ----- | ---------------------- | --------------------------------- | ---------------- | -------------- | -------- |
| F-01  | <kebab-case-change-id> | (foundation) <foundation outcome> | —                | NFR-XX         | proposed |
| F-02  | <kebab-case-change-id> | (foundation) <foundation outcome> | F-01             | NFR-YY         | proposed |
| S-01  | <kebab-case-change-id> | <user-can outcome>                | F-01             | US-01, FR-001  | ready    |
| S-02  | <kebab-case-change-id> | <user-can outcome>                | S-01             | US-02, FR-003  | proposed |
| S-03  | <kebab-case-change-id> | <user-can outcome>                | S-01, F-02       | US-03, FR-005  | blocked  |

## Streams

Navigation aid — groups items that share a Prerequisites chain. Canonical ordering still lives in the dependency graph below; this table is the proposed reading order across parallel tracks.

| Stream | Theme              | Chain                          | Note                                                      |
| ------ | ------------------ | ------------------------------ | --------------------------------------------------------- |
| A      | <Theme>            | `F-01` → `S-01` → `S-02`       | <One-line rationale tying the stream to main_goal.>       |
| B      | <Theme>            | `F-02` → `S-03`                | <Joins Stream A at `S-NN` if applicable, else standalone.> |
| C      | <Theme>            | `S-NN`                         | <Standalone slice with no foundation prerequisite.>       |

(2–5 streams; every `F-NN` and `S-NN` appears in exactly one stream. Omit this section entirely if the dep graph is too small for streams to add value — see Step 6h.)

## Baseline

What's already in place in the codebase as of `<YYYY-MM-DD>` (auto-researched + user-confirmed).
Foundations below assume these are present and do NOT re-scaffold them.

- **Frontend:** <present | absent | partial> — <one line, file pointer if present>
- **Backend / API:** <…>
- **Data:** <…>
- **Auth:** <…>
- **Deploy / infra:** <…>
- **Observability:** <…>

## Foundations

### F-01: <Foundation title>

- **Outcome:** (foundation) <one sentence on what's now in place — not user-visible>.
- **Change ID:** <kebab-case-change-id>
- **PRD refs:** <NFR-NN, Access Control section, etc. — be specific>
- **Unlocks:** <downstream S-NN IDs, blocking unknown IDs/questions, or named verification paths>
- **Prerequisites:** <slice/foundation IDs and external state — or `—`>
- **Parallel with:** <IDs that can run alongside, or `—`>
- **Blockers:** <external pending, or `—`>
- **Unknowns:** <questions, or `—`>
- **Risk:** <one line: why sequenced here, what could go wrong>
- **Status:** proposed | ready | blocked

(Repeat for each F-NN.)

## Slices

### S-01: <Slice title>

- **Outcome:** <user can …>
- **Change ID:** <kebab-case-change-id>
- **PRD refs:** <FR-NNN, US-NN, NFR-N — every must-have FR this slice satisfies, every US-NN it advances>
- **Prerequisites:** <slice/foundation IDs and external state>
- **Parallel with:** <IDs, or `—`>
- **Blockers:** <external pending, or `—`>
- **Unknowns:**
  - <question> — Owner: <user|team|TBD>. Block: <yes|no>.
  - (or `—` if none)
- **Risk:** <one line>
- **Status:** proposed | ready | blocked

(Repeat for each S-NN, in dependency order.)

## Backlog Handoff

| Roadmap ID | Change ID              | Suggested issue title         | Ready for `/10x-plan` | Notes |
| ---------- | ---------------------- | ----------------------------- | --------------------- | ----- |
| F-01       | <kebab-case-change-id> | <issue title for Jira/Linear> | no                    | <why or `—`> |
| S-01       | <kebab-case-change-id> | <issue title for Jira/Linear> | yes                   | Run `/10x-plan <change-id>` |

This table is the clean handoff to Jira/Linear or any MCP-backed backlog. Include one row for every `F-NN` and `S-NN`. It should be compact enough to copy into issues, but it must not duplicate the detailed roadmap body.

## Open Roadmap Questions

1. **<Question>** — Owner: <who>. Block: <which slice IDs this gates, or `roadmap-wide`>.
2. ...

(Each entry mirrors PRD's `## Open Questions` shape. Per-slice unknowns stay in the slice.)

## Parked

- **<Item>** — Why parked: <PRD §Non-Goals reference, or rationale from interview>.
- ...

## Milestone History

(Append-only. Carried forward verbatim into each successor milestone's roadmap; empty on the very first milestone. Closure entries are written by this skill's `READY_TO_CLOSE → CLOSED` transition. Format:)

- **M-<seq>: <Milestone name>** (`<milestone_id>`) — closed <YYYY-MM-DD>. <One-line outcome.>

## Done

(Empty on first generation. `/10x-archive` appends an entry here — and flips that item's `Status` to `done` — when a change whose `Change ID` matches the item is archived. Do NOT pre-populate. Format:)

- **<Slice ID>: <Outcome>** — Archived <YYYY-MM-DD> → `context/archive/<YYYY-MM-DD-change-id>/`. Lesson: <pointer to lessons.md if any, or `—`>.
````

**Semantyka pól, szczegółowo:**

- **Outcome** używa czasownika. Przekroje: *„user can sign in and see an empty fridge”*. Foundations: *„(foundation) auth scaffold landed; tokens issued via configured provider”*. Nigdy fraza rzeczownikowa („authentication system”); zawsze deklaracja stanu świata.
- **Change ID** jest w kebab-case, stabilne i odpowiednie dla `context/changes/<change-id>/`. Nie używaj `F-01` / `S-01` jako change id; są to lokalne ID kolejności mapy drogowej.
- **Unlocks** występuje tylko w Foundations. Nazywa downstreamowy powód istnienia tej Foundation: określone przekroje `S-NN`, blokujące niewiadome albo ścieżki weryfikacji. Foundation bez Unlocks to poziomy dryf.
- **PRD refs** używa literalnych ID z PRD (`FR-001`, `US-01`, `NFR-02`). Nie parafrazuj. Każdy FR must-have z PRD musi pojawić się po Kroku 8 w PRD refs co najmniej jednego przekroju.
- **Prerequisites** miesza ID przekrojów (`S-01`, `F-02`) i stan zewnętrzny, rozdzielone przecinkami. Stan zewnętrzny jest zwykłym angielskim tekstem („seeded ingredient table”, „design tokens published”). Jedno pole, niepodzielone.
- **Parallel with** ma charakter informacyjny. Obliczane z grafu zależności: każdy przekrój X, gdzie moje Prerequisites i Prerequisites X nie mają ścieżki między sobą. Puste = `—`.
- **Blockers** to wyłącznie *zewnętrzne oczekiwania* (dostawca, design, decyzja interesariusza). Rzeczy, których zespół nie może jednostronnie rozwiązać. Jeśli zespół MOŻE to rozwiązać, to Unknown, nie Blocker.
- **Unknowns** to pytania do zbadania. Każde zawiera Owner i flagę Block. Block=yes podnosi Status przekroju do `blocked`.
- **Risk** to jedna linia: dlaczego sekwencjonowane tutaj, co może pójść źle, dlaczego jest to bezpieczniejsza kolejność niż alternatywy. Nie postmortem. Nie katastrofizowanie. Tylko kluczowy powód, którego przyszły czytelnik potrzebuje do zrozumienia sekwencji.
- **Status** ma cykl życia: `proposed` (domyślnie przy pierwszym generowaniu) | `ready` (wszystkie Prerequisites spełnione, brak blokujących niewiadomych — można uruchomić `/10x-plan`) | `planning` | `in-progress` | `done` | `blocked` (jedna lub więcej niewiadomych z `Block: yes`). Ta umiejętność generuje wyłącznie `proposed`, `ready` i `blocked`; pozostałe są zapisywane downstream (zob. „Relacja z innymi umiejętnościami”), best-effort i wyłącznie do przodu.
- **Frontmatter `main_goal` / `top_blocker`** zapisują odpowiedzi z Kroku 5, aby przyszłe ponowne odczytanie (lub recenzent) mogło od razu zobaczyć uprzedzenie sekwencjonowania bez otwierania historii rozmowy.

**Twarda reguła — nigdy nie wymyślaj przekrojów.** Każdy przekrój musi odnosić się do ID kotwicy źródłowej (bariera 1). Jeśli wywiad ujawnił coś, czego źródła nie deklarują („oh and we also need offline mode”), NIE staje się to przekrojem — staje się Open Roadmap Question (rzeczywista luka) albo wpisem Parked (jawnie odroczonym). Mapa drogowa sekwencjonuje to, co deklarują źródła; nie rozbudowuje ich.

**Żadnych jednostek czasu. Żadnych estymacji. Żadnych ocen złożoności.** (Bariera 5.) Kolejność jest zakodowana w Prerequisites; tempo w Blockers i Unknowns. Chęć napisania „this should take a few hours” oznacza, że wkroczyłeś na teren `/10x-plan` — zatrzymaj się.

### Krok 8: Samoocena

Przed jakimkolwiek zapisem na dysk zweryfikuj mapę drogową w pamięci:

1. **Frontmatter** — obecne wszystkie 11 kluczy (`project`, `version`, `status`, `created`, `updated`, `prd_version`, `main_goal`, `top_blocker`, `milestone_id`, `milestone_seq`, `milestone_status`).
2. **Wymagane sekcje** — poniższe nagłówki `##` istnieją w tej kolejności: `Milestone`, `Vision recap`, `North star`, `At a glance`, `Streams` (opcjonalne — obecne wtedy i tylko wtedy, gdy Krok 6h zdecydował, że strumienie dodają wartość), `Baseline`, `Foundations`, `Slices`, `Backlog Handoff`, `Open Roadmap Questions`, `Parked`, `Milestone History`, `Done`. Z `Streams` liczba wynosi 13; bez niego 12.
3. **Schemat każdego wpisu** — każde S-NN ma 9 obowiązkowych pól (`Outcome`, `Change ID`, `PRD refs`, `Prerequisites`, `Parallel with`, `Blockers`, `Unknowns`, `Risk`, `Status`). Każde F-NN ma te pola plus `Unlocks`.
4. **Pokrycie PRD** — każdy FR `must-have` z PRD (grep `^- FR-\d{3}: .* must-have$`) występuje w `PRD refs` co najmniej jednego przekroju. To samo dla każdego `### US-NN:`. Jeśli must-have nie jest pokryty, samoocena kończy się NIEPOWODZENIEM.
5. **Integralność grafu zależności** — brak cykli. Każde ID wymienione w `Prerequisites` istnieje gdzieś w dokumencie. Kolejność w `## Foundations` i `## Slices` jest sortowaniem topologicznym: żaden przekrój nie zależy od czegoś, co jest po nim.
6. **Zgodność tabeli At-a-glance** — wiersze tabeli odpowiadają ciałom sekcji. `Change ID`, `Prerequisites`, `PRD refs`, `Status` każdego wiersza są dosłownie zgodne z polami w treści.
7. **Spójność statusów** — każdy przekrój `blocked` ma co najmniej jedną Unknown z `Block: yes`. Każdy przekrój `ready` ma wszystkie Prerequisites już w stanie `done` (obecnie oznacza to: brak Prerequisites ALBO Prerequisites to wyłącznie foundations, które stan bazowy raportuje jako `present`).
8. **Brak wymyślonych przekrojów** — `PRD refs` każdego przekroju zawiera co najmniej jedno rzeczywiste ID kotwicy źródłowej: ID PRD (`FR-\d{3}` lub `US-\d{2}`) dla kamieni milowych opartych na PRD albo kotwicę karty `MS-\d{2}` dla tych opartych na opisie. Mieszane źródła mogą mieszać typy ID, lecz każda kotwica musi istnieć w dokumencie źródłowym albo karcie `## Milestone`.
9. **Spójność Baseline ↔ Foundations** — żadna Foundation nie buduje ponownie warstwy, którą sekcja `## Baseline` raportuje jako `present`. Jeśli stan bazowy mówi, że auth jest obecne, a nadal istnieje `F-NN` dla szkieletu auth, to błąd samooceny (albo stan bazowy jest błędny, albo foundation jest zbędne).
10. **Kontrakt umożliwiający Foundation** — każda Foundation ma wypełnione `Unlocks` zawierające co najmniej jedno downstreamowe `S-NN`, nazwaną blokującą niewiadomą albo nazwaną ścieżkę weryfikacji. Generyczna foundation taka jak „database layer” bez downstreamowego powodu jest błędem samooceny.
11. **Integralność Change ID** — każde F-NN i S-NN ma unikalne Change ID w kebab-case; każde F-NN i S-NN pojawia się dokładnie raz w `## Backlog Handoff`; każdy wiersz handoff odnosi się do istniejącego Roadmap ID i powtarza to samo Change ID. Brak spacji, dat, etykiet statusu i Roadmap ID jako change ID.
12. **Równowaga granularności przekrojów** — żadne `S-NN` nie może pochłaniać większości nietrywialnego PRD, podczas gdy sąsiednie przekroje są wąskimi resztkami. Jeśli jeden przekrój odnosi się do większości FR must-have, więcej niż dwóch niepowiązanych wpisów US-NN, wielu podstawowych akcji użytkownika albo niepowiązanych ryzyk/niewiadomych, samoocena kończy się NIEPOWODZENIEM, chyba że PRD naprawdę ma tylko jeden workflow widoczny dla użytkownika. Napraw przez podział na węższe pionowe rezultaty, nie przez tworzenie przekrojów warstw.
13. **Limit zakresu Foundation** — żadna Foundation nie może ukończyć całej warstwy z wyprzedzeniem. Outcome i Risk muszą pokazywać minimalny kontrakt umożliwiający, a `Unlocks` musi nazywać pionowe przekroje, które nadal zintegrują tę warstwę przez zachowanie skierowane do użytkownika. Jeśli Foundation brzmi jak „build the data/API/UI/auth layer”, samoocena kończy się NIEPOWODZENIEM. Podziel, zawęź albo włącz minimum potrzebnej pracy do pierwszego konsumującego `S-NN`.
14. **Progresywne ujawnianie elementów technicznych** — każdy przekrojowy element techniczny pojawia się albo w pierwszym pionowym przekroju, który go potrzebuje, albo w Foundation wymaganym przed tym przekrojem, aby można było go zaplanować, zweryfikować lub uczynić bezpiecznym. Jeśli element techniczny zostaje wprowadzony wyłącznie dlatego, że będzie przydatny później, samoocena kończy się NIEPOWODZENIEM, a praca przechodzi do pierwszego przekroju, który rzeczywiście go używa.
15. **Pokrycie Streams** (tylko jeśli wyemitowano sekcję `## Streams`) — każde `F-NN` i każde `S-NN` wymienione w `## At a glance` pojawia się dokładnie w jednej komórce `Chain` strumienia. Zarówno duplikaty, jak i pominięcia powodują błąd. Komórki Chain odnoszą się wyłącznie do istniejących Roadmap IDs (bez wymyślonych ID). Liczba strumieni wynosi 2–5. Jeśli dokument ma < 2 kandydackie strumienie, sekcja powinna zostać pominięta (limit Kroku 6h).
16. **Integralność kamienia milowego** — `milestone_status` ma wartość `open` przy generowaniu; `milestone_seq` jest o 1 większe niż najwyższe zamknięte `M-<seq>` w `## Milestone History` (1, gdy historia jest pusta); `M-<seq>` w karcie `## Milestone` odpowiada `milestone_seq`; każda kotwica `MS-NN`, do której odnosi się dowolny przekrój, istnieje w karcie; `## Milestone History` zostało przeniesione dosłownie (nigdy nieedytowane, nigdy nieucięte) przy regeneracji i otwieraniu kolejnego kamienia milowego.
17. **Terminy strategiczne są definiowane w tekście** — przeskanuj wygenerowaną treść pod kątem listy żargonu z bariery 13; każdy wymieniony termin, który występuje, musi mieć jednolinijkową definicję przy **pierwszym** wystąpieniu (identyfikatory w stylu `FR-001`/`S-02` oraz nazwy własne narzędzi/usług są wyłączone). Niezdefiniowane pierwsze użycie powoduje NIEPOWODZENIE; termin, którego nie da się zdefiniować jednym zdaniem, jest zastępowany zwykłym językiem i emitowany ponownie.

Jeśli któraś kontrola się nie powiedzie, **przerwij zapis** i zgłoś konkretny błąd:

```
Roadmap self-review FAILED:

  - <specific failure, e.g., "FR-007 (must-have) is not covered by any slice"
     or "Slice S-04 lists S-06 in Prerequisites, but S-06 comes later in the doc"
     or "F-02 (auth scaffold) is redundant — Baseline reports auth as present">
  - ...

The roadmap was NOT written. Fix the failure and regenerate, or — if a check is
wrong — file a skill bug. Self-review aborts protect downstream tooling from
drift.
```

Następnie ZATRZYMAJ.

### Krok 9: Kontrola kolizji

```bash
test -f context/foundation/roadmap.md
```

Jeśli plik nie istnieje, zapisz do `context/foundation/roadmap.md` i przejdź do Kroku 10.

Jeśli plik istnieje, konwencją dokumentów foundation jest **edycja w miejscu** dla stopniowego dopracowania oraz **archiwizacja, a następnie zastąpienie** dla pełnej regeneracji. Ta umiejętność tworzy *pełną* mapę drogową z PRD; chirurgiczne dopracowanie jest poza zakresem. Dlatego domyślnie archiwizuj i zastępuj, ale zapytaj za pomocą wybranego narzędzia pytań interaktywnych:

Zapytaj użytkownika:

- question: "context/foundation/roadmap.md już istnieje. Jak chcesz kontynuować?"
  header: "Kolizja"
  options:
  - label: "Archiwizuj i zastąp (Zalecane)"
    description: "Przenieś istniejący plik do context/foundation/archive/<today>-roadmap.md, a następnie zapisz nową mapę drogową. Historia zachowana zgodnie z konwencją foundation README."
  - label: "Nadpisz bez archiwizacji"
    description: "Zastąp w miejscu. Istniejąca treść zostanie utracona (chyba że została zatwierdzona w repozytorium). Używaj tylko wtedy, gdy istniejąca mapa drogowa jest pusta lub robocza."
  - label: "Anuluj"
    description: "Zakończ bez zapisu. Bez rozstrzygania kolizji."
  multiSelect: false

Po „Archiwizuj i zastąp”: utwórz `context/foundation/archive/`, jeśli go brakuje, przenieś istniejący plik do `context/foundation/archive/<today>-roadmap-<milestone_id>.md` (dzisiejsza data w `YYYY-MM-DD`; usuń sufiks `-<milestone_id>` dla starszych plików bez niego), a następnie zapisz nową treść. Jeśli plik już istnieje pod tą ścieżką archiwum (regenerowany dwukrotnie tego samego dnia), dodaj `-2`, `-3` itd.

Po „Nadpisz bez archiwizacji”: zapisz nową treść, nadpisując w miejscu.

Po „Anuluj”: ZATRZYMAJ.

### Krok 10: Przekaż dalej

Po zapisaniu podsumuj:

```
═══════════════════════════════════════════════════════════
  ROADMAP GENERATED
═══════════════════════════════════════════════════════════

  Project:           <project>
  Milestone:         M-<seq>: <name>  (<milestone_id>)  —  open
  Path:              context/foundation/roadmap.md
  Main goal:         <main_goal>            (sequencing bias)
  #1 blocker:        <top_blocker>          (what to plan around)
  Baseline present:  <comma-separated layers reported present>
  Foundations:       <count>
  Slices:            <count>
  Status breakdown:  ready: N  |  proposed: M  |  blocked: K
  PRD coverage:      <covered must-have FRs> / <total must-have FRs>
  Open Roadmap Q:    <count>
  Parked items:      <count>

  North star:  <Slice ID> — <Outcome>

═══════════════════════════════════════════════════════════
```

Następnie **zarekomenduj jeden następny ruch** — nie oddawaj listy „ready” i nie pytaj użytkownika o wybór. Wybierz jeden element mapy drogowej do zaplanowania jako pierwszy i uzasadnij go w jednej linii. Użytkownik może go nadpisać, ale domyślnie przedstawiana jest rekomendacja, nie menu.

**Reguła wyboru rekomendowanego następnego ruchu** (stosuj kolejno, wygrywa pierwsze dopasowanie):

1. Jeśli north star jest `ready`, zarekomenduj ją. North star jest kamieniem walidacyjnym; jej odkładanie traci sygnał.
2. W przeciwnym razie, jeśli Foundation, od której north star bezpośrednio zależy, jest `ready`, zarekomenduj tę Foundation i wyraźnie powiedz „this unlocks the north star <S-NN>”.
3. W przeciwnym razie, jeśli żaden przekrój nie jest `ready`, zarekomenduj rozwiązanie Open Question lub Blocker o najwyższej dźwigni (tego, który odblokowuje najwięcej elementów downstream). Do tego czasu nie ma dostępnego ruchu planistycznego.
4. W przeciwnym razie zarekomenduj przekrój `ready`, który odblokowuje najwięcej elementów downstream (największy fan-out w grafie zależności). Remis rozstrzygnij według głównego celu (Krok 6d).

Format:

```
► **Your next move:** `/10x-plan <change-id>` on **<Roadmap ID>: <Outcome>**.

  Why this one first: <one sentence — load-bearing reason: it IS the north
  star / it unblocks the north star / it has the highest fan-out / it's the
  smallest end-to-end validation we can ship now>.

  After that, in order: <next ready ID>: <Outcome> → <next>: <Outcome>.
  (Full list in `## Backlog Handoff`.)

  Blocked — stay parked until their Unknowns resolve:
    - <Slice ID>: <Unknown> (Owner: <who>)
    - ...
  (Resolving any of these promotes its slice to `ready` and changes my
  recommendation; come back and I'll re-recommend.)
```

Jeśli żaden przekrój nie jest `ready` i żadna Foundation również nie jest `ready` (przypadek 3), zastąp rekomendację:

```
► **No planning move is available yet.** Every slice is blocked.
  Highest-leverage unknown to resolve next:

    <Question> — Owner: <who>. Unblocks: <S-NN, S-MM, ...>.

  Resolving this promotes <count> slices and is the single change that
  most opens the roadmap. Resolve it, then re-invoke `/10x-roadmap` to
  re-recommend.
```

ZATRZYMAJ. Nie przechodź automatycznie do innej umiejętności — użytkownik wybiera, kiedy planować. Ale NIE obniżaj rekomendacji do listy wielokrotnego wyboru; jeśli użytkownik chce inny przekrój, powie to.

## Krytyczne bariery

1. **Materiały źródłowe są źródłem.** Każdy przekrój odnosi się do ID kotwicy źródłowej — ID PRD (`FR-NNN`/`US-NN`) dla kamieni milowych opartych na PRD, kotwic karty `MS-NN` dla tych opartych na opisie użytkownika. Ramowanie celu/gwiazdy przewodniej/inwestycji/blokady w Kroku 5 ujawnia kontekst wywnioskowany ze źródeł; stan bazowy ujawnia, co już istnieje; żadne nie rozbudowuje źródeł. Elementy mapy drogowej bez odwołania do źródła są błędem samooceny.

2. **Najpierw pionowe przekroje.** Przekrój dostarcza widoczną dla użytkownika możliwość end-to-end. Przekroje poziome („the API layer”, „the schema”) są antywzorcem, któremu ta umiejętność ma zapobiegać. Foundations są *jedynym* wyjątkiem — są jawnie przekrojowymi elementami umożliwiającymi, znajdują się we własnej sekcji, zawierają `Unlocks` i są oznaczone `(foundation)`, aby żaden czytelnik nie pomylił ich z pracą skierowaną do użytkownika.

3. **Zrównoważona granularność bez estymacji.** Przekroje nie otrzymują etykiet rozmiaru, lecz ich zakres nadal musi być porównywalny. Mapa drogowa, w której `S-01` zawiera niemal całe PRD, a `S-02`/`S-03` to drobne resztki, jest złą mapą drogową. Dziel zbyt duże elementy według węższych rezultatów widocznych dla użytkownika, faz workflow, person albo granic ryzyka — nigdy według warstwy technicznej.

4. **Foundations są minimalnymi odblokowaniami, nie projektami ukończenia warstwy.** Foundation może tworzyć najmniejszy warunek wstępny potrzebny przed rozpoczęciem pracy pionowej. Nie może z wyprzedzeniem budować całej warstwy database/API/UI/auth. Jeśli element techniczny można wprowadzić wewnątrz pierwszego przekroju skierowanego do użytkownika, który go potrzebuje, umieść go tam; utrzymuje to integrację pionową i progresywnie ujawnia tylko potrzebne elementy.

5. **Bez estymacji, bez jednostek czasu.** Żadnego „Day 1”, „2 weeks”, „small/medium/large” ani punktów. Wykonanie przez agentów AI jest nieliniowe, a estymacje oparte na budżecie czasowym kłamią. Kolejność jest zakodowana w Prerequisites; tempo ujawnia się przez Blockers i Unknowns. Mapa drogowa opisuje kształt, nie harmonogram.

6. **Bez niskopoziomowych szczegółów technicznych.** Bez nazw frameworków (są w `tech-stack.md`), bez ścieżek plików, definicji schematów, kodu ani wyborów bibliotek. Jeśli zaczynasz je pisać, wkroczyłeś na teren `/10x-plan` — zatrzymaj się i pozwól `/10x-plan` wykonać jego pracę downstream.

7. **Ujawniaj niewiadome, nie maskuj ich.** Unknowns per-przekrój z `Block: yes` podnoszą `Status: blocked`. Niewiadome przekrojowe trafiają do `## Open Roadmap Questions`. Jeśli PRD ma TODO, mapa drogowa dziedziczy je jako niewiadome zablokowanych przekrojów. Wartość mapy drogowej częściowo polega na pokazaniu użytkownikowi, co NIE jest jeszcze możliwe do zaplanowania.

8. **Stan bazowy jest badany automatycznie, a nie pytany.** Nie pytaj użytkownika „what's already in place?” — użyj równoległych agentów badawczych w tle (Krok 4) i pozwól, aby kod odpowiedział. Następnie pytaj użytkownika wyłącznie o potwierdzenie lub korektę. To kontrakt, który czyni Foundations uczciwymi: foundation istnieje tylko wtedy, gdy stan bazowy mówi, że warstwa jest absent lub partial.

9. **Samoocena przerywa przy dryfie.** Brak wymaganych sekcji, uszkodzony graf zależności, niepokryte FR must-have, wymyślone przekroje, zbyt duże przekroje, Foundation kończące warstwę, sprzeczności Baseline-vs-Foundations — wszystko przerywa zapis z konkretnym błędem. Bez cichego łatania.

10. **Konwencja dokumentów foundation.** `roadmap.md` jest dokumentem foundation zgodnie z `context/foundation/README.md`. Domyślna obsługa kolizji to archiwizacja i zastąpienie (historia trafia do `foundation/archive/<today>-roadmap.md`); chirurgiczne dopracowanie jest poza zakresem tej umiejętności (edytuj ręcznie, jeśli go potrzebujesz).

11. **Wyłącznie uniwersalny język.** Żadnych odniesień do 10xDevs / cohort / certification w jakimkolwiek wyniku skierowanym do użytkownika ani artefakcie zapisanym na dysk. Umiejętność jest generycznym generatorem map drogowych.

12. **Nigdy nie łańcuchuj automatycznie.** Krok 10 jest ogłoszeniem, nie wywołaniem. Użytkownik wybiera, kiedy (i który) przekrój przekazać do `/10x-plan`. Automatyczne łańcuchowanie pominęłoby ludzką recenzję wygenerowanej mapy drogowej.

13. **Definiuj terminy strategiczne w tekście przy pierwszym użyciu.** Słownictwo strategii produktu — `wedge`, `beachhead`, `north star`, `validation milestone`, `primary metric`, `must-have path`, `product-market fit`, `thin end of the wedge`, `riskiest assumption`, `core hypothesis` — jest wewnętrznym skrótem umiejętności i PRD, a nie powszechną wiedzą; mapa drogowa musi być czytelna od razu dla współpracownika (lub przyszłego ciebie), który nie ukończył kursu strategii produktu. Przy PIERWSZYM wystąpieniu każdego takiego terminu w treści dokumentu dołącz jednolinijkową definicję w tekście (w nawiasie, jako doprecyzowanie po myślniku lub krótkie następne zdanie); nie powtarzaj jej później. Jeśli pojęcia nie da się zdefiniować jednym zdaniem, zastąp je zwykłym językiem („the smallest end-to-end flow that proves the product works” jest lepsze od „the wedge”, którego nie da się skompresować do jednej klauzuli). Dotyczy prozy skierowanej do użytkownika w wygenerowanym dokumencie — nie pytań wywiadu (5g je obejmuje) ani semantyki pól tego pliku. Kontrola samooceny nr 17 to wymusza.

14. **Zwięzły wywiad z mocnymi Recommends — nie ciche auto-ramowanie, nie nieograniczone odkrywanie.** Reguły Kroku 5 są normatywne: maksymalnie 3 pytania kotwiczące (`main_goal`, `north_star`, `top_blocker`), obszary inwestycji wyprowadzane, nie pytane, każde pytanie z jedną Recommend opartą na cytowanej linii artefaktu oraz 1–2 rzeczywistymi alternatywami (chochoły zabronione), pominięcia tylko wtedy, gdy artefakty dosłownie podają wartość, dopytania tylko w ramach wyjątku custom-MVP (5f). Rekomendowany następny ruch z Kroku 10 stosuje tę samą zasadę przy przekazaniu dalej: jedna rekomendacja z jednolinijkowym powodem, nie lista „ready to plan”, którą użytkownik musi sortować.

15. **Kamienie milowe tworzą pętlę, ale nigdy nie są ograniczone czasowo.** Dokładnie jeden kamień milowy jest otwarty naraz; zamyka się dopiero, gdy każde F-NN/S-NN ma status `done` (lub użytkownik jawnie go porzuci), po czym pętla otwiera się ponownie ze świeżymi materiałami źródłowymi lub opisem użytkownika. Stan kamienia milowego jest wyprowadzany wyłącznie z `roadmap.md` — bez plików pomocniczych. Specyfikacja cyklu życia znajduje się w `references/milestone-state.md`, ładowanym WYŁĄCZNIE przy operacjach na poziomie kamienia milowego (dyspozycja Kroku 0). Umiejętności downstream pozostają nieświadome kamieni milowych; ta umiejętność wykrywa ukończenie kamienia milowego przy następnym wywołaniu.

## Uwagi

- Ta umiejętność jest **generatorem dokumentu oraz trackerem kamieni milowych**. Wynikiem jest `context/foundation/roadmap.md`, kropka. Planowanie pojedynczych zmian znajduje się downstream w `/10x-plan`.
- Sonda stanu bazowego (Krok 4) zastępuje dawne pytanie „what's already in place?”. Subagenci są tańsi niż uwaga użytkownika, a kod jest bardziej niezawodny niż pamięć.
- Gdy umiejętność regeneruje istniejącą mapę drogową, zarchiwizowana poprzednia wersja jest najczystszym celem diff do zobaczenia, jak zmieniło się rozumienie projektu — to właśnie możliwość, dla której zaprojektowano konwencję dokumentów foundation.