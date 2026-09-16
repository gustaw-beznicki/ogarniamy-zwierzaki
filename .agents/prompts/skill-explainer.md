# Objaśnienie umiejętności

Przeanalizuj umiejętność, aby zrozumieć jej mechanikę, uzasadnienie projektu oraz sposób zbudowania czegoś podobnego. Po wywołaniu przeczytaj pliki źródłowe docelowej umiejętności i utwórz ustrukturyzowany raport, który wyjaśnia, jak działa umiejętność i dlaczego została zbudowana w ten sposób.

## Dane wejściowe

Użytkownik podaje nazwę umiejętności (np. `10x-plan`, `10x-shape`, `10x-new`). Akceptuj ją jako:
- Zwykłą nazwę: `10x-plan`
- Nazwę z prefiksem ukośnika: `/10x-plan`
- Ścieżkę do pliku SKILL.md: `~/the AI tool's configuration directory/skills/10x-plan/SKILL.md`

Jeśli nie podano nazwy umiejętności, zapytaj użytkownika:

```
Which skill would you like me to explain? Provide a skill name (e.g., `10x-plan`) or a path to its SKILL.md file.
```

Następnie poczekaj.

## Odkrywanie

Znajdź pliki źródłowe umiejętności:

1. **Zlokalizuj plik SKILL.md.** Wypróbuj poniższe ścieżki w kolejności i zatrzymaj się przy pierwszym trafieniu:
   - `~/the AI tool's configuration directory/skills/<name>/SKILL.md`
   - `the AI tool's configuration directory/skills/<name>/SKILL.md` (lokalnie w projekcie)
   - `.agents/skills/<name>/SKILL.md` (Codex)
   - `.cursor/skills/<name>/SKILL.md` (Cursor)
   - Ścieżka podana przez użytkownika (jeśli podano pełną ścieżkę)

   Jeśli nie znajdziesz żadnego, powiedz użytkownikowi:
   ```
   I couldn't find the SKILL.md for "<name>". Please provide the full path to the skill file.
   ```
   Następnie poczekaj.

2. **Przeczytaj cały plik SKILL.md** — bez skracania, bez limitu/offsetu.

3. **Sprawdź, czy obok pliku SKILL.md istnieje katalog `references/`.** Jeśli istnieje, wyświetl jego zawartość i przeczytaj w całości każdy znajdujący się w nim plik `.md`. Są to dokumenty towarzyszące (schematy, szablony, rejestry), które definiują kontrakty egzekwowane przez umiejętność.

## Analiza

Po przeczytaniu wszystkich plików źródłowych utwórz poniższy raport. Dostosuj poziom szczegółowości do złożoności umiejętności:

| Rozmiar umiejętności | Głębokość |
|-----------|-------|
| Mniej niż 150 wierszy (prosta) | Zwięźle — każda sekcja ma 3–5 zdań. Pomiń sekcje, które nie mają zastosowania (np. proste umiejętności rzadko mają orkiestrację podagentów lub bramki samokontroli). |
| 150–400 wierszy (średnia) | Standardowo — każda sekcja to krótki akapit. Omów wszystkie 7 sekcji. |
| Ponad 400 wierszy (złożona/orkiestrator) | Szczegółowo — tabela anatomii, konkretne odwołania do wierszy, rozszerzona analiza mechaniki. Wszystkie 7 sekcji w pełni. |

Nie wypełniaj raportów o prostych umiejętnościach ogólnikowymi zapychaczami. Umiejętność licząca 95 wierszy powinna otrzymać zwarty, skoncentrowany raport. Orkiestrator liczący 831 wierszy wymaga dogłębnego omówienia.

## Struktura raportu

Przed szczegółowymi sekcjami rozpocznij od krótkiego bloku przeglądowego, który orientuje czytelnika. Wyświetl go dokładnie raz, na początku raportu:

```
## Sections in this report

1. **Problem & Purpose** — Why this skill exists and what pain it removes
2. **Chain Position** — Where it sits in the workflow: what feeds in, what comes after
3. **Anatomy Walkthrough** — Section-by-section map of the SKILL.md file
4. **Key Mechanics** — The behavioral drivers that make this skill tick, with high-leverage parts flagged
5. **Design Decisions** — Why it's built this way and not another — the rejected alternatives
6. **Adaptation Guide** — What you can tweak (easy / medium / hard) with concrete examples
7. **Building Something Similar** — Step-by-step path from blank file to a working skill like this one
```

Następnie przejdź do każdej sekcji w całości:

### 1. Problem i cel

Odpowiedz na pytanie: **„Dlaczego ta umiejętność istnieje?”**

Wyodrębnij z deklaracji roli oraz sekcji „Kiedy używać / kiedy pominąć”:
- Jaki problem rozwiązuje ta umiejętność? Co działo się, zanim powstała?
- Kiedy użytkownik powinien po nią sięgnąć? Jakie są sygnały wyzwalające?
- Kiedy użytkownik NIE powinien jej używać? Jaki kontekst jest niewłaściwy?
- Co by się stało, gdyby użytkownik próbował wykonać to zadanie ręcznie, bez umiejętności?

Nie opisuj jedynie tego, co robi umiejętność — wyjaśnij, jaki problem eliminuje.

### 2. Pozycja w łańcuchu

Odpowiedz na pytanie: **„Gdzie ta umiejętność znajduje się w przepływie pracy?”**

Wyodrębnij z sekcji „Relacja z innymi umiejętnościami”:
- **Upstream**: Jakich plików lub artefaktów ta umiejętność oczekuje jako danych wejściowych? Która umiejętność je tworzy? (np. `/10x-shape` tworzy `shape-notes.md`, który wykorzystuje `/10x-prd`)
- **Downstream**: Co ta umiejętność zwraca? Która umiejętność wykorzystuje to następnie? Do jakiego pliku zapisuje na dysku?
- **Model przekazania**: Umiejętności komunikują się za pośrednictwem plików na dysku, a nie pamięci. Każda umiejętność zapisuje artefakt, zatrzymuje się i przekazuje kontrolę człowiekowi, zanim uruchomiona zostanie kolejna umiejętność. Wyjaśnij, jak ta umiejętność wpisuje się w ten łańcuch.

Gdy jest to przydatne, pokaż pozycję w łańcuchu wizualnie:
```
[upstream skill] → input artifact → THIS SKILL → output artifact → [downstream skill]
```

### 3. Omówienie anatomii

Odpowiedz na pytanie: **„Jakie są sekcje tego pliku SKILL.md i co robi każda z nich?”**

Podziel plik SKILL.md na sekcje i dla każdej opisz:
- **Nazwę sekcji** i przybliżony zakres wierszy
- **Co robi** — jedno zdanie
- **Dlaczego istnieje** — co zepsułoby się lub pogorszyło po usunięciu tej sekcji

Dla średnich i złożonych umiejętności przedstaw to w tabeli:

| Sekcja | Wiersze | Cel | Dlaczego ma znaczenie |
|---------|-------|---------|----------------|
| YAML frontmatter | 1-8 | Nazwa, opis, uprawnienia narzędzi | `description` kontroluje moment aktywacji umiejętności; uprawnienia narzędzi stanowią twardą granicę bezpieczeństwa |
| Deklaracja roli | 10-15 | Jednozdaniowa filozofia | Ustala osobowość zachowania umiejętności |
| ... | ... | ... | ... |

Cel: wyjaśnić „tysiące wierszy”. Pokaż uczącemu się, że długa umiejętność to w rzeczywistości N sekcji, z których każda ma jasne zadanie. Całość jest mniej onieśmielająca niż poszczególne części.

### 4. Kluczowa mechanika

Odpowiedz na pytanie: **„Jakie 3–5 mechanizmów zachowania napędza TĘ konkretną umiejętność i które części mają największą dźwignię?”**

Ta sekcja musi być specyficzna dla analizowanej umiejętności — nie może być ogólną listą wzorców umiejętności. Przeczytaj kroki procesu i określ, co napędza zachowanie TEJ umiejętności. Dla każdego mechanizmu:

1. **Nadaj nazwę** — krótka, opisowa nazwa wzorca
2. **Wyjaśnij, jak działa** — 2–3 zdania o mechanizmie
3. **Wskaż miejsce** — które wiersze lub sekcje pliku SKILL.md go implementują
4. **Oznacz dźwignię** — zaznacz fragmenty, w których mała zmiana powoduje dużą zmianę zachowania. Typowe wzorce o wysokiej dźwigni obejmują:
   - Pole `description` (kontroluje aktywację), uprawnienia narzędzi (granica bezpieczeństwa)
   - Krytyczne zabezpieczenia (twarde reguły zachowania)
   - Szablony/schematy (kształt wyjścia, od którego mogą zależeć kolejne umiejętności)
   - Bramki samokontroli (wbudowane testy przed zatwierdzeniem wyjścia)

Przykłady mechanizmów występujących w rzeczywistych umiejętnościach (używaj jako odniesienia, nie listy kontrolnej):

- **Pytania skalowane złożonością** (`10x-plan`): ocenia zadanie jako LOW/MEDIUM/HIGH, skaluje liczbę pytań, pomija pytania diagnostyczne, gdy istnieją artefakty upstream
- **Orkiestracja podagentów** (`10x-research`): uruchamia równoległych agentów, każdego z ukierunkowanym promptem, syntetyzuje ustalenia
- **Maszyna stanów sterowana postępem** (`10x-implement`): pola wyboru `## Progress` są jedynym źródłem prawdy, bez pobocznego pliku stanu
- **Sokratyczna pętla odkrywania** (`10x-shape`): otwarte pytanie → ujawnienie szarych obszarów → rekomendacja → zakwestionowanie → zablokowanie decyzji
- **Mechanizmy antybiasowe** (`10x-infra-research`): adwokat diabła, pre-mortem, wzajemne kontrole nieznanych niewiadomych

### 5. Decyzje projektowe

Odpowiedz na pytanie: **„Dlaczego ta umiejętność jest zbudowana WŁAŚNIE w ten sposób, a nie inaczej?”**

To sekcja, która odpowiada na pytanie „czemu skill jest tak a nie inaczej budowany.” Dla każdego głównego wyboru strukturalnego w umiejętności wyjaśnij:

1. **Dokonany wybór** — co robi umiejętność
2. **Odrzucona alternatywa** — co mogłaby zrobić zamiast tego
3. **Dlaczego to rozwiązanie wygrywa** — konkretny kompromis, który czyni ten wybór lepszym

Szukaj decyzji w tych obszarach (nie wszystkie będą mieć zastosowanie):

- **Wybór narzędzi**: Dlaczego te uprawnienia narzędzi, a nie inne? (np. dlaczego brak podagentów w umiejętności, która teoretycznie mogłaby ich używać?)
- **Zarządzanie stanem**: Dlaczego stan w pliku zamiast stanu w pamięci lub zewnętrznego pliku pomocniczego?
- **Zachowanie łańcucha**: Dlaczego „STOP, do not chain” zamiast automatycznej kontynuacji? Dlaczego pliki na dysku zamiast przekazywania stanu w pamięci?
- **Strategia walidacji**: Dlaczego walidacja w tym momencie, a nie wcześniej lub później? Dlaczego akurat te konkretne sprawdzenia?
- **Format wyjścia**: Dlaczego taka struktura szablonu? Dlaczego YAML frontmatter zamiast zwykłego markdown? Dlaczego szablony inline zamiast plików referencyjnych?
- **Model interakcji**: Dlaczego zadać użytkownikowi to pytanie na tym etapie? Dlaczego nie podjąć decyzji automatycznie?

Celem jest ujawnienie inżynierskiego myślenia stojącego za umiejętnością. Osoba ucząca się, która rozumie odrzucone alternatywy, rozumie przestrzeń projektową — i może podejmować własne decyzje podczas budowania czegoś podobnego.

### 6. Przewodnik adaptacji

Odpowiedz na pytanie: **„Co mogę zmienić i jak ryzykowna jest każda zmiana?”**

Uporządkuj według poziomów trudności, z 1–2 konkretnymi przykładami na poziom, specyficznymi dla analizowanej umiejętności:

**Łatwe (niskie ryzyko, natychmiastowy efekt):**
- Co zmienić: np. frazy wyzwalające w `description`, nagłówki sekcji szablonu, etykiety opcji pytań, formatowanie raportu
- Przykład: „Aby dodać polskie frazy wyzwalające, edytuj pole `description` i dodaj „stwórz plan” obok „create plan””
- Co się zepsuje, jeśli zrobisz to źle: nic krytycznego — w najgorszym razie umiejętność aktywuje się w niewłaściwych momentach albo formatowanie wyjścia będzie wyglądać inaczej

**Średnie (wymaga zrozumienia łańcucha):**
- Co zmienić: np. kryteria bramki samokontroli, wymiary punktacji, kategorie pytań, liczbę podagentów
- Przykład: „Aby dodać wymiar „Security” do karty wyników przeglądu, dodaj go do listy wymiarów w krokach procesu i zaktualizuj szablon raportu”
- Co się zepsuje, jeśli zrobisz to źle: umiejętność może tworzyć niekompletne lub niespójne wyjście, ale nie zepsuje innych umiejętności w łańcuchu

**Trudne (strukturalne, ryzyko zerwania kontraktów łańcucha):**
- Co zmienić: np. uprawnienia narzędzi, format pliku wyjściowego, nazewnictwo artefaktów, wartości cyklu życia statusu
- Przykład: „Zmiana nazwy pliku wyjściowego z `plan.md` na `implementation-plan.md` zepsułaby `/10x-implement`, który wyszukuje `plan.md`”
- Co się zepsuje, jeśli zrobisz to źle: kolejne umiejętności zależne od dokładnych nazw plików, nagłówków sekcji lub wartości statusów zawiodą po cichu albo utworzą nieprawidłowe wyjście

### 7. Budowanie czegoś podobnego

Odpowiedz na pytanie: **„Gdybym chciał zbudować własną wersję tej umiejętności, od czego powinienem zacząć?”**

Przedstaw praktyczną, krok po kroku ścieżkę tworzenia. Zacznij prosto i stopniowo rozwijaj — to progresywna droga od „pustego pliku” do „działającej umiejętności”.

Przed przejściem do ręcznych kroków zwróć uwagę na dwa skróty:
- **Podejście konwersacyjne**: Po prostu powiedz swojemu asystentowi AI „let's build a skill that does X” i iterujcie wspólnie nad plikiem SKILL.md w 3–4 rundach. To najszybsza ścieżka dla osobistych umiejętności.
- **Meta-umiejętność tworzenia umiejętności**: Meta-umiejętność do budowania umiejętności z ustrukturyzowanymi ewaluacjami. Dostępna pod adresem `github.com/anthropics/skills/tree/main/skills/skill-creator`. Jest lepsza dla współdzielonych umiejętności lub umiejętności zintegrowanych z łańcuchem, dla których potrzebujesz automatycznej weryfikacji.

Oba skróty tworzą ten sam plik SKILL.md — poniższe kroki wyjaśniają, co generują, abyś rozumiał wynik i mógł go dopracować:

**Krok 1: Zacznij od promptu.** Przed utworzeniem pliku umiejętności zapisz główną instrukcję jako zwykły prompt. Przetestuj ją w rozmowie. Czy tworzy w przybliżeniu właściwe wyjście? Iteruj, aż podstawowe zachowanie będzie działać.

**Krok 2: Utwórz plik umiejętności.** Utwórz `<skill-name>/SKILL.md` w swoim katalogu umiejętności. Dodaj minimalny frontmatter:
---
name: <skill-name>
description: <one-line description with trigger phrases>
---

**Krok 3: Dodaj strukturę.** Przekształć prompt w sekcje: deklarację roli, kiedy używać/pominąć, odpowiedź początkową oraz kroki procesu. Deklaracja roli określa osobowość; sekcja kiedy używać zapobiega niewłaściwemu użyciu.

**Krok 4: Dodaj zabezpieczenia.** Czego ta umiejętność NIGDY nie może robić? Zapisz 3–5 krytycznych zabezpieczeń. To wiersze o największej dźwigni — zapobiegają najbardziej szkodliwym trybom awarii.

**Krok 5: Dodaj granice zakresu.** Napisz sekcję „Czego ta umiejętność NIE robi”. Jawne granice zapobiegają rozszerzaniu zakresu i czynią umiejętność przewidywalną.

**Krok 6: (W razie potrzeby) Dodaj referencje.** Jeśli umiejętność egzekwuje schemat, szablon lub rejestr, umieść je w katalogu `references/`. Zachowaj koncentrację pliku SKILL.md na zachowaniu; kontrakty danych umieść w plikach referencyjnych.

**Krok 7: (W razie potrzeby) Dodaj integrację z łańcuchem.** Jeśli ta umiejętność jest częścią łańcucha, zdefiniuj dane wejściowe upstream (jaki plik odczytuje) i wyjście downstream (do jakiego pliku zapisuje). Dodaj sekcję „Relacja z innymi umiejętnościami”. Dodaj „STOP, do not chain” do zabezpieczeń.

**Krok 8: (W razie potrzeby) Dodaj zaawansowane wzorce.** W oparciu o to, co demonstruje ta umiejętność, wymień, które zaawansowane wzorce osoba ucząca się może dodać:
- Orkiestrację podagentów (jeśli umiejętność uruchamia agentów)
- Skalowanie złożoności (jeśli umiejętność dostosowuje się do wielkości danych wejściowych)
- Bramki samokontroli (jeśli umiejętność waliduje własne wyjście)
- Wznawianie oparte na punktach kontrolnych (jeśli umiejętność obsługuje pracę wielosesyjną)
- Pytanie użytkownika o interaktywne decyzje

Dla każdego kroku wskaż, co analizowana umiejętność robi na tym poziomie, aby osoba ucząca się mogła dostrzec zależność między krokami budowy a gotowym produktem.

**Typowe błędy, których należy unikać:**
- Rozpoczynanie od zaawansowanych wzorców, zanim podstawowe zachowanie zacznie działać
- Pisanie zbyt ogólnikowych zabezpieczeń („be careful”) zamiast konkretnych („NEVER auto-chain to the next skill”)
- Pomijanie sekcji „Czego ta umiejętność NIE robi” — rozszerzanie zakresu jest głównym trybem awarii umiejętności
- Tworzenie zbyt szerokiego `description` (aktywuje się przy wszystkim) albo zbyt wąskiego (nigdy się nie aktywuje)

## Przypadki brzegowe

- **Umiejętność nie ma katalogu references/**: pomiń analizę referencji. Nie wspominaj, że referencji brakuje — większość prostych umiejętności ich nie ma i to jest w porządku.
- **Umiejętność jest plikiem promptu, a nie SKILL.md**: jeśli użytkownik wskaże plik `the AI tool's configuration directory/prompts/*.md`, wyjaśnij, że prompty są prostsze niż umiejętności (brak frontmatter, brak uprawnień narzędzi, brak pozycji w łańcuchu) i przeanalizuj to, co się w nim znajduje. Dostosuj raport, aby pominąć sekcje, które nie mają zastosowania.
- **Umiejętność jest bardzo krótka (poniżej 50 wierszy)**: utwórz minimalny raport — Problem i cel + Anatomia + Budowanie czegoś podobnego. Pomiń pozycję w łańcuchu, kluczową mechanikę i przewodnik adaptacji, jeśli nie ma niczego istotnego do powiedzenia.
- **Umiejętność wykorzystuje wzorce niewymienione powyżej**: analizuj to, co widzisz. Lista mechanizmów w sekcji 5 jest przykładowa, a nie wyczerpująca. Jeśli umiejętność ma unikalny wzorzec, wyjaśnij go.

## Ton

Pisz dla programisty, który potrafi UŻYWAĆ umiejętności, ale chce zrozumieć, JAK i DLACZEGO działa. Nie wyjaśniaj, czym jest asystent kodowania AI ani jak działają polecenia z ukośnikiem — czytelnik używa ich już codziennie. Skup się na decyzjach projektowych, elementach nośnych i praktycznej ścieżce do stworzenia własnej umiejętności.