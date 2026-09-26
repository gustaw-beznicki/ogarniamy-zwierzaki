---
name: 10x-implement
description: Implement technical plans from context/changes/<change-id>/plan.md with verification
---

# Wdrażanie planu

Twoim zadaniem jest wdrożenie zatwierdzonego planu technicznego z `context/changes/<change-id>/plan.md`. Te plany zawierają fazy z konkretnymi zmianami oraz kanoniczną sekcję `## Progress` na dole, która steruje stanem wykonania (zob. `references/progress-format.md`).

## Konfiguracja początkowa

Gdy to polecenie zostanie wywołane:

1. **Rozwiąż plan**:
   - Jeśli wywołano jako `/10x-implement <change-id> [phase N]`, rozwiąż do `context/changes/<change-id>/plan.md`.
   - Jeśli wywołano z `@context/changes/<change-id>/plan.md` lub pełną ścieżką, zaakceptuj ją.
   - **Odmów, jeśli rozwiązana ścieżka zaczyna się od `context/archive/`** — wyświetl „This change is archived. Open a new change with `/10x-new` instead.” i ZATRZYMAJ się.
   - Jeśli nic nie podano, odpowiedz poniższym komunikatem i **ZATRZYMAJ się oraz czekaj**:

```
I'll help you implement an approved technical plan. Please provide:

1. A change-id (e.g., `/10x-implement oauth-login phase 1`), or
2. A full path (e.g., `@context/changes/oauth-login/plan.md`).

You can list active changes with: `ls context/changes/`

Tip: Make sure the plan has been reviewed and approved before implementation.
```

## Rozpoczęcie pracy

Po otrzymaniu ścieżki planu:

- Przeczytaj plan w całości. Sekcja `## Progress` na dole jest nadrzędna dla stanu wykonania — pola wyboru (`- [x]`) występują TYLKO tam. Bloki faz zawierają zwykłe wypunktowania `- ` (bez pól wyboru).
- Przeczytaj `context/foundation/lessons.md`, jeśli istnieje, i przyswój każdy wpis przed rozpoczęciem dowolnej fazy — są to zaakceptowane przez zespół powtarzające się zasady, które muszą kształtować każdą decyzję implementacyjną w tym uruchomieniu.
- Przeczytaj wszystkie pliki wymienione w planie (odwołane badania, ramy, pliki źródłowe w tym samym folderze zmiany)
- **Czytaj pliki w całości** — nigdy nie używaj parametrów limit/offset, potrzebujesz pełnego kontekstu
- Głęboko zastanów się, jak elementy pasują do siebie
- **Wykonaj preflight bramek**: zbierz polecenia z kryteriów sukcesu Automated każdej fazy i sprawdź, czy każde można tu uruchomić — binarny plik wykonywalny lub skrypt pakietu istnieje (`package.json` scripts, `command -v`, `Makefile` targets). Kryterium, którego polecenia nie można uruchomić, jest rozbieżnością dla fazy, która go potrzebuje, a znacznie taniej jest powiedzieć o tym teraz niż odkryć to po napisaniu kodu. Zgłoś każde niemożliwe do uruchomienia polecenie przy wejściu (`PREFLIGHT: <command> not runnable — Phase <N> will need this`). Nigdy po cichu nie pomijaj niemożliwego do zweryfikowania kryterium.
- **Zaktualizuj `change.md`**: przy wejściu ustaw `status: implementing` (tylko jeśli obecnie jest w `{planned, plan_reviewed}`) oraz `updated: <today>`.
- **Zsynchronizuj roadmapę** (w miarę możliwości, raz przy wejściu): jeśli `context/foundation/roadmap.md` zawiera element, którego `Change ID` równa się `<change-id>`, zmień temu elementowi `Status: in-progress`. Zobacz „## Roadmap status sync” poniżej. Jest to odpowiednik dla otwartej pracy względem zmiany na `done` wykonywanej przez `/10x-archive`; nigdy nie blokuje i większość zmian nie będzie śledzona w roadmapie.
- Policz łączną liczbę faz (z nagłówków `## Phase N:`) i utwórz po jednym wpisie zadania na fazę (pojawiają się one na pasku stanu użytkownika):
  - Dla każdej fazy utwórz zadanie z `subject: "Phase N: [Phase Name]"` oraz `activeForm: "Implementing Phase N"`
  - Ustaw bieżącą fazę jako `in_progress` przed rozpoczęciem pracy
  - Oznacz każdą fazę jako `completed`, gdy jej kryteria sukcesu przejdą
- **Znajdź następny oczekujący krok** poprzez przeskanowanie sekcji `## Progress`: pierwsza linia `- [ ]` w kolejności dokumentu to miejsce rozpoczęcia. Jeśli przekazano argument `phase N`, przejdź do pierwszego `- [ ]` wewnątrz `### Phase N:` zamiast tego.
- Rozpocznij implementację, jeśli rozumiesz, co należy zrobić

## Tryb wykonywania dla każdej fazy

Pisanie kodu dla fazy jest kosztowną częścią tej sesji: pełne czytanie plików źródłowych, analizowanie zmian, stosowanie edycji. Uruchom to w subagencie, aby główny kontekst pozostał zwięzły w długim planie; uruchom to tutaj, aby użytkownik mógł obserwować i przerwać pracę w trakcie fazy. Oba podejścia są właściwe — użytkownik wybiera dla każdej fazy.

**Zapytaj przed pierwszą fazą uruchomienia** (pierwszą oczekującą fazą lub tą wskazaną argumentem `phase N`):

Zapytaj użytkownika: „Phase [N] — how should I implement it?”

- **Delegate to a subagent (Recommended)**: Subagent pisze kod; ja zachowuję tutaj bramki, staging, commit i Progress. Utrzymuje to ten kontekst zwięzłym dla długiego planu — po powrocie zobaczysz zmodyfikowane pliki, dostosowania i werdykty bramek.
- **Implement in this context**: Piszę kod tutaj, aby można było obserwować wprowadzane edycje i przekierować mnie w trakcie fazy. Zużywa kontekst — długi plan może wymagać wyczyszczenia między fazami.

Dla każdej późniejszej fazy wybór jest przenoszony wraz z monitem „Next phase decision” na końcu rytuału commitu — bez osobnego pytania. Jeśli użytkownik poprosił o kilka faz kolejno (więc ten monit jest pomijany), przenieś dalej ostatnio wybrany tryb.

Niezależnie od obowiązującego trybu, **wszystko, co użytkownik musi zdecydować lub przejrzeć, pozostaje tutaj**: wykonanie bramek i linie werdyktów, staging, rytuał commitu, zmiany w `## Progress`, pytania o rozbieżności. Delegowanie przenosi pisanie, nigdy decyzję.

### Delegowanie implementacji do subagenta

Gdy faza jest delegowana, przed stosem bramek oddeleguj implementację do jednego subagenta ogólnego przeznaczenia, którego prompt zawiera:

- Change-id oraz numer fazy + tytuł.
- Pełną sekcję planu danej fazy dosłownie — Overview, Changes Required, Success Criteria. (Success Criteria jest kontekstem, aby subagent znał cel; NIE uruchamia bramek — robisz to ty.)
- **Dyscyplinę implementacyjną do stosowania.** Rozwiąż `references/implementation-discipline.md` (znajduje się obok tego pliku instrukcji) do **ścieżki bezwzględnej** i powiedz subagentowi, aby ją przeczytał i stosował. Uruchomiony subagent nie ma pojęcia o katalogu tego pliku instrukcji, więc ścieżka względna lub „przeczytaj odniesienie tej instrukcji” nie zostaną rozwiązane. Wskaż plik; nie powtarzaj go inline.
- Każdy wpis z `context/foundation/lessons.md`, jeśli istnieje — subagent nie może przeczytać pliku, chyba że wkleisz wpisy.
- Taksonomię rozbieżności z „Implementation Philosophy”: dostosowuj bezpośrednio rozbieżności **Minor** i zgłaszaj je; w przypadku rozbieżności **Structural** zatrzymaj się i zgłoś ją zamiast dostosowywać lub przeprojektowywać.
- Twarde granice: implementuj TYLKO zmiany w kodzie. Nie uruchamiaj stosu bramek, nie wykonuj stagingu, nie commituj, nie dotykaj sekcji `## Progress` ani żadnego pola wyboru, nie edytuj bloków faz, nie wychodź poza zakres planu. Nie zadawaj użytkownikowi pytań — użytkownik rozmawia z tobą, nie z subagentem; otwarte pytanie wraca zamiast tego w wiadomości końcowej.

Wymagaj poniższej ustrukturyzowanej wiadomości końcowej jako wartości zwracanej, a nie notatki skierowanej do człowieka:

```
STATUS: completed | structural-mismatch
TOUCHED: <repo-relative path>, <path>, ...      # every file created or edited
ADAPTATIONS: <one line each, or none>
STRUCTURAL: <plan assumption vs. what exists — only when STATUS is structural-mismatch>
UNCERTAINTIES: <ambiguous decisions, or none>
```

Po powrocie:

- **`completed`** → zainicjuj zestaw zmodyfikowanych plików fazy z `TOUCHED` (zob. „Tracking files touched during a phase”), przekaż użytkownikowi `ADAPTATIONS` i `UNCERTAINTIES` własnymi słowami — ciche dostosowanie to takie, które boli później — i przejdź do stosu bramek. Nigdy ślepo nie ufaj `TOUCHED`; uzgodnienie `git status --porcelain` podczas stagingu jest weryfikacją krzyżową dla pliku, którego subagent dotknął, ale pominął na liście.
- **`structural-mismatch`** → nie uruchamiaj bramek. Przedstaw blok problemu z „Implementation Philosophy”, używając szczegółu `STRUCTURAL` subagenta, i zadaj pytanie o rozbieżność. Następnie:
  - **Adapt and continue** → subagent, który utrzymywał kontekst fazy, już nie istnieje, dlatego uruchom nowego dla pozostałej części, przekazując decyzję użytkownika, szczegół `STRUCTURAL` oraz częściową listę `TOUCHED`. Ponownie czyta to, czego potrzebuje; to ponowne czytanie jest uczciwą ceną tej ścieżki i występuje tylko przy rozbieżnościach strukturalnych. Jeśli faza była prawie ukończona, po prostu ukończ ją tutaj.
  - **Skip this part** / **Stop and re-plan** → jak opisano w „Implementation Philosophy”. Pozostaw częściową pracę w worktree; nie wykonuj stagingu ani commitu.

Poprawka bramki może zostać delegowana w ten sam sposób — uruchom ukierunkowanego subagenta z wynikiem nieudanej bramki i problematycznymi plikami oraz połącz zwrócone `TOUCHED` z zestawem fazy przed ponownym stagingiem. Trywialne mechaniczne poprawki (osierocony import, zmiana nazwy) szybciej zastosować tutaj. W każdym przypadku budżet dwóch prób pozostaje bez zmian, a **kontrola celowego zepsucia zawsze odbywa się tutaj** — jej edycja tylko w worktree i bezwarunkowe przywrócenie nigdy nie są delegowane.

## Filozofia implementacji

Plany są starannie zaprojektowane, ale rzeczywistość może być chaotyczna. Twoim zadaniem jest:

- Podążać za intencją planu, dostosowując się do tego, co znajdziesz
- Wdrożyć każdą fazę w całości przed przejściem do następnej
- Zweryfikować, czy praca ma sens w szerszym kontekście kodu
- Aktualizować pola wyboru w planie w miarę ukończenia sekcji

[references/implementation-discipline.md](references/implementation-discipline.md) jest warstwą rzemiosła dla samej edycji — czytaj odwołany kod w całości, dostosowuj bez przeprojektowywania, dopasuj zmianę do jej sąsiadów, honoruj zaakceptowane przez zespół zasady, szukaj przed edycją nieznanego obszaru. Przeczytaj ją przed pierwszą fazą implementowaną tutaj; gdy faza jest delegowana, czyta ją zamiast tego subagent (bezwzględną ścieżką — zob. „Dispatching the implementation subagent”). Ta sekcja obejmuje tylko to, co robić, gdy plan i rzeczywistość się nie zgadzają.

Gdy rzeczy nie pasują dokładnie do planu, zastanów się, dlaczego, i komunikuj się jasno. Plan jest twoim przewodnikiem, ale twój osąd również ma znaczenie.

**Sklasyfikuj rozbieżność, zanim przerwiesz.** Nie każda luka zasługuje na pytanie:

- **Minor** — przeniesiony plik, zmieniony symbol, dryf importów, trywialna różnica API lub konfiguracji. Intencja planu pozostaje nienaruszona; zmieniły się tylko współrzędne. Dostosuj implementację do rzeczywistości, powiedz o tym w jednej linii (`ADAPT: plan says src/auth.ts, file is now src/auth/index.ts`) i kontynuuj. Nie zatrzymuj się z tego powodu; pytanie o każdą ścieżkę importu zasypuje te, które mają znaczenie.
- **Structural** — brakująca zależność, architektura różniąca się od założeń planu, odwołany plik lub API, które nie istnieje, faza zależna od wyniku, którego poprzednia faza nigdy nie wyprodukowała. Planu nie da się realizować zgodnie z zapisem, a dostosowanie oznaczałoby jego przeprojektowanie. Zatrzymaj się i podążaj ścieżką strukturalną poniżej.

W razie wątpliwości między tymi dwoma przypadkami potraktuj to jako structural. Błędne „zapytanie” kosztuje jedną wymianę; błędne „dostosowanie” może dostarczyć przeprojektowanie, którego nikt nie zatwierdził.

W przypadku rozbieżności strukturalnej:

- ZATRZYMAJ się i głęboko zastanów, dlaczego nie można podążać za planem
- Przedstaw problem jasno jako tekst:

  ```
  Issue in Phase [N]:
  Expected: [what the plan says]
  Found: [actual situation]
  Why this matters: [explanation]
  ```

- Następnie zapytaj użytkownika: „How should I handle this mismatch?”

  - **Adapt and continue**: Dostosuj implementację do rzeczywistości. Wyjaśnię dostosowanie.
  - **Skip this part**: Przejdź do następnej sekcji/fazy. Ta zmiana nie jest potrzebna.
  - **Stop and re-plan**: Ta rozbieżność jest zbyt istotna. Najpierw musimy zaktualizować plan.

## Śledzenie plików zmodyfikowanych podczas fazy

Rytuał commitu na końcu fazy (zob. „Verification Approach” poniżej) wykonuje staging plików z **zestawu zmodyfikowanych plików**, który utrzymujesz w pamięci roboczej przez całą fazę. Ten zestaw jest kanonicznym wejściem dla `git add` — nigdy nie wracaj do heurystyk `git status` przy decyzjach stagingowych.

**Dyscyplina**:

- Za każdym razem, gdy edytujesz lub tworzysz plik podczas bieżącej fazy, dodaj jego ścieżkę względną względem repozytorium do zestawu zmodyfikowanych plików.
- Gdy faza jest delegowana, zainicjuj zestaw listą `TOUCHED` zwróconą przez subagenta i połącz z nim każdą ścieżkę zwróconą przez delegowaną poprawkę bramki. Pliki edytowane tutaj bezpośrednio — pola wyboru `## Progress`, zmiana `change.md` — dodaj jak zwykle.
- Zestaw zawsze zawiera `context/changes/<change-id>/plan.md`, ponieważ każda faza powoduje co najmniej jedną edycję swojej sekcji `## Progress`. Dodaj go przy wejściu do fazy jeszcze przed zmianą jakiegokolwiek pola wyboru.
- **Bootstrap Phase 1**: w pierwszej fazie zmiany również zainicjuj zestaw zmodyfikowanych plików wszystkimi nieśledzonymi lub zmodyfikowanymi plikami wewnątrz `context/changes/<change-id>/` — zwykle `change.md`, `research.md`, `plan.md` i wszelkimi innymi plikami kontekstowymi utworzonymi podczas planowania. Pliki te są częścią zmiany i powinny trafić do pierwszego commitu, zamiast pozostać nieśledzonymi maruderami.
- Zestaw **resetuje się na każdej granicy fazy**. Po zakończeniu commitu na końcu fazy wyczyść go przed rozpoczęciem następnej fazy.
- Ta lista nadpisuje każdą heurystykę z `git status`. Jeśli zestaw zmodyfikowanych plików to `{a.md, b.md, plan.md}`, ale `git status --porcelain` raportuje również brudne `c.md`, `c.md` jest niezwiązany — obsłuż go przez monit o brudne ścieżki w rytuale, nigdy po cichu nie dołączaj go do commitu.

## Śledzenie odniesień do issue/tasków dla commitów

Przed zaproponowaniem wiadomości commitu na końcu fazy lub w epilogu przeskanuj kontekst rozmowy pod kątem odniesień do issue lub tasków systemu śledzenia powiązanych z tą pracą implementacyjną, w tym kluczy Jira (na przykład `ABC-123`), ID zgłoszeń Linear (na przykład `ENG-123`), odniesień do issue/PR GitHub (na przykład `#123`, `GH-123` lub pełnych URL-i issue/PR GitHub) albo jawnych linków do zadań z Jira, Linear lub GitHub.

- Jeśli obecne jest jedno lub więcej odniesień, uwzględnij je w treści wiadomości commitu pod linią `Refs:`, zachowując dokładne identyfikatory/URL-e podane przez użytkownika, gdzie to możliwe.
- Jeśli dotyczy wiele odniesień, wypisz je rozdzielone przecinkami w jednej linii `Refs:`.
- Nie wymyślaj ani nie wywnioskuj odniesień śledzenia na podstawie change-id, nazwy gałęzi ani nazw plików. Używaj wyłącznie odniesień widocznych w bieżącym kontekście rozmowy lub jawnie podanych przez użytkownika.
- Stosuj tę samą linię `Refs:` do każdego commitu kończącego fazę i do commitu epilogu, chyba że użytkownik ograniczy odniesienie do konkretnej fazy.

## Synchronizacja statusu roadmapy

`context/foundation/roadmap.md` (tworzony przez `/10x-roadmap`) indeksuje każdy Foundation/Slice za pomocą stabilnego **Change ID**. `/10x-archive` już zamyka pętlę na dalszym końcu — gdy zmiana jest archiwizowana, zmienia pasujący element roadmapy na `Status: done`. Ten krok obsługuje bliższy koniec: gdy implementacja się *rozpoczyna*, oznacz pasujący element jako **`in-progress`**, aby roadmapa pokazywała aktywną pracę zamiast przechodzić prosto z `ready` do `done`.

Uruchom to **raz, przy wejściu** do zmiany (zaraz po oznaczeniu `change.md` → `implementing`) — nie dla każdej fazy. Wyszukiwanie jest **obowiązkowe**; „best effort” obejmuje wyłącznie *edycje* — brak roadmapy lub brak znalezionego celu jest po cichu pomijany i nigdy nie blokuje, nie wyświetla monitu, nie wycofuje ani nie przerywa uruchomienia. Nie pomijaj sprawdzenia, zakładając, że roadmapy nie ma.

1. `test -f context/foundation/roadmap.md`. Jeśli nie istnieje, pomiń ten krok po cichu.
2. Zapisz, czy plik jest już brudny: `ROADMAP_PREDIRTY=$(git status --porcelain context/foundation/roadmap.md 2>/dev/null)` — używane w kroku 5 do decyzji o stagingu.
3. Przeczytaj plik. Szukaj użycia `<change-id>` jako `Change ID`:
   - w tabeli `## At a glance` — wiersz, którego komórka kolumny **Change ID** równa się dokładnie `<change-id>`;
   - oraz w treściach `## Foundations` / `## Slices` — blok `### <ID>: …`, który zawiera linię `- **Change ID:** <change-id>`.

   `<ID>` to lokalny identyfikator roadmapy tego elementu (`F-NN` lub `S-NN`). Dopasowanie jest tylko dokładnym ciągiem — slice może tworzyć kilka zmian, dlatego celowe jest, że prawie dopasowanie *nie* zostaje zmienione. **Brak dopasowania** → wyświetl `ℹ context/foundation/roadmap.md has no item with Change ID "<change-id>" — roadmap left untouched.` i pomiń resztę tego kroku.
4. **Znaleziono dopasowanie** → przeczytaj bieżące `- **Status:**` elementu. Jeśli jest już `in-progress` lub `done`, pozostaw go nietkniętym (**tylko do przodu**: nigdy nie cofaj bardziej zaawansowanego statusu) i przejdź do kroku 5. W przeciwnym razie zastosuj obie edycje — każda niezależna i best effort; jeśli celu nie ma tam, gdzie umieszcza go szablon `/10x-roadmap` (ręcznie edytowana lub starsza roadmapa), pomiń tę podedycję, kontynuuj i odnotuj, co pominięto. Dotykaj tylko pola `Status`; pozostaw `Outcome`, `Prerequisites`, `Change ID` itd. bez zmian.
   1. **`## At a glance`** — w dopasowanym wierszu ustaw komórkę kolumny **Status** na `in-progress`.
   2. **Treść elementu** — przepisz linię `- **Status:**` elementu na `- **Status:** in-progress`.

   Następnie zwiększ wartość frontmatter `updated:` roadmapy do `<today>` (pozostaw każdy inny klucz bez zmian; pomiń to, jeśli plik nie ma frontmatter).
5. **Włącz zmianę do historii tej zmiany.** Jeśli `git` jest dostępny **oraz** `ROADMAP_PREDIRTY` (krok 2) był pusty, dodaj `context/foundation/roadmap.md` do zestawu zmodyfikowanych plików bieżącej fazy, aby zmiana statusu trafiła do commitu fazy zamiast pozostać jako brudna. Jeśli `ROADMAP_PREDIRTY` był niepusty, plik już miał niezatwierdzone edycje: pozostaw zmianę w worktree, zachowaj `context/foundation/roadmap.md` POZA zestawem zmodyfikowanych plików i wyświetl `⚠ context/foundation/roadmap.md had pre-existing uncommitted changes — flipped roadmap item <ID> to in-progress in the working tree but did NOT stage it. Commit it yourself.` Jeśli `git` jest niedostępny, edycja po prostu pozostaje w worktree.

## Podejście do weryfikacji

Po implementacji fazy uruchom tę stałą sekwencję — kanoniczną kolejność dla wszystkiego pomiędzy „kod napisany” a „commit utworzony”. Bramki są uruchamiane od najtańszych, staging jest tam, gdzie potrzebuje go kontrola zepsucia, a rytuał commitu zamyka sekwencję. Wyświetl jednoliniowy werdykt po każdej bramce — `GATE <name>: PASS` lub `GATE <name>: FAIL (<summary>, attempt <k>/2)` — aby użytkownik widział, co faktycznie uruchomiono, bez ponownego czytania przewijanej historii.

1. **(a) Kryteria planu** — uruchom polecenia kryteriów sukcesu `#### Automated` fazy z planu, w kolejności. Każde polecenie jest osobną bramką z własną linią werdyktu.

2. **Wykonaj staging zestawu zmodyfikowanych plików** — uruchom kroki 2–4 rytuału commitu poniżej („Compute the staging set”, „Detect unrelated dirty paths”, „Stage explicitly by path”) *tutaj*, a nie w czasie commitu. Staging przed kontrolą zepsucia sprawia, że jej przywrócenie jest dokładne: `git checkout -- <file>` resetuje worktree do wersji w stagingu, więc celowe zepsucie nigdy nie może przeciec do commitu.

3. **(b) Kontrola celowego zepsucia** — tylko dla faz, które dodają lub zmieniają testy. Przy plikach fazy w stagingu zweryfikuj, że nowy lub zmieniony test faktycznie coś chroni:

   1. Odwróć lub osłab chronione zachowanie w kodzie produkcyjnym — edycja tylko w worktree, nigdy w stagingu.
   2. Uruchom odpowiedni test (uruchomienie zakresowe, np. pojedynczy plik testowy).
   3. Potwierdź, że kończy się niepowodzeniem. Czerwony wynik jest tutaj warunkiem sukcesu: `GATE break-check: PASS (test went red on broken code)`.
   4. Przywróć bezwarunkowo przez `git checkout -- <file>` — resetuje to worktree dokładnie do wersji w stagingu, więc zepsucie nigdy nie może przeciec do commitu.
   5. Zgłoś sekwencję (co zostało zepsute, że test stał się czerwony, że plik został przywrócony).

   Jeśli test **pozostaje zielony** na zepsutym kodzie, asercja niczego nie chroni — jest to niepowodzenie bramki. Napraw to przez wzmocnienie asercji, nigdy przez osłabienie kodu produkcyjnego lub pominięcie kontroli. Edycja zepsucia nigdy nie może zostać zacommitowana; przywrócenie w kroku 4 jest bezwarunkowe, również na ścieżce niepowodzenia.

4. **(c) Kontrole dla całego repozytorium** — pełny zestaw testów, lint, typecheck, wszędzie tam, gdzie określa je plan lub repozytorium (np. skrypt `ci:local`, `make check test`). Jedna linia werdyktu dla każdego.

5. **(d) Commit** — niezmiennik commit-only-on-green: nigdy nie rozpoczynaj rytuału commitu, gdy dowolna powyższa bramka jest czerwona. Nie ma wyjątku, a „naprawię to w następnej fazie” nim nie jest. Jeśli poprawka bramki (b) lub (c) zmieniła pliki, ponownie uruchom krok 2, aby je objąć, a następnie uruchom rytuał commitu na końcu fazy poniżej — jego kroki stagingu są no-op, jeśli od tego czasu nic się nie zmieniło.

**Gdy bramka nie powiedzie się**, popraw ją samodzielnie najwyżej dwa razy; numeruj próby w liniach werdyktów (`attempt 1/2`, `attempt 2/2`). Jeśli ta sama bramka nie powiedzie się trzeci raz, przestań poprawiać i przekaż ją użytkownikowi wraz z wynikiem niepowodzenia — problem jest głębszy niż mechaniczny dryf, a trzecia ślepa próba zwykle pogarsza diff. Nigdy nie osłabiaj asercji, nie usuwaj testu ani nie łagodź reguły lint lub typecheck, aby bramka przeszła, chyba że plan wyraźnie to mówi: napraw kod tak, aby spełniał kontrolę, a nie kontrolę tak, aby spełniała kod. Gdy oczekiwana wartość testu jest rzeczywiście niejednoznaczna — plan i implementacja się nie zgadzają, a nie ma niezależnego źródła prawidłowej odpowiedzi — nie zgaduj; pozostaw werdykt uczciwy i zapytaj.

Równolegle z sekwencją:

- Aktualizuj postęp w swoich todos i w sekcji `## Progress` planu
- **Modyfikuj WYŁĄCZNIE sekcję `## Progress`.** Bloki faz (Overview, Changes Required, Success Criteria) są tylko do odczytu. Zmień `- [ ] N.M <title>` → `- [x] N.M <title>` w Progress w miarę ukończenia każdego kroku. NIE edytuj wypunktowań bloków faz, NIE dodawaj znaczników postępu HTML comment na dole planu i NIE zapisuj żadnego sidecar pliku stanu.
- **Uruchom rytuał commitu na końcu fazy**: bramka (d) powyżej. Gdy każda bramka będzie zielona, przejdź przez ten sekwencjonowany rytuał, aby utworzyć jeden commit Conventional-Commits i zapisać końcowy krótki SHA z powrotem w każdym wierszu Progress zmienionym podczas fazy.

  1. **Bramka ręcznego potwierdzenia.** Poinformuj człowieka, że automatyczna weryfikacja przeszła, i wypisz elementy ręcznej weryfikacji z planu. Zatrzymaj się tutaj. Nie przechodź dalej, dopóki człowiek nie potwierdzi powodzenia testów ręcznych. Użyj tego formatu:

     ```
     Phase [N] Complete - Ready for Manual Verification

     Automated verification passed:
     - [List automated checks that passed]

     Please perform the manual verification steps listed in the plan:
     - [List manual verification items from the plan]

     Let me know when manual testing is complete so I can proceed to the commit step.
     ```

     **Międzyfazowe zestawienie ręczne (tylko faza końcowa).** Przed wyświetleniem komunikatu bramki ustal, czy bieżąca faza jest końcowa: przeskanuj sekcję `## Progress` pod kątem nagłówków `### Phase M:` i uznaj bieżącą fazę za końcową wtedy i tylko wtedy, gdy w kolejności dokumentu nie istnieje nagłówek z `M > N`. Jeśli bieżąca faza **nie** jest końcowa, komunikat bramki ma dokładnie powyższy format — bez zestawienia. Jeśli bieżąca faza **jest** końcowa, po bloku „Please perform the manual verification steps listed in the plan:” przeskanuj całą sekcję Progress pod kątem wierszy `- [ ]`, które znajdują się w podsekcji `#### Manual` dowolnej fazy **innej niż bieżąca**. Jeśli istnieją takie wiersze, dołącz następujący blok do komunikatu bramki (w kolejności dokumentu, jeden wiersz na linię, sformatowany jako `<phase>.<index> <title>` — usuń dowolny prefiks `- [ ]` oraz dowolny końcowy sufiks ` — <sha>`):

     ```
     Pending manual checks from earlier phases:
     - [phase.index title]
     ```

     Jeśli nie ma oczekujących ręcznych wierszy z wcześniejszych faz, całkowicie pomiń blok zestawienia. Bramka nadal zatrzymuje się na potwierdzenie człowieka; to informacja, nie twarda blokada. Fazy pośrednie (każda faza, która nie jest końcowa) zachowują oryginalny format bramki bez zestawienia.

  2. **Oblicz zestaw stagingu.** Kroki 2–4 zostały już uruchomione raz jako krok 2 stosu bramek; uruchom je ponownie tutaj, aby objąć wszystko, czego dotknęła poprawka bramki. Gdy od tego czasu nic się nie zmieniło, są no-op. Weź zestaw zmodyfikowanych plików utrzymywany podczas fazy (zob. „Tracking files touched during a phase” powyżej) i połącz go z `{context/changes/<change-id>/plan.md}`. Plik planu jest zawsze w stagingu, ponieważ każda faza powoduje co najmniej jedną edycję swojej sekcji `## Progress`.

  3. **Wykryj niezwiązane brudne ścieżki.** Uruchom `git status --porcelain` i wykonaj część wspólną ze ścieżkami *poza* zestawem stagingu. Jeśli zestaw brudnych, ale nietkniętych ścieżek nie jest pusty, przedstaw problematyczne ścieżki i zapytaj użytkownika: „`<N>` unrelated path(s) are dirty. How should I handle them?”

     - **Continue — stage only the planned set (Recommended)**: Commituj tylko pliki, których dotknęła ta faza. Pozostaw niezwiązane ścieżki brudne, aby obsłużyć je osobno.
     - **Stage all**: Dodaj niezwiązane ścieżki do tego commitu. Bierzesz odpowiedzialność za szerszy zakres.
     - **Abort**: Zatrzymaj commit fazy. Najpierw rozwiąż brudne ścieżki, a następnie ponownie uruchom rytuał.

     Jeśli zestaw brudnych, ale nietkniętych ścieżek jest pusty, pomiń ten krok.

  4. **Wykonaj staging jawnie według ścieżki.** Wykonaj `git add` dla każdego pliku w wybranym zestawie według nazwy. NIE używaj `git add -A` ani `git add .` — tylko jawne ścieżki.

  5. **Sprawdź pusty diff.** Uruchom `git diff --cached --quiet`. Kod wyjścia 0 oznacza brak diffu w stagingu. Jeśli jest pusty, wyświetl:

     ```
     Phase [N] had no diff to commit; rows remain SHA-less; archive warn-only will surface them.
     ```

     Ustaw `SHA=""` i przejdź do kroku 8.

  6. **Zaproponuj wiadomość Conventional-Commits.** Utwórz linię tematu w formie `<type>(<change-id>): <phase title> (p<N>)`, gdzie `<type>` jest jednym z `feat / fix / chore / refactor / docs`, wybranym na podstawie charakteru fazy (np. `feat` dla nowego widocznego dla użytkownika zachowania, `chore` dla edycji promptów/dokumentacji, `refactor` dla restrukturyzacji bez zmiany zachowania). Tytuł fazy jest istotną częścią i prowadzi; sufiks `(p<N>)` niesie indeks fazy. Utwórz krótką treść z listą zmodyfikowanych plików oraz linią `Refs:` z „Tracking issue/task references for commits”, jeśli dotyczy. Zapytaj użytkownika: „Approve commit message?”

     - **Approve as proposed (Recommended)**: Użyj przygotowanej wiadomości.
     - **Edit subject line**: Nadpisz temat; zachowaj treść.
     - **Override entirely**: Zastąp zarówno temat, jak i treść.

  7. **Wykonaj commit przez heredoc.** Uruchom `git commit` zgodnie z globalnym protokołem wiadomości commit:

     ```bash
     git commit -m "$(cat <<'EOF'
     <type>(<change-id>): <phase title> (p<N>)

     <short body listing touched files>
     <Refs: issue/task references, if applicable>
     EOF
     )"
     ```

     Nigdy nie przekazuj flag `--no-verify`, `--amend` ani flag omijających podpisywanie. Jeśli hook pre-commit nie powiedzie się, napraw podstawowy problem i utwórz NOWY commit — oryginalny commit NIE nastąpił, więc amending dotknąłby commitu poprzedniej fazy.

  8. **Pobierz krótki SHA.** Uruchom `git rev-parse --short HEAD` i zapisz jako `SHA`. Pomiń ten krok, jeśli `SHA=""` zostało ustawione w kroku 5.

  9. **Zapisz SHA z powrotem do Progress.** Dla każdego wiersza Progress zmienionego podczas tej fazy wykonaj ukierunkowaną edycję:

     - Znajdź: `- [x] N.M <title>` (bez istniejącego sufiksu ` — <sha>` na końcu linii)
     - Zastąp przez: `- [x] N.M <title> — <SHA>`

     Pomiń wiersze, które już mają sufiks SHA (bezpieczeństwo wznowienia: jeśli rytuał zostanie ponownie uruchomiony po częściowym wykonaniu, nie dołączaj podwójnie). Jeśli `SHA=""`, całkowicie pomiń dołączanie — wiersze pozostają bez SHA, a `/10x-archive` pokaże je jako ostrzeżenia informacyjne w ramach swojej kontroli soft-warning brakującego SHA.

  10. **Zaktualizuj `change.md`.** Ustaw `updated: <today>`; zachowaj `status: implementing` (idempotentne do końcowej fazy). W końcowej fazie ustaw `status: implemented` po zapisaniu SHA (zob. „After all phases” poniżej).

  11. **Zresetuj zestaw zmodyfikowanych plików.** Wyczyść go przed rozpoczęciem następnej fazy. Rytuał jest samowystarczalny dla każdej fazy.

- **Decyzja o następnej fazie**: Jeśli istnieje następna faza, pomóż użytkownikowi zdecydować, czy kontynuować, czy rozpocząć od nowa.

  Zapytaj użytkownika: „Phase [N] complete. How to proceed?”

  - **Continue to Phase [N+1] — delegate**: Przejdź do następnej fazy, w której kod napisze subagent. Utrzymuje to ten kontekst zwięzłym; po jego powrocie zobaczysz zmodyfikowane pliki, dostosowania i werdykty bramek.
  - **Continue to Phase [N+1] — in context**: Przejdź do następnej fazy i pisz kod tutaj, gdzie można obserwować wprowadzane edycje i przekierować pracę w trakcie fazy.
  - **Clear context first**: Skopiuj polecenie wznowienia do schowka. Zacznij od nowa dla Phase [N+1].
  - **Review this phase first**: Uruchom /10x-impl-review, aby zweryfikować implementację względem planu przed kontynuacją.

  **Jeśli użytkownik wybierze review**: Uruchom `/10x-impl-review @[path-to-plan] phase [N]`, aby przejrzeć właśnie ukończoną fazę. Po zakończeniu przeglądu ponownie przedstaw decyzję kontynuacji/wyczyszczenia (tym razem bez opcji review).

  **Jeśli użytkownik wybierze którąkolwiek opcję kontynuowania**: Przejdź bezpośrednio do następnej fazy — przeczytaj sekcję planu dla następnej fazy, ustaw zadanie na `in_progress` i zaimplementuj je w wybranym trybie wykonania (zob. „Per-phase execution mode”). Nie trzeba ponownie czytać całego planu ani już załadowanych plików; delegowana faza nadal otrzymuje własną sekcję planu dosłownie w prompcie delegowania.

  **Jeśli użytkownik wybierze wyczyszczenie**: Skopiuj polecenie wznowienia do schowka i wyświetl je:
  1. Skopiuj:
     ```bash
     echo -n "/10x-implement <change-id> phase [next-phase-number]" | pbcopy 2>/dev/null || echo -n "/10x-implement <change-id> phase [next-phase-number]" | clip.exe 2>/dev/null || echo -n "/10x-implement <change-id> phase [next-phase-number]" | xclip -selection clipboard 2>/dev/null || true
     ```

     ```powershell
     # PowerShell (Windows)
     Set-Clipboard "/10x-implement <change-id> phase [next-phase-number]"
     ```
  2. Wyświetl:
     ```
     → /10x-implement <change-id> phase [next-phase-number] (✓ copied)
     ```

Jeśli otrzymasz instrukcję wykonania kilku faz kolejno, pomiń pytanie użytkownika między fazami i przenoś dalej ostatnio wybrany tryb wykonania.

nie odznaczaj elementów w krokach testów ręcznych, dopóki użytkownik ich nie potwierdzi.

## Śledzenie stanu

**Sekcja `## Progress` w `plan.md` jest jedynym źródłem prawdy.** Brak pliku stanu. Brak znaczników komentarzy. Zobacz `references/progress-format.md`, aby poznać kontrakt formatu.

### Po każdym kroku

Zmień dokładnie jedną linię Progress naraz:

- Znajdź: `- [ ] N.M <title>`
- Zastąp przez: `- [x] N.M <title>`

Nie dołączaj sufiksu SHA przy edycji pojedynczego kroku — SHA jest zapisywane z powrotem na końcu fazy przez rytuał commitu (zob. „Verification Approach” powyżej), a tylko SHA końcowego commitu trafia do każdego wiersza zmienionego podczas fazy. W trakcie fazy ukończone wiersze mają `[x]` bez sufiksu SHA; jest to prawidłowy stan pośredni.

### Po każdej fazie

Gdy wszystkie elementy `- [ ]` wewnątrz `### Phase N:` są teraz `- [x]`:

1. Uruchom rytuał commitu na końcu fazy (zob. „Verification Approach” powyżej): ręczne potwierdzenie → staging → monit o brudne ścieżki → commit → zapis SHA.
2. `change.md.updated` jest zwiększane jako część kroku 10 rytuału.

Fazy z pustym diffem (tylko ręczna weryfikacja lub fazy no-op dostosowane do sytuacji) niczego nie commitują i pozostawiają wiersze bez SHA; `/10x-archive` pokaże je jako ostrzeżenia informacyjne w ramach kontroli soft-warning brakującego SHA. Jest to zamierzone — nie każda faza tworzy kod.

### Po wszystkich fazach

Gdy każde `- [ ]` w całej sekcji `## Progress` jest teraz `- [x]`:

1. **Defensywne ujawnienie oczekujących elementów.** Przeskanuj ponownie całą sekcję `## Progress` po raz ostatni pod kątem wierszy `- [ ]`. W normalnym przepływie jest to no-op — warunek uruchamiający „After all phases” brzmi już „każde `- [ ]` jest `- [x]`”, więc ujawnienie nie powinno nic znaleźć. Istnieje po to, aby jawnie pokazać wszelkie niespodziewane pozostałości zamiast po cichu je zgubić (np. jeśli częściowe uruchomienie, ręczna edycja lub ścieżka wznowienia ominęły wyzwalacz). Jeśli liczba jest niezerowa, wypisz każdy wiersz jako `<phase>.<index> <title>`, pogrupowany według podsekcji Automated vs Manual w kolejności dokumentu, a następnie zapytaj użytkownika: „`<N>` Progress item(s) still pending. How to proceed?”

   - **Pause (Recommended)**: ZATRZYMAJ się bez zmiany `change.md.status`. Obsłuż ręcznie pozostałości, a następnie ponownie wejdź na ścieżkę epilogu.
   - **Proceed to epilogue**: Mimo wszystko zmień status na implemented i uruchom commit epilogu. Pozostałości pojawią się jako ostrzeżenia w `/10x-archive`.

   Przy „Pause”: ZATRZYMAJ się natychmiast. NIE aktualizuj `change.md`, NIE uruchamiaj commitu epilogu. Przy „Proceed to epilogue”: kontynuuj krokami 2–4 poniżej. Jeśli liczba wynosi zero, pomiń ten krok i kontynuuj.

2. Zaktualizuj `change.md`: ustaw `status: implemented`, `updated: <today>`. (NIE ustawiaj `archived_at` — należy to do `/10x-archive`.)
3. NIE zapisuj żadnego znacznika postępu HTML comment na dole planu.
4. **Uruchom commit epilogu.** Commit końcowej fazy nie może zawierać własnego SHA (problem kury i jajka), więc zapis SHA z powrotem do wierszy Progress końcowej fazy oraz zmiana statusu `change.md` pozostają brudne w worktree po powrocie rytuału końcowej fazy. Utwórz jeden zamykający commit, aby je zapisać — w przeciwnym razie twarda bramka odmowy `/10x-archive` (niezatwierdzone ścieżki wewnątrz folderu zmiany) zablokuje działanie. Kroki:
   1. Wykonaj staging dokładnie `context/changes/<change-id>/plan.md` i `context/changes/<change-id>/change.md` (jawne ścieżki, bez `git add -A`).
   2. Uruchom `git diff --cached --quiet`; jeśli kod wyjścia wynosi 0, pomiń epilog (brak końcowych zmian do commitu) i zatrzymaj się tutaj.
   3. Zaproponuj temat `chore(<change-id>): close out plan (epilogue)` wraz z krótką treścią wskazującą końcowy zapis SHA planu + `change.md` → implemented oraz linię `Refs:` z „Tracking issue/task references for commits”, jeśli dotyczy. Poproś użytkownika o zatwierdzenie zgodnie z propozycją, edycję tematu lub całkowite nadpisanie (te same opcje co w rytuale fazy).
   4. Wykonaj commit przez heredoc zgodnie z globalnym protokołem (nigdy `--no-verify` / `--amend`).
   5. NIE zapisuj własnego SHA epilogu z powrotem do planu — jego jedynym zadaniem jest czyste zapisanie końcowych edycji.

### „Where am I?” — wyprowadzane, nie przechowywane

Przeanalizuj sekcję `## Progress`. Pierwsza linia `- [ ]` jest następnym krokiem. Bieżąca faza to nagłówek `### Phase N:` bezpośrednio nad nią. Ukończenie to `count([x]) / count([ ] + [x])`. Bez JSON, bez znaczników, bez sidecar — tylko sekcja Progress.

## Ukończenie planu

Gdy WSZYSTKIE fazy są zaimplementowane i zweryfikowane (każde pole wyboru Progress ma `[x]`):

1. Potwierdź, że `change.md.status` ma teraz wartość `implemented`.
2. Przedstaw podsumowanie ukończenia, a następnie zaoferuj końcowy przegląd:

```
All phases implemented! 🎉

Summary:
- Phases completed: [N]
- Files changed: [list key files]
```

Zapytaj użytkownika: „Plan complete. Would you like a final implementation review?”

- **Run full review (/10x-impl-review)**: Kompleksowy przegląd wszystkich faz względem planu. Wykrywa problemy międzyfazowe.
- **Skip review — I'm satisfied**: Przegląd nie jest potrzebny. Oznacz plan jako ukończony.

Jeśli użytkownik wybierze review → uruchom `/10x-impl-review <change-id>` (brak numeru fazy = pełny przegląd planu).

## Jeśli utkniesz

Gdy coś nie działa zgodnie z oczekiwaniami:

- Najpierw upewnij się, że przeczytałeś i zrozumiałeś cały istotny kod
- Rozważ, czy baza kodu ewoluowała od czasu napisania planu
- Przedstaw rozbieżność jasno i poproś o wskazówki

Zobacz „When you're stuck or in unfamiliar territory” w [references/implementation-discipline.md](references/implementation-discipline.md), aby poznać sekwencję szukaj-potem-analizuj oraz co robić, gdy właściwa wartość jest rzeczywiście niejednoznaczna.

Poza delegowaną fazą (zob. „Per-phase execution mode”), sięgnij po podzadanie, gdy jest tego warte — do ukierunkowanego debugowania albo eksploracji nieznanego obszaru:

- **Explore** — Szybkie wyszukiwanie plików, wzorców, podobnego kodu
- **general-purpose** — Głęboka analiza wymagająca wieloetapowego rozumowania

## Wznawianie pracy

Jeśli sekcja `## Progress` planu ma istniejące oznaczenia `[x]`:

- Ufaj, że ukończona praca jest wykonana
- Podejmij pracę od pierwszej linii `- [ ]`
- Weryfikuj poprzednią pracę tylko wtedy, gdy coś wydaje się nie tak

Pamiętaj: wdrażasz rozwiązanie, a nie tylko odhaczasz pola. Miej na uwadze cel końcowy i utrzymuj tempo naprzód.