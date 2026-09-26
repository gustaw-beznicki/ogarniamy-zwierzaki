---
name: 10x-plan
description: Create detailed implementation plans with thorough research and iteration
---
# Plan wdrożenia

Twoim zadaniem jest tworzenie szczegółowych planów wdrożenia poprzez interaktywny, iteracyjny proces. Powinieneś być sceptyczny, dokładny i współpracować z użytkownikiem, aby tworzyć wysokiej jakości specyfikacje techniczne.

## Natywna obsługa pytań

Najpierw skieruj wywołanie: `/10x-plan <change-id> save` (również `$10x-plan <change-id> save`) albo prośba o zapisanie już uzgodnionego planu po zmianie trybu wznawia **utrwalanie**, a nie wywiad. Przeczytaj [references/plan-persistence.md](references/plan-persistence.md) i postępuj zgodnie z jego ścieżką zapisu przed sprawdzeniem warunków wstępnych pytań. Brak możliwości zadawania pytań nie jest blokadą, gdy nie pozostały nierozstrzygnięte decyzje.

`Ask the user:` poniżej oznacza rzeczywistą, ustrukturyzowaną funkcję pytań bieżącego środowiska. Zachowaj znaczenie pytań oraz format rekomendacji/kompromisów podczas mapowania argumentów; rzeczywiście obsługiwany schemat określa liczbę opcji i limity pól. Używaj mniejszej liczby opcji albo dziel niezależne pytania, gdy limit hosta jest bardziej restrykcyjny. Jeśli host wymaga unikalnego `id` pytania albo nie udostępnia obsługi wielokrotnego wyboru, użyj osobnych pytań jednokrotnego wyboru, gdy wybory są niezależne. Mapuj wielokrotny wybór i niestandardowe wprowadzanie tekstu zgodnie ze schematem obsługiwanym przez hosta.

Przed pierwszym pytaniem sprawdź funkcję faktycznie dostępną w bieżącej sesji. Natywne ustrukturyzowane pytania mogą wymagać interaktywnego trybu planowania lub współpracy. Wywołanie tej umiejętności nie zmienia trybu hosta, a nieinteraktywne wykonanie może nie być punktem wejścia do interaktywnych pytań. Host/kontroler musi rozpocząć interaktywną turę w trybie obsługującym pytania. Użyj sesji, której natywna funkcja pytań ma podłączonego klienta pytań; nienadzorowane uruchomienie wyłącznie tekstowe nie jest dowodem, że użytkownik może odpowiadać. Jeśli wymagana natywna funkcja jest niedostępna, nazwij brakującą funkcję i zatrzymaj się, zanim założysz odpowiedzi lub utworzysz plan. Zalecaj przełączenie do interaktywnego trybu planowania lub współpracy albo ponowne podłączenie interaktywnego klienta pytań i wznowienie sesji. Nie twierdź, że pytanie zostało przesłane, jeśli jedynie wyświetliłeś tekst. Jawne nieinteraktywne zadanie, które zatrzymuje się z powodu brakujących warunków wstępnych, może nadal zgłaszać te warunki bez rozpoczynania wywiadu.

Używaj rozmiaru rundy obsługiwanego przez natywne narzędzie, z poszanowaniem bardziej restrykcyjnego natywnego limitu. Potwierdzony budżet pytań jest **łączny dla wszystkich rund**, a nie wymogiem upchnięcia wszystkich pytań w pierwszym wywołaniu. Pytaj z poziomu głównego agenta; podagenci badawczy nie prowadzą wywiadu z użytkownikiem.

Sprawdzaj możliwość zapisu niezależnie od możliwości zadawania pytań. Gdy host zezwala na pytania, ale zabrania zapisów w repozytorium, przeczytaj [references/plan-persistence.md](references/plan-persistence.md). Wyjaśnij na początku, że ta sesja przygotuje kompletny plan i brief w rozmowie, a następnie nastąpi krok zapisu w trybie z możliwością zapisu w tej samej rozmowie. Umiejętność nie może zmienić trybu hosta. Zachowaj najnowsze decyzje i poprawki podczas tego przejścia; nie rozpoczynaj wywiadu od nowa.

## Odpowiedź początkowa

Gdy to polecenie zostanie wywołane:

1. **Sprawdź, czy podano parametry**:
   - Jeśli jako parametr podano ścieżkę pliku lub odwołanie do zgłoszenia, pomiń domyślną wiadomość
   - Natychmiast przeczytaj WSZYSTKIE podane pliki w CAŁOŚCI
   - Rozpocznij proces badawczy

2. **Jeśli nie podano parametrów**, odpowiedz:

```
Pomogę Ci utworzyć szczegółowy plan wdrożenia. Zacznijmy od zrozumienia, co budujemy.

Podaj proszę:
1. Opis zadania/zgłoszenia (lub odwołanie do pliku zgłoszenia)
2. Wszelki istotny kontekst, ograniczenia lub konkretne wymagania
3. Linki do powiązanych badań lub wcześniejszych wdrożeń

Im więcej kontekstu z wcześniejszych etapów przekażesz, tym mniej pytań zadam:
- Sam opis zadania → pełny zestaw pytań
- Zadanie + dokument badawczy (`context/changes/<change-id>/research.md`) → mniej pytań; nie powtórzę tego, co obejmują badania
- Zadanie + brief ramowy (`context/changes/<change-id>/frame.md`) → znacznie mniej pytań; ujęcie problemu jest już ustalone
- Zadanie + rama + badania → minimalna liczba pytań; skupiam się wyłącznie na decyzjach projektowych rozwiązania, które wymagają Twojego wkładu

Wskazówka: wywołaj bezpośrednio z identyfikatorem zmiany lub ścieżką — `/10x-plan oauth-login` lub `/10x-plan @context/changes/oauth-login/frame.md`
Aby uzyskać głębszą analizę, spróbuj: `/10x-plan think deeply about @context/changes/oauth-login/research.md`
```

Następnie poczekaj na dane wejściowe użytkownika.

## Kroki procesu

### Krok 1: Zebranie kontekstu i wstępna analiza

#### Krok 1.0: Zidentyfikuj artefakty wcześniejszych etapów i dostosuj głębokość pytań

Przed jakimkolwiek czytaniem zidentyfikuj, jakie rodzaje artefaktów wcześniejszych etapów przekazał użytkownik. Każdy z nich reprezentuje już podjęte decyzje — nie pytaj o nie ponownie.

- **Brief ramowy** — ścieżka pasuje do `context/changes/<change-id>/frame.md` albo treść zaczyna się od `# Frame Brief:` / zawiera sekcję `## Reframed`.
- **Dokument badawczy** — ścieżka pasuje do `context/changes/<change-id>/research.md` albo YAML frontmatter zawiera pola `topic:` i `researcher:`.
- **Istniejący plan** — ścieżka pasuje do `context/changes/<change-id>/plan.md` (tryb wznowienia/doprecyzowania — poza zakresem tej logiki skalowania).
- **Wyłącznie opis zadania** — żadne z powyższych.

**Liczba pytań i ich zakres zależą od tego, co dostarczono:**

| Artefakty wcześniejszych etapów | NISKI | ŚREDNI | WYSOKI | Co się zmienia względem wariantu bazowego |
| --------------------------- | ----- | ------ | ----- | -------------------------------------------------------------------------------------------------------------------------------------- |
| Tylko zadanie (wariant bazowy) | 4–6 | 7–10 | 11–15 | Pełny zestaw pytań we wszystkich istotnych kategoriach. |
| Zadanie + badania | 3–5 | 5–7 | 8–11 | Pomiń pytania, na które odpowiedź już znajduje się w dokumencie badawczym. Nie uruchamiaj ponownie podagentów, aby znaleźć to, co badania już zmapowały. |
| Zadanie + rama | 2–3 | 4–6 | 7–9 | Pomiń kategorie [D]iagnostyczne — rama ustaliła ujęcie problemu. Traktuj Przeformułowane (lub Potwierdzone) Stwierdzenie Problemu jako wiążące. |
| Zadanie + rama + badania | 1–2 | 3–5 | 5–7 | Pomiń oba rodzaje. Zadawaj wyłącznie pytania dotyczące projektu [S]olution, które rzeczywiście wymagają wkładu użytkownika. |

**Wyjątek dotyczący rozstrzygniętych danych wejściowych:** jeśli artefakty wcześniejszych etapów już rozstrzygają każdą istotną decyzję dotyczącą rozwiązania, zaproponuj **0 merytorycznych pytań**, krótko wskaż te dowody i uzyskaj zwykłe natywne potwierdzenie złożoności/budżetu. Zachowaj akceptację natywnej struktury. Jeśli uzgodniony budżet stanie się niepotrzebny po późniejszych odpowiedziach, potwierdź korektę zamiast sztucznie wypełniać pytania. Powyższe zakresy wskazują nierozstrzygniętą pracę; nie wymagają wymyślonych decyzji.

**Zasada**: każdy przekazany artefakt jest źródłem już podjętych decyzji. Czytanie ich jest równoznaczne ze słuchaniem użytkownika. Nie pytaj użytkownika o to, co już zapisał.

**Gdy obecna jest rama**, przeczytaj ją W CAŁOŚCI i traktuj jako wiążącą:
- Skopiuj **Reported Observation** + **Reframed (or Confirmed) Problem Statement** jako definicję zadania. Nie podważaj ponownie ujęcia problemu.
- Przenieś tabelę **Hypothesis Investigation** oraz **Narrowing Signals** do swojej „Analizy stanu bieżącego” — ta praca została już wykonana.
- Jeśli oznaczono **Confidence: LOW** ramy, uwzględnij to w sekcji „Otwarte ryzyka i założenia” planu i zadaj JEDNO pytanie doprecyzowujące, jak postąpić (najpierw zweryfikować czy planować z uznanym ryzykiem).
- NIE badaj ponownie ujęcia problemu. Rama odpowiada za ujęcie problemu; Ty odpowiadasz za projekt rozwiązania.

**Gdy obecne są badania**, przeczytaj je W CAŁOŚCI i użyj jako podstawy wiedzy o bazie kodu:
- Sekcja „Code References” JEST Twoim ugruntowaniem w bazie kodu — nie uruchamiaj ponownie agentów Explore, aby znaleźć te same pliki.
- „Architecture Insights” trafiają bezpośrednio do „Current State Analysis.”
- Uruchamiaj podagentów wyłącznie, aby wypełnić konkretne luki, których badania nie objęły (np. dokładne pliki, które zmodyfikuje ten plan, jeśli badania miały szerszy zakres).

#### Krok 1.1: Czytanie i badanie

1. **Natychmiast przeczytaj WSZYSTKIE wspomniane pliki w CAŁOŚCI**:
   - Pliki referencyjne (np. `context/changes/<change-id>/research.md`, `context/changes/<change-id>/frame.md`)
   - Dokumenty badawcze
   - Briefy ramowe
   - Powiązane plany wdrożenia
   - Wszelkie wspomniane pliki JSON/danych
   - `context/foundation/lessons.md`, jeśli istnieje — traktuj jego reguły jako priory podczas badania zakresu, przypadków brzegowych i wyborów architektonicznych; reguły już zaakceptowane przez zespół zawężają pulę pułapek projektowych, które nadal wymagają nowych pytań.
   - **WAŻNE**: Czytaj całe pliki bez parametrów limitu lub przesunięcia
   - **KRYTYCZNE**: NIE uruchamiaj podzadań przed samodzielnym przeczytaniem tych plików w głównym kontekście
   - **NIGDY** nie czytaj plików częściowo — jeśli plik jest wspomniany, przeczytaj go w całości

2. **Uruchom równoległe badania przed wywiadem**:
   Przed zadaniem użytkownikowi jakichkolwiek pytań deleguj nierozstrzygnięte luki dowodowe do podagentów pracujących równolegle — domyślnie 2–3 w pojedynczej wiadomości, każdy nad innym wymiarem wyszukiwania (np. „znajdź wszystkie pliki związane z X”, „znajdź podobne implementacje Y”, „znajdź wcześniejsze decyzje o Z w `context/changes/**/` i `context/archive/**/`”), prosząc każdego o kotwice `file:line`. Przeczytaj [references/task-orchestration.md](references/task-orchestration.md) przed pierwszym delegowaniem, aby poznać kontrakt delegowania, awaryjne rozwiązanie dla możliwości i wymagania dotyczące dowodów. Rozstrzygnij pojedynczy zlokalizowany fakt za pomocą lokalnego odczytu o zawężonym zakresie zamiast delegowania oraz zachowaj pytania produktowe i decyzje międzykomponentowe dla głównego agenta. Deleguj podagentów przy użyciu natywnego odpowiednika hosta do lokalizowania kodu lub wykonywania analizy. Poczekaj na zakończenie wszystkich delegowanych podagentów przed zintegrowaniem ich wyników.

3. **Przeczytaj wszystkie pliki wskazane przez zadania badawcze**:
   - Po zakończeniu zadań badawczych przeczytaj WSZYSTKIE pliki, które wskazały jako istotne
   - Wczytaj je W CAŁOŚCI do głównego kontekstu
   - Zapewnia to pełne zrozumienie przed kontynuowaniem

4. **Przeanalizuj i zweryfikuj zrozumienie**:
   - Porównaj wymagania zgłoszenia z rzeczywistym kodem
   - Zidentyfikuj wszelkie rozbieżności lub nieporozumienia
   - Zanotuj założenia wymagające weryfikacji
   - Ustal rzeczywisty zakres na podstawie realiów bazy kodu
   - **Zbadaj słowa, których żądanie nie definiuje.** Terminy rankingu, wyboru i stanu — „top N”, „latest”, „first”, „winner”, „duplicate”, „active”, „until the end” — ustalają tylko to, co dosłownie mówią. Dla każdego zbuduj najmniejszy przypadek, w którym dwa odczytania dają różne widoczne dla użytkownika wyniki, a następnie sprawdź, co robi kod w tym przypadku; rozstrzygnięcie remisu po id, kolejności wstawienia lub pozycji w tablicy nie jest decyzją, którą ktokolwiek podjął, więc nie rozstrzyga terminu. Każdy termin, którego odczytania się różnią, staje się pytaniem pierwszej rundy. Zobacz [references/question-examples.md](references/question-examples.md#undefined-terms-in-the-request), aby dowiedzieć się, jak zbudować przypadek, sformułować opcję i zapisać wynik.

5. **Przedstaw świadome zrozumienie i oceń złożoność**:

   Najpierw przedstaw krótkie podsumowanie tego, co znalazłeś:

   ```
   Na podstawie [zgłoszenia i moich badań bazy kodu / Twojego opisu i mojej analizy] rozumiem, że musimy [dokładne podsumowanie].

   Znalazłem, że:
   - [Kluczowe odkrycie — odwołanie do kodu, istniejący zasób, wcześniejsza praca lub ograniczenie domenowe]
   - [Istotny wzorzec, konwencja lub odkryte ograniczenie]
   - [Zidentyfikowana potencjalna złożoność lub przypadek brzegowy]
   ```

   Następnie oceń złożoność zadania i przedstaw ją użytkownikowi do potwierdzenia:

   ```
   **Ocena złożoności: [HIGH / MEDIUM / LOW]**

   [2-3 zdania wyjaśniające DLACZEGO ten poziom złożoności, odnosząc się do konkretnych czynników:
   liczby dotkniętych systemów, punktów integracji, potrzeb zarządzania stanem,
   zmian modelu danych, nieznanych niewiadomych, powierzchni testowania itd.]

   Chciałbym zadać **[N] pytań** w kilku rundach, aby doprecyzować ważne
   decyzje dotyczące [wymień kluczowe obszary decyzji: architektura, przypadki brzegowe, model danych, UX, testowanie itd.].

   Czy to wydaje się właściwe, czy zmieniłbyś poziom złożoności?
   ```

   Zapytaj użytkownika:
   - question: "Czy ta ocena złożoności odpowiada Twoim oczekiwaniom?"
     header: "Complexity"
     options:
     - label: "⭐ Recommended: [N] pytań (Recommended)"
       description: "Użyj proponowanego budżetu pytań. · Strength: Koncentruje się na zidentyfikowanych decyzjach. · Tradeoff: Nowo odkryte luki mogą wymagać korekty."
     - label: "Wyższa — zadaj więcej pytań"
       description: "Rozszerz wywiad o brakujące zagadnienia. · Strength: Obejmuje dodatkowe ryzyka. · Tradeoff: Wymaga więcej czasu użytkownika."
     - label: "Niższa — potrzeba mniej pytań"
       description: "Ogranicz wywiad do pozostałych decyzji. · Strength: Unika redundantnych pytań. · Tradeoff: Wymaga zidentyfikowania, które kwestie są już rozstrzygnięte."
       multiSelect: false

   **Skala złożoności:**

   | Poziom | Pytania | Kiedy używać |
   | ---------- | --------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
   | **LOW** | 4-6 | Proste zadanie z jasnymi wymaganiami. Niewiele ruchomych części, podąża za ustalonymi wzorcami lub konwencjami, ograniczone niewiadome. Przykłady programistyczne: zmiana w jednym pliku, korekta konfiguracji. Przykłady nieprogramistyczne: konspekt dotyczący jednego tematu, prosta korekta procesu. |
   | **MEDIUM** | 7-10 | Wiele współdziałających komponentów lub kwestii. Wymaga decyzji projektowych, ma przypadki brzegowe warte omówienia, pewną niejednoznaczność podejścia. Przykłady programistyczne: funkcja wieloplikowa, nowy endpoint API. Przykłady nieprogramistyczne: wieloczęściowy plan treści, przeprojektowanie przepływu pracy, moduł kursu. |
   | **HIGH** | 11-15 | Przekrojowe kwestie, istotne niewiadome, wielu interesariuszy lub ograniczeń. Wymaga myślenia architektonicznego, niesie ryzyko kosztownej przeróbki przy błędzie. Przykłady programistyczne: przeprojektowanie systemu, migracja danych. Przykłady nieprogramistyczne: strategia uruchomienia wielokanałowego, przebudowa programu nauczania, zmiana procesu organizacyjnego. |

   Po potwierdzeniu (lub dostosowaniu) przez użytkownika przejdź do pytań.

6. **Zadawaj dogłębne pytania sondujące**:

   Pytaj o pozostałe decyzje w ramach potwierdzonego łącznego budżetu, używając natywnego rozmiaru rundy powyżej. Budżet obejmuje merytoryczne doprecyzowania; nie jest limitem do wypełnienia ani pozwoleniem na ponowne otwieranie ustalonych interfejsów.

   **Zasady strukturyzowania pytań:**
   - Każde pytanie powinno mieć 2–4 konkretne opcje w ramach rzeczywistego natywnego schematu; użyj 2–3, gdy taki jest limit hosta
   - Używaj `multiSelect: true` tylko wtedy, gdy wybory nie wykluczają się wzajemnie
   - Zachowuj krótki `header` (maks. 12 znaków): „Scope”, „Edge cases”, „Priority”
   - Użytkownik zawsze może wybrać „Other” dla swobodnego tekstu

   **Każde pytanie MUSI mieć jedną rekomendację; każda opcja MUSI zawierać analizę kompromisów:**
   - Umieść dokładnie jedną rekomendowaną opcję **jako pierwszą**, z dokładnym szablonem etykiety `⭐ Recommended: [short choice] (Recommended)`. Zachowaj dosłowny znacznik `⭐ Recommended` oraz natywny sufiks `(Recommended)` razem; skracaj tekst wyboru, nigdy nie skracaj znacznika do `⭐ Rec`. Spełnia to zarówno format umiejętności, jak i natywne umiejscowienie rekomendacji.
   - `description` każdej opcji musi mieć następujący format:
     `[1-zdaniowy opis działania] · Strength: [kluczowa zaleta] · Tradeoff: [kluczowy koszt lub ryzyko]`
   - Rekomendacja powinna opierać się na badaniach (wzorce bazy kodu dla oprogramowania, wiedza domenowa i kontekst dla zadań nieprogramistycznych) — nie na zgadywaniu

   **Sprawdź ładunek przed zadaniem pytania:** każdy `header` ma 1–12 znaków (licząc spacje), każde pytanie ma 2–4 odrębne wybory w rzeczywistym limicie hosta, tylko pierwsza etykieta zawiera dokładny znacznik `⭐ Recommended`, a każdy opis zawiera zarówno ` · Strength: `, jak i ` · Tradeoff: ` z konkretną treścią. Użyj `Format` zamiast 13-znakowego `Output format`. Zweryfikuj same natywne argumenty pytania, a nie tylko tekstowy podgląd.

   **Przykładowe ustrukturyzowane pytanie z rekomendacjami (oprogramowanie — dostarczenie):** `Rollout` to `[S]` — strategia dostarczenia; pytaj tylko wtedy, gdy zmiana może zawieść na produkcji w sposób, który ścieżka wydania musiałaby ograniczyć, w przeciwnym razie odziedzicz domyślną praktykę zespołu.

   Zapytaj użytkownika:
   - question: "Jak nowe obliczenie cen powinno trafić do kont produkcyjnych?"
     header: "Rollout"
     options:
     - label: "⭐ Recommended: Oznaczony kanarek (Recommended)"
       description: "Wydaj za flagą funkcji włączoną najpierw dla 10% kont, a następnie rozszerzaj. · Strength: Błędna cena dotyka ograniczonej grupy i można ją cofnąć zmianą flagi, bez ponownego wdrożenia — wykorzystuje wrapper flagi już kontrolujący przeprojektowanie checkoutu. · Tradeoff: Obie ścieżki obliczeń pozostają aktywne do czasu sprzątania, więc testy cen muszą obejmować każdą z nich."
     - label: "Wydaj wszystkim naraz"
       description: "Wdróż nowe obliczenie dla wszystkich kont w jednym wydaniu. · Strength: Jedna ścieżka kodu od pierwszego dnia — nic do sprzątania i brak obsługi flag. · Tradeoff: Wycofanie oznacza ponowne wdrożenie, a nieprawidłowe faktury już dotarły do klientów."
     - label: "Najpierw uruchomienie cieniowe"
       description: "Obliczaj stare i nowe ceny równolegle, zapisuj różnice w logach, przez dwa tygodnie zwracaj wyłącznie stary wynik. · Strength: Ujawnia rozbieżności na rzeczywistym ruchu bez wpływu na klientów. · Tradeoff: Opóźnia start o okno obserwacji i dodaje log różnic, za który nikt jeszcze nie odpowiada."
     multiSelect: false

   **Przykładowe ustrukturyzowane pytanie z rekomendacjami (oprogramowanie):** `Conflicts` to `[S]` — architektura rozwiązania; pytaj tylko wtedy, gdy decyzja pozostaje nierozstrzygnięta po przeczytaniu artefaktów wcześniejszych etapów.

   Zapytaj użytkownika:
   - question: "Jak system powinien obsługiwać konflikty, gdy dwóch użytkowników edytuje równocześnie?"
     header: "Conflicts"
     options:
     - label: "⭐ Recommended: Wspomagane scalanie (Recommended)"
       description: "Pokaż konflikt użytkownikowi, pozwól mu wybrać wersję do zachowania. · Strength: Zapobiega utracie danych przy zachowaniu prostego UX — odpowiada wzorcowi w istniejącym komponencie EditPanel. · Tradeoff: Dodaje modal rozwiązywania konfliktów i subskrypcję WebSocket do wykrywania w czasie rzeczywistym."
     - label: "Wygrywa ostatni zapis"
       description: "Późniejszy zapis bez ostrzeżenia nadpisuje wcześniejszy. · Strength: Zero dodatkowej złożoności, nie wymaga zmian UI. · Tradeoff: Użytkownicy mogą stracić pracę bez ostrzeżenia — akceptowalne tylko, jeśli edycje są rzadkie lub mało istotne."
     - label: "Oparte na blokadach"
       description: "Pierwszy edytor blokuje zasób; inni widzą tryb tylko do odczytu do czasu zwolnienia. · Strength: Całkowicie zapobiega konfliktom — najprostszy model mentalny dla użytkowników. · Tradeoff: Zaległe blokady wymagają TTL + logiki czyszczenia; blokuje uzasadnioną równoległą pracę."
     multiSelect: false

   **Przykładowe ustrukturyzowane pytanie z rekomendacjami (nieprogramistyczne — treść/strategia):** `Depth` to `[D]` — diagnostyczne pytanie o odbiorców/zakres; pomiń, jeśli brief ramowy już ustalił, dla kogo jest materiał.

   Zapytaj użytkownika:
   - question: "Jaki poziom szczegółowości technicznej powinien mieć moduł kursu?"
     header: "Depth"
     options:
     - label: "⭐ Recommended: Ćwiczenia prowadzone (Recommended)"
       description: "Koncepcje połączone z ćwiczeniami krok po kroku. · Strength: Równoważy zrozumienie i praktykę — odpowiada formatowi, który uzyskał najwyższe wskaźniki ukończenia w 10xDevs2. · Tradeoff: 2-3x więcej czasu przygotowania na lekcję; wymaga działających repozytoriów przykładowych."
     - label: "Przegląd koncepcyjny"
       description: "Zasady wysokiego poziomu, bez kodu. · Strength: Dostępne dla wszystkich poziomów umiejętności, szybsze do przygotowania. · Tradeoff: Zaawansowani uczestnicy mogą uznać materiał za zbyt płytki — ryzyko utraty zaangażowania."
     - label: "Dogłębna analiza z otwartymi wyzwaniami"
       description: "Minimalne wsparcie, problemy z rzeczywistego świata. · Strength: Wymusza autentyczne rozwiązywanie problemów, najwyższe utrwalenie nauki. · Tradeoff: Wysokie ryzyko rezygnacji u mniej doświadczonych uczestników; trudniejsze wsparcie na dużą skalę."
     multiSelect: false

   **O co pytać** — dostosuj kategorie do domeny zadania:

   Najpierw zidentyfikuj domenę zadania: **software**, **content/education**, **strategy/process** albo **hybrid**. Następnie wybierz kategorie pytań, które pasują. Poniższe kategorie są uporządkowane według domen — wybierz to, co istotne, nie narzucaj kategorii programistycznych zadaniom nieprogramistycznym.

   **Każda kategoria jest oznaczona jako `[D]` (diagnostyczna — o problemie) albo `[S]` (solution — o tym, jak je zbudować).** Gdy w Kroku 1.0 dostarczono brief ramowy, **pomiń wszystkie kategorie `[D]`** — rama je rozstrzygnęła. Zawsze zadawaj kategorie `[S]`, dla których nadal potrzebny jest wkład użytkownika.

   **Kategorie uniwersalne (wszystkie domeny, wszystkie poziomy):**
   - **Granice zakresu** `[D]`: Co jest w zakresie, a co poza nim
   - **Przypadki brzegowe / tryby awarii** `[S]`: Co dzieje się, gdy sprawy idą źle lub nietypowo (obsługa implementacyjna, nawet jeśli rama nazwała klasę obserwacji). Zacznij od niezdefiniowanych terminów ujawnionych w Kroku 1.1 — umieść konkretny przypadek w pytaniu zamiast nazywać kategorię
   - **Kryteria sukcesu** `[D]`: Skąd wiemy, że to zadziałało — z perspektywy użytkownika końcowego lub interesariusza
   - **Priorytet** `[D]`: Niezbędne kontra miłe do posiadania — co zostaje wycięte, jeśli czas jest ograniczony

   **Kategorie specyficzne dla oprogramowania (dodaj zależnie od złożoności):**

   MEDIUM+:
   - **Decyzje dotyczące modelu danych** `[S]`: Schemat, relacje, ograniczenia, migracje
   - **Strategia obsługi błędów** `[S]`: Tryby awarii, logika ponawiania, komunikaty dla użytkownika
   - **Podejście do testowania** `[S]`: Poziom pokrycia, które przypadki brzegowe testować jawnie
   - **Granice wydajności** `[S]`: Oczekiwane obciążenie, akceptowalne opóźnienie, cache'owanie

   HIGH:
   - **Wybory architektoniczne** `[S]`: Granice usług, synchronicznie kontra asynchronicznie, sterowane zdarzeniami kontra żądanie-odpowiedź
   - **Zarządzanie stanem** `[S]`: Gdzie znajduje się stan, gwarancje spójności, rozwiązywanie konfliktów
   - **Model bezpieczeństwa** `[S]`: Granice uwierzytelniania, dostęp do danych, walidacja wejścia
   - **Migracja i wycofanie** `[S]`: Wdrożenie przyrostowe, strategia cofania
   - **Obserwowalność** `[S]`: Kluczowe metryki, alerty, powierzchnia debugowania

   **Kategorie treści / edukacji (dodaj zależnie od złożoności):**

   MEDIUM+:
   - **Odbiorcy i wymagania wstępne** `[D]`: Dla kogo to jest, co już wiedzą
   - **Format i medium** `[S]`: Tekst, wideo, interaktywnie, na żywo — i dlaczego
   - **Łuk narracyjny** `[S]`: Jaką drogę przechodzi czytelnik/uczeń
   - **Przykłady i ćwiczenia** `[S]`: Co utrwala koncepcje

   HIGH:
   - **Zależności programu nauczania** `[D]`: Czego trzeba nauczyć się przed czym
   - **Strategia oceny** `[S]`: Jak zweryfikować, że nauka się odbyła
   - **Ponowne wykorzystanie i modułowość** `[S]`: Czy części mogą działać samodzielnie lub w innych kontekstach
   - **Dystrybucja i dostęp** `[D]`: Gdzie to jest, jak ludzie to znajdują

   **Kategorie strategii / procesu (dodaj zależnie od złożoności):**

   MEDIUM+:
   - **Interesariusze i role** `[D]`: Kto uczestniczy, kto decyduje, kto wykonuje
   - **Harmonogram i kamienie milowe** `[S]`: Kluczowe daty, zależności, ścieżka krytyczna
   - **Identyfikacja ryzyka** `[S]`: Co może pójść źle, jaki jest plan awaryjny
   - **Ograniczenia zasobów** `[D]`: Budżet, czas, ludzie, narzędzia

   HIGH:
   - **Zarządzanie zmianą** `[S]`: Jak osoby, których dotyczy zmiana, dowiedzą się o niej i ją przyjmą
   - **Ramy pomiaru** `[D]`: Wskaźniki wyprzedzające kontra opóźnione, jak korygować kurs
   - **Zależności i sekwencjonowanie** `[S]`: Co blokuje co, co może działać równolegle
   - **Plan komunikacji** `[S]`: Kto musi wiedzieć co, kiedy i przez jaki kanał

   **O co NIE pytać:**
   - O cokolwiek już rozstrzygniętego w artefaktach wcześniejszych etapów (brief ramowy, dokument badawczy) — ponowne pytanie jest trybem awarii, któremu ma zapobiec to skalowanie
   - O szczegóły implementacyjne niskiego poziomu, które możesz ustalić samodzielnie (na podstawie badań bazy kodu dla oprogramowania, z plików kontekstowych i wcześniejszej pracy dla zadań nieprogramistycznych)
   - O pytania z oczywistymi odpowiedziami przy już dostarczonym kontekście
   - O preferencje, które nie wpływają na strukturę planu ani sukces

   Użyj Kroku 1.0, aby zaproponować budżet odpowiedni do złożoności i dowodów z wcześniejszych etapów. Uwzględnij każdą istotną nierozstrzygniętą decyzję w ramach potwierdzonego przez użytkownika budżetu; jeśli trzeba, poproś o rozszerzenie przed zadaniem kolejnego merytorycznego pytania. Nie sztucznie wydłużaj wywiadu ani nie otwieraj ponownie ustalonych wyborów, aby osiągnąć sugerowany zakres. Każde pytanie powinno rozstrzygać rzeczywistą lukę.

   Prowadź wewnętrzny rejestr nierozstrzygniętych decyzji użytkownika i najnowszej jawnej odpowiedzi dla każdej z nich. Częściowa odpowiedź lub „nie wiem” pozostawia decyzję otwartą, chyba że użytkownik jawnie deleguje wybór. Mechanizm już obecny w kodzie nie rozstrzyga nierozstrzygniętego wyboru produktowego dotyczącego sposobu jego użycia. Korekty zastępują wcześniejszą decyzję przy zachowaniu pozostałych odpowiedzi.

   Przed przedstawieniem podejścia sprawdź, czy każda nierozstrzygnięta decyzja ma odpowiedź lub jawną delegację. Uzgodniony budżet pytań obejmuje rundy doprecyzowań; nie wypełniaj go nowymi tematami, gdy wcześniejsze odpowiedzi nadal wymagają wyjaśnienia. Jeśli budżet wyczerpie się przy otwartej decyzji, określ, co pozostaje, i zapytaj, czy rozszerzyć wywiad, zamiast po cichu wybierać lub twierdzić, że wywiad został ukończony.

   Przenoś dokładne predykaty decyzji do podsumowań, przykładów i rezultatów. Zachowuj granice, wyjątki, jednostki i kierunek przy skracaniu odpowiedzi; węższa lub szersza reguła jest nową decyzją. Sprawdź proponowane sformułowanie na przypadku granicznym. Po korekcie zaktualizuj zależne przykłady i brief, a także rejestr decyzji, utrzymując niezwiązane wybory jako rozstrzygnięte.

### Krok 2: Badania i odkrywanie

Po uzyskaniu początkowych wyjaśnień od użytkownika TERAZ zajmij się szczegółami implementacji:

1. **Zbadaj wzorce implementacyjne i wcześniejszą pracę**:
   W tej fazie sam odpowiadaj na pytania implementacyjne — nie proś użytkownika o podejmowanie tych decyzji.

   **Dla zadań programistycznych** zbadaj bazę kodu:
   - Jakich wzorców używa baza kodu dla podobnych funkcji?
   - Jakie jest ustalone podejście do obsługi błędów / logowania / testowania?
   - Które istniejące komponenty lub narzędzia można wykorzystać ponownie?
   - Jakie ograniczenia narzuca bieżąca architektura?

   **Dla zadań nieprogramistycznych** zbadaj pliki kontekstowe i wcześniejszą pracę:
   - Jakie formaty, struktury lub szablony były wcześniej używane do podobnej pracy?
   - Jakie ograniczenia wynikają z wcześniejszych decyzji, odbiorców lub platformy?
   - Jakie powiązane treści lub procesy już istnieją i z czym to powinno być zgodne?
   - Co działało dobrze (albo nie działało) w poprzednich iteracjach?

   **To NIE jest decyzja dla użytkowników** — ustalasz to, badając istniejące wzorce, pliki i kontekst.

2. **Jeśli użytkownik poprawi jakiekolwiek nieporozumienie**:
   - Przyjmij zmienione preferencje jako najnowszą decyzję użytkownika; nie żądaj dowodu źródłowego dla preferencji.
   - Zweryfikuj poprawione twierdzenia faktyczne w nazwanym źródle; deleguj tylko wtedy, gdy niezależne dochodzenie jest użyteczne
   - Przeczytaj dotknięte pliki lub sekcje, zachowując niezwiązane zweryfikowane ustalenia
   - Kontynuuj dopiero po samodzielnym zweryfikowaniu faktów

3. **Zaktualizuj nierozstrzygnięte pytania i właścicieli**:
   Wykorzystaj ponownie listę roboczą z Kroku 1. Śledź pracę obejmującą wiele obszarów za pomocą natywnych możliwości zadań, jeśli są dostępne; pojedyncze wyszukanie nie wymaga dodatkowej ceremonii śledzenia.

4. **Badaj tylko pozostałe luki**:
   Zastosuj referencję orkiestracji do delegowania o zawężonym zakresie, dostępnych wyborów modeli, błędów uruchomienia i nieaktualnych wyników. Niezależne zadania mogą działać równolegle; zależne zadania czekają na warunek wstępny. Użyj istniejącego workera ponownie do skoncentrowanego działania następczego.

5. **Zamknij odkrywanie na granicy dowodów**:
   Syntetyzuj wyniki w miarę ich napływania; weryfikuj istotne twierdzenia i rozstrzygaj materialne konflikty. Zatrzymaj się, gdy potrafisz wyjaśnić dotknięte kontrakty, wzorce do ponownego użycia, weryfikację i pozostałe decyzje użytkownika. Anuluj nieistotną eksplorację. Niedokończone wymagane kontrole pozostają blokujące; nie zastępuj dowodów budżetem czasowym.

6. **Przedstaw ustalenia i opcje projektu**:

   Najpierw przedstaw krótkie podsumowanie ustaleń badawczych:

   ```
   Na podstawie moich badań znalazłem następujące informacje:

   **Stan bieżący:**
   - [Kluczowe odkrycie dotyczące istniejącego kodu]
   - [Wzorzec lub konwencja, której należy przestrzegać]
   ```

   Sprawdź podejścia względem ustalonych wymagań przed ich zaproponowaniem. Odrzuć opcje, które rezygnują z wymaganych gwarancji; nie wymyślaj alternatyw, aby wypełnić limit. Rozróżniaj decyzje użytkownika od brakujących dowodów i szczegółów implementacyjnych, które agent może wyprowadzić.

   Następnie, jeśli istnieje wiele poprawnych podejść, przedstaw je jako ustrukturyzowane wybory:

   Zapytaj użytkownika:
   - question: "Którego podejścia implementacyjnego powinniśmy użyć?"
     header: "Approach"
     options:
     - label: "⭐ Recommended: [Option A] (Recommended)"
       description: "[Co robi A]. · Strength: [Zaleta poparta dowodami]. · Tradeoff: [Konkretny koszt lub ograniczenie]."
     - label: "[Option B name]"
       description: "[Co robi B]. · Strength: [Zaleta poparta dowodami]. · Tradeoff: [Konkretny koszt lub ograniczenie]."

   Jeśli istnieje wyraźnie jedno najlepsze podejście, pomiń pytanie użytkownika i wyjaśnij, dlaczego je wybrałeś.
   Pytaj tylko wtedy, gdy wybór naprawdę ma znaczenie i nie możesz ustalić odpowiedzi na podstawie wzorców bazy kodu.

### Krok 3: Opracowanie struktury planu

Po uzgodnieniu podejścia:

1. **Przedstaw zarys planu i uzyskaj ustrukturyzowaną informację zwrotną**:

   Najpierw wypisz proponowane fazy jako tekst (informacyjnie):

   ```
   Oto moja proponowana struktura planu:

   ## Przegląd
   [Podsumowanie w 1-2 zdaniach]

   ## Fazy wdrożenia:
   1. [Nazwa fazy] - [co osiąga]
   2. [Nazwa fazy] - [co osiąga]
   3. [Nazwa fazy] - [co osiąga]
   ```

   Następnie zapytaj użytkownika:
   - question: "Czy ten podział na fazy wygląda właściwie?"
     header: "Phases"
     options:
     - label: "⭐ Recommended: Zatwierdź fazy (Recommended)"
       description: "Napisz szczegółowy plan z tymi fazami. · Strength: Wykorzystuje zweryfikowaną strukturę. · Tradeoff: Późniejsze zmiany zakresu wymagają jej ponownego przejrzenia."
     - label: "Wymaga korekty"
       description: "Zmień zakres lub kolejność faz. · Strength: Uwzględnia teraz brakujące ograniczenia. · Tradeoff: Dodaje rundę planowania."
     - label: "Zbyt szczegółowe"
       description: "Połącz fazy w większe jednostki. · Strength: Zmniejsza narzut koordynacyjny. · Tradeoff: Każdy krok weryfikacji obejmuje więcej pracy."
       multiSelect: false

### Krok 4: Pisanie szczegółowego planu

Po zatwierdzeniu struktury:

Jeśli zapisy w repozytorium są zabronione, przygotuj pełny plan z użyciem [references/plan-templates.md](references/plan-templates.md) oraz briefu z Kroku 4.5 **w rozmowie**, a następnie wykonaj przekazanie do utrwalenia z `references/plan-persistence.md`. Nie twórz folderów, nie aktualizuj metadanych, nie zapisuj wersji roboczej w innym miejscu ani nie deleguj zapisów, aby ominąć ograniczenie hosta. To jest `awaiting_persistence`, a nie zapisany lub ukończony plan. Host z możliwością zapisu postępuje bezpośrednio normalną ścieżką poniżej.

1. **Rozwiąż folder zmiany, a następnie zapisz plan** do `context/changes/<change-id>/plan.md`.
   - Jeśli użytkownik wywołał `/10x-plan <change-id>` i `context/changes/<change-id>/` już istnieje, użyj go.
   - W przeciwnym razie wyprowadź identyfikator `<change-id>` w formacie kebab-case z tematu i utwórz folder + `change.md` (odzwierciedlając semantykę `/10x-new`) przed zapisem.
   - Odmów, jeśli rozwiązana ścieżka zaczyna się od `context/archive/` — wypisz: "Ta zmiana jest zarchiwizowana. Zamiast tego otwórz nową zmianę za pomocą `/10x-new`." i ZATRZYMAJ się.
   - Po zapisaniu i zweryfikowaniu zarówno planu, jak i briefu, przeprowadź wyłącznie `new`/`preparing` do `planned` i ustaw `updated: <today>`; zachowaj tożsamość, niezwiązane metadane i późniejsze stany cyklu życia. Użyj ograniczonego pomocnika metadanych/mechanizmu awaryjnego z [plan-persistence.md](references/plan-persistence.md#reuse-the-metadata-helper).
   - **Zsynchronizuj roadmapę** (best effort): jeśli `context/foundation/roadmap.md` zawiera element, którego `Change ID` równa się `<change-id>`, ustaw status tego elementu na `Status: planning`. Zobacz „## Roadmap status sync” poniżej. Nigdy nie blokuje; większość zmian nie będzie powiązana z roadmapą.
2. **Przeczytaj teraz [references/plan-templates.md](references/plan-templates.md) i użyj jego struktury pełnego planu.** Bloki faz zawierają zwykłe punktory — `- `, a nie `- [ ]` — a pojedyncza kanoniczna sekcja `## Progress` na dole posiada stan checkboxów; zobacz `references/progress-format.md`. Nie ładuj szablonów artefaktów podczas odkrywania tylko po to, aby przygotować się do późniejszego pisania.

Sekcja Progress jest mechaniczna — wygeneruj jedno `### Phase N: <name>` na fazę, z podsekcjami `#### Automated` / `#### Manual`, które wyliczają każdy punkt Kryteriów Sukcesu z tej fazy jako `- [ ] <phase>.<index> <title>`. Pomiń puste podsekcje. Same bloki faz zawierają zwykłe punktory `- ` (bez checkboxów); sekcja `## Progress` jest jedynym miejscem, w którym występują `[ ]` / `[x]`.

### Krok 4.5: Brief planu (dwie strony)

Po zapisaniu pełnego planu wygeneruj zwięzły brief, który daje czytelnikowi ogólny obraz, zanim zagłębi się w szczegółowy plan. Brief jest pierwszą rzeczą, którą użytkownik czyta — jego przeczytanie powinno zająć mniej niż 2 minuty i pozostawić jasny model mentalny tego, co robi plan, dlaczego oraz jakie były kluczowe decyzje.

1. **Zapisz brief** do `context/changes/<change-id>/plan-brief.md` (obok `plan.md` w tym samym folderze zmiany).

2. **Użyj szablonu briefu z [references/plan-templates.md](references/plan-templates.md)**:

3. **Kluczowe zasady briefu**:
   - Musi zmieścić się na około 2 wydrukowanych stronach (~60-80 linii markdown). Jeśli będzie dłuższy, skróć.
   - Tabela „Key Decisions” jest sednem — pokazuje, co zostało ustalone podczas pytań, aby każda osoba czytająca plan później rozumiała wybory bez ponownego czytania wszystkich pytań.
   - „Starting Point” osadza czytelnika w tym, co istnieje dzisiaj — bez niego osoba nieznająca projektu nie zrozumie różnicy.
   - „Prerequisites & Estimated effort” na dole tabeli Phases daje czytelnikowi szybką ocenę wykonalności przed podjęciem lektury pełnego planu.
   - Pisz dla osoby, która nie była częścią rozmowy planistycznej — powinna zrozumieć kształt i uzasadnienie planu na podstawie samego briefu.
   - Umieść link do pełnego planu u góry, aby czytelnik mógł zagłębić się w dowolną sekcję.
   - Wyprowadź brief z tego samego najnowszego rejestru decyzji co plan. Podczas istniejącego przejścia przeglądowego porównaj twierdzenia, liczby, przykłady i wykluczenia w obu dokumentach; popraw sprzeczności przed utrwaleniem. Dla każdego zmienionego parametru wygeneruj ponownie dotknięte dosłowne przykłady i oczekiwane wyniki z tego parametru; nie zachowuj poprzedniego oczekiwanego ciągu, aktualizując jedynie tabelę decyzji. Sprawdź wymagane sekcje, w tym References, względem już załadowanego szablonu. Strukturalnie poprawna tabela nie dowodzi zgodności z otaczającym ją tekstem.

### Krok 5: Synchronizacja i przegląd

Przy `$10x-plan <change-id> save` (oraz ścieżce utrwalania w [plan-persistence.md](references/plan-persistence.md)) **pomiń tę sekcję**. Ta ścieżka już zarządza jednym wstępnym `inspect`, jednym zapisem, jednym przejściem weryfikacyjnym i jednym `mark-planned`. Nie dodawaj drugiego odczytu zwrotnego, drugiego uruchomienia pomocnika ani własnoręcznie stworzonego walidatora Markdown.

1. **Potwierdź, że plan + brief trafiły do folderu zmiany** (wyłącznie planowanie w sesji z możliwością zapisu; nie ścieżka zapisu):
   - Zweryfikuj, że istnieją zarówno `context/changes/<change-id>/plan.md`, jak i `context/changes/<change-id>/plan-brief.md`.
   - Jednorazowo odczytaj z powrotem utrwaloną treść. Zweryfikuj, że najnowsze decyzje i poprawki są zgodne w dokumentach, wymagane sekcje planu/briefu są obecne oraz że Kryteria Sukcesu każdej fazy mapują się jeden do jednego na kanoniczny wiersz Progress. Tylko nowe wiersze planowania muszą być odznaczone; powtarzane zapisy zachowują istniejący Progress wykonania i późniejszy stan cyklu życia w ramach ścieżki utrwalania. Zachowaj niezwiązane metadane. Kontrole mechaniczne mogą czytać pełne pliki, jednocześnie zwracając tylko liczby, hashe i możliwe do wykonania błędy; sprawdzaj treść semantyczną względem uzgodnionego planu. Nie powtarzaj wielokrotnie wszystkich trzech dokumentów. Zgłoś ukończenie dopiero po tych kontrolach; samo istnienie pliku jest niewystarczające.

2. **Skopiuj polecenie szybkiego startu do schowka**:
   - Po zapisaniu planu skopiuj polecenie wdrożenia do schowka:

   ```bash
   echo -n "/10x-implement <change-id> phase 1" | pbcopy 2>/dev/null || echo -n "/10x-implement <change-id> phase 1" | clip.exe 2>/dev/null || echo -n "/10x-implement <change-id> phase 1" | xclip -selection clipboard 2>/dev/null || true
   ```

   ```powershell
   # PowerShell (Windows)
   Set-Clipboard "/10x-implement <change-id> phase 1"
   ```

3. **Przedstaw zarówno brief, jak i pełny plan**:

   ```
   Utworzyłem plan wdrożenia:

   📋 Brief (zacznij tutaj): `context/changes/<change-id>/plan-brief.md`
   📄 Pełny plan: `context/changes/<change-id>/plan.md`

   → /10x-implement <change-id> phase 1 (✓ skopiowano)

   Najpierw przejrzyj brief, a następnie sprawdź pełny plan pod kątem wymagających korekty elementów:
   - Czy fazy mają odpowiedni zakres?
   - Czy kryteria sukcesu są wystarczająco konkretne?
   - Czy jakieś szczegóły techniczne wymagają korekty?
   - Czy brakuje przypadków brzegowych lub rozważań?
   ```

4. **Iteruj na podstawie informacji zwrotnej** - bądź gotowy, aby:
   - Dodać brakujące fazy
   - Skorygować podejście techniczne
   - Doprecyzować kryteria sukcesu (zarówno automatyczne, jak i ręczne)
   - Dodać/usunąć elementy zakresu

5. **Kontynuuj dopracowywanie**, aż użytkownik będzie zadowolony

## Synchronizacja statusu roadmapy

`context/foundation/roadmap.md` (tworzony przez `/10x-roadmap`) indeksuje każdy Foundation/Slice według stabilnego **Change ID**. Gdy planowanie przekształca element roadmapy w konkretny folder zmiany + plan, oznacz ten element jako **`planning`**, aby roadmapa odzwierciedlała, że element opuścił backlog i wszedł do aktywnej pracy. `/10x-implement` później przesuwa ten sam element do `in-progress`, a `/10x-archive` zamyka go jako `done`.

Wykonaj to w Kroku 4 (tuż po oznaczeniu `change.md` jako → `planned`). Wyszukiwanie jest **obowiązkowe**; „best effort” dotyczy tylko *edycji* — brak roadmapy lub nieznalezienie celu jest pomijane po cichu i nigdy nie blokuje, nie pyta ani nie przerywa wykonania. Nie pomijaj sprawdzenia, zakładając, że nie ma roadmapy.

1. Sprawdź, czy istnieje `context/foundation/roadmap.md`. Jeśli nie istnieje, pomiń ten krok po cichu.
2. Przeczytaj plik. Szukaj `<change-id>` użytego jako `Change ID`:
   - w tabeli `## At a glance` — wiersz, którego komórka kolumny **Change ID** jest dokładnie równa `<change-id>`;
   - oraz w treściach `## Foundations` / `## Slices` — blok `### <ID>: …`, który zawiera linię `- **Change ID:** <change-id>`.

   Dopasowanie odbywa się wyłącznie jako dokładny ciąg. **Brak dopasowania** → wypisz `ℹ context/foundation/roadmap.md has no item with Change ID "<change-id>" — roadmap left untouched.` i zakończ tutaj.
3. **Znaleziono dopasowanie** → jeśli `- **Status:**` elementu ma już wartość `planning`, `in-progress` lub `done`, pozostaw go bez zmian (**tylko do przodu**: nigdy nie cofaj bardziej zaawansowanego statusu) i zakończ. W przeciwnym razie zastosuj obie edycje — każda niezależna i best effort; pomiń podedycję, której cel nie znajduje się tam, gdzie umieszcza go szablon `/10x-roadmap`, i odnotuj pominięcie. Zmieniaj tylko pole `Status`:
   1. **`## At a glance`** — ustaw komórkę **Status** dopasowanego wiersza na `planning`.
   2. **Treść elementu** — przepisz linię `- **Status:**` elementu na `- **Status:** planning`.

   Następnie zaktualizuj frontmatter roadmapy `updated:` do `<today>` (pomiń, jeśli nie ma frontmatter).
4. `/10x-plan` nie commituję własnych artefaktów; pozostaw zmianę w drzewie roboczym. Zostanie zatwierdzona później wraz z pierwszą fazą `/10x-implement` zmiany (która ponownie zmienia ten sam element na `in-progress`).

## Ważne wytyczne

1. **Bądź sceptyczny**:
   - Podważaj niejasne wymagania
   - Wcześnie identyfikuj potencjalne problemy
   - Pytaj „dlaczego” i „co z”
   - Nie zakładaj - weryfikuj za pomocą kodu, plików lub kontekstu

2. **Bądź interaktywny**:
   - Nie pisz pełnego planu za jednym razem
   - Uzyskuj akceptację na każdym głównym etapie
   - Pozwalaj na korekty kursu
   - Pracuj wspólnie

3. **Bądź dokładny**:
   - Przeczytaj WSZYSTKIE pliki kontekstowe W CAŁOŚCI przed planowaniem
   - Badaj nierozstrzygnięte wzorce lokalnie lub przez zawężone niezależne zadania, używając referencji orkiestracji
   - Uwzględniaj konkretne odwołania (`file:line` dla kodu, ścieżki dokumentów dla treści)
   - Pisz mierzalne kryteria sukcesu z jasnym rozróżnieniem automatyczne kontra ręczne

4. **Bądź praktyczny**:
   - Skupiaj się na przyrostowych, testowalnych zmianach
   - Uwzględniaj migrację i wycofanie
   - Myśl o przypadkach brzegowych
   - Uwzględniaj „czego NIE robimy”

5. **Śledź postęp**:
   - Wykorzystuj ponownie listę nierozstrzygniętych pytań; natywne możliwości zadań są opcjonalne i zależne od hosta
   - Dokładnie oznaczaj pracę ukończoną, zablokowaną i anulowaną; unikaj prowadzenia ewidencji dla pojedynczego wyszukania

6. **OBOWIĄZKOWE: Dogłębne pytania skalowane złożonością**:
   - **PRZED** zapisaniem jakiegokolwiek planu MUSISZ ocenić złożoność (HIGH/MEDIUM/LOW) i uzyskać potwierdzenie użytkownika
   - Użyj skalowania wcześniejszych etapów z Kroku 1.0, jego wyjątku dotyczącego rozstrzygniętych danych wejściowych oraz potwierdzonego przez użytkownika łącznego budżetu pytań; zakresy dla wyłącznie zadania nie zastępują zaakceptowanych decyzji z wcześniejszych etapów
   - Każde pytanie musi umieszczać jeden wybór `⭐ Recommended: [short choice] (Recommended)` jako pierwszy; każda opcja wymaga dokładnego formatu opisu Strength/Tradeoff i nagłówka o długości maksymalnie 12 znaków
   - Uwzględniaj zakres, przypadki brzegowe, architekturę, model danych, testowanie i wydajność stosownie do złożoności
   - Pytaj w rundach obsługiwanych natywnie w ramach potwierdzonego łącznego budżetu; rozszerzaj go jawnie wyłącznie dla nierozstrzygniętych decyzji
   - Zachowuj potwierdzony wywiad i akceptację natywnej struktury. Dostosuj budżet z użytkownikiem, gdy zmienia się zakres; nigdy nie powtarzaj ustalonych pytań jedynie po to, aby wypełnić bazowy zakres
   - Czekaj na odpowiedzi użytkownika przed przejściem do szczegółowego planowania

7. **Brak otwartych pytań w końcowym planie**:
   - Jeśli napotkasz otwarte pytania podczas planowania, ZATRZYMAJ się
   - Natychmiast zbadaj temat lub poproś o wyjaśnienie
   - NIE zapisuj planu z nierozstrzygniętymi pytaniami
   - Plan wdrożenia musi być kompletny i wykonalny
   - Każda decyzja musi być podjęta przed finalizacją planu
   - Termin, o którym użytkownik zdecydował, trafia do już istniejących sekcji — nazwany test lub kryterium sukcesu, gdy zaakceptowany, „What We're NOT Doing”, gdy odrzucony. Nie twórz dla niego nowej sekcji
   - Podsekcje „Critical Implementation Details” są opcjonalne: uwzględniaj je tylko wtedy, gdy występuje rzeczywiste ograniczenie, pułapka lub wymóg kolejności. Domyślnie pomijaj. Plan bez tej sekcji nie jest niekompletny.

8. **Opisuj intencję, a nie implementację**:
   - Plan mówi wdrażającemu **co zmienić i dlaczego**, a nie jak napisać kod
   - Każdy wpis zmiany pod `### Changes Required:` oddziela `**Intent**` (co i dlaczego) od `**Contract**` (interfejs, sygnaturę, pole schematu, trasę, strukturę lub niezmiennik, którego dotyczy zmiana). Fragmenty kodu, gdy są potrzebne, znajdują się na końcu `**Contract**`
   - Domyślnie nie używaj fragmentów kodu. Dodaj fragment TYLKO wtedy, gdy zmiana nie jest oczywista (trudne regex, nietypowe wywołanie API, nieintuicyjna kolejność, obejście, kontrakt sygnatury, od którego zależą inne fazy)
   - W przypadku rutynowych zmian — dodania pola, podłączenia handlera, zastosowania istniejącego wzorca — opisz `**Intent**` w 1-2 zdaniach, nazwij `**Contract**` w jednym i zakończ. Wdrażający (człowiek lub agent) ustala kod na podstawie ścieżki pliku, otaczającego wzorca i intencji
   - Ścieżki plików oraz krótkie opisy Intent/Contract zwykle wystarczą. Oprzyj się pokusie wcześniejszego napisania kodu

## Wytyczne dotyczące kryteriów sukcesu

**Zawsze rozdzielaj kryteria sukcesu na dwie kategorie:**

1. **Automated Verification** — polecenia, które agenci mogą uruchomić: `make test`, `npm run lint`, sprawdzenia typów, istnienie określonych plików
2. **Manual Verification** — testowanie przez człowieka: UI/UX, rzeczywista wydajność, przypadki brzegowe, akceptacja użytkownika

Kryteria sukcesu każdej fazy używają zwykłych punktorów `- ` pod nagłówkami `#### Automated Verification:` i `#### Manual Verification:`. Skopiuj każde kryterium dokładnie raz do kanonicznej sekcji `## Progress` jako nieoznaczony numerowany wiersz; Progress jest jedynym miejscem dla checkboxów wykonania.

## Typowe wzorce

- **Zmiany bazy danych**: schemat/migracja → metody magazynu → logika biznesowa → API → klienci
- **Nowe funkcje**: badanie wzorców → model danych → backend → API → UI
- **Refaktoryzacja**: dokumentowanie zachowania → zmiany przyrostowe → kompatybilność wsteczna → migracja

## Koordynacja zadań

[Referencja orkiestracji](references/task-orchestration.md) zarządza wyborem zadań, delegowaniem, awaryjnym mechanizmem możliwości modelu, przeglądem dowodów i zatrzymywaniem. Nie zastępuje natywnych pytań, akceptacji struktury ani ścieżki zapisu zależnej od trybu. Nie wywołuj rekomendatora modelu implementacyjnego jedynie po to, aby zakończyć badania lub zapisać uzgodniony plan.

- **Uruchamiaj wielu podagentów równolegle** w pojedynczej wiadomości — 2–3 dla fazy badawczej — zamiast jednego po drugim.
- **Utrzymuj każde zadanie w skupieniu** na konkretnym obszarze, z szczegółowymi instrukcjami (katalogi, co wyodrębnić, oczekiwany format).
- **Wymagaj konkretnych odwołań `file:line`** w odpowiedziach.
- **Czekaj na zakończenie wszystkich delegowanych podagentów** przed syntezą ustaleń.
- **Weryfikuj wyniki podagentów** — jeśli ustalenie jest nieoczekiwane, deleguj działanie następcze i porównaj je z rzeczywistym kodem.

## Zarządzanie kontekstem

Planowanie może intensywnie wykorzystywać kontekst ze względu na badania + iteracje. Utrzymuj efektywność kontekstu:

- **Deleguj badania do podagentów** — zwracają podsumowania, utrzymując główny kontekst w lekkiej formie. Nie czytaj ponownie plików, które podagenci już przeanalizowali, chyba że musisz zweryfikować konkretne szczegóły.
- **Syntetyzuj, nie gromadź** — po zwróceniu wyników przez podagentów syntetyzuj ustalenia we własnym zrozumieniu, zamiast cytować duże bloki dosłownie.
- **Jeśli kontekst wydaje się zdegradowany podczas planowania** — jeśli odpowiedzi stają się powolne lub powtarzalne, zapisz bieżącą wersję roboczą planu do pliku i zaproponuj użytkownikowi kontynuację w świeżym kontekście:
  Rób to tylko wtedy, gdy host zezwala na zapisy w repozytorium. W trybie planowania tylko do odczytu użyj przekazania w rozmowie z `references/plan-persistence.md`; nie twierdź, że wersja robocza została zapisana, ani nie obiecuj, że nowa rozmowa odzyska niedostępne decyzje.
  ```
  Wersja robocza planu została zapisana w: context/changes/<change-id>/plan.md
  Czy chcesz kontynuować dopracowywanie w nowym oknie?
  → /10x-plan <change-id> (✓ skopiowano)
  ```
  Umożliwia to `/10x-plan` ponowne załadowanie wersji roboczej i kontynuowanie iteracji z pełnym dostępnym kontekstem.

## Dodatkowe przykłady pytań

Jeśli pytanie specyficzne dla funkcji jest trudne do sformułowania, zapoznaj się z [references/question-examples.md](references/question-examples.md). Format pytań i skalowanie wcześniejszych etapów powyżej pozostają wiążące; przykłady nie ustanawiają zmierzonych korzyści wydajnościowych.