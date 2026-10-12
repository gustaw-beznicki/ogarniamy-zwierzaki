---
name: 10x-ui
description: >
  Audit and improve ONE view that already renders, as a normal 10x change with a
  design-system contract — audit into 3–5 charges (missing tokens, missing shared
  component, accidental architecture), fix the contract before the pixels, cover a
  7-state matrix, gate with a screenshot, and leave a rule so the next agent keeps
  using the tokens and components. The UI entry to /10x-research → /10x-plan →
  /10x-implement, sharing the same change folder and Progress. Use when the user wants
  a theme, a restyle, "make it prettier", shadcn/Tailwind work, design tokens, dark
  mode, a visible-focus pass, or cleanup of UI an agent built feature by feature. Not a
  generator for a view that does not exist yet, not a component catalog, not a
  Playwright course.
---
# 10x-ui — kontrakt systemu projektowego dla jednej zmiany wizualnej

Interfejs UI to zwykła zmiana 10x. Nie otwieraj czatu vibes w wątku CRUD i nie zaczynaj
od promptu mówiącego jedynie „zrób to ładniej”.

**Ta umiejętność iteruje na UI, które już istnieje.** Zakłada widok, który możesz otworzyć i
zrobić jego zrzut ekranu: znaczniki się renderują, dane przepływają, ekran spełnia swoje zadanie,
ale po prostu nie jest wystarczająco dobry. Stworzenie tego pierwszego widoku to zwykła praca nad
funkcją dla Core Skills Chain; ta umiejętność przejmuje pracę w momencie, gdy jest on na ekranie.

Nie zastępuje łańcucha. Otwiera zmianę, przekazuje brief audytu przez
`/10x-research`, kształtuje plan pisany przez `/10x-plan` i dodaje kontrole UI do każdej fazy
`/10x-implement`. Te umiejętności zachowują własne kontrakty — ten sam `plan.md`, ten sam
`## Progress`, ten sam rytuał commitów.

Cel: `$ARGUMENTS`

Jeśli istnieje `context/foundation/lessons.md`, przeczytaj go raz pod kątem powtarzających się
problemów UI w tym repozytorium.

## Kiedy ją uruchamiać — i na którym widoku

- **Kiedy:** po wyrenderowaniu pierwszego pionowego przekroju z prawdziwymi danymi — główny
  przepływ działa od początku do końca i wygląda, jakby był budowany funkcja po funkcji.
  Wcześniej nie ma czego audytować; znacznie później każdy nowy widok kopiuje odchylenia.
  Najlepszy moment to przed drugim lub trzecim widokiem, aby dziedziczyły kontrakt zamiast
  literałów.
- **Który widok:** ten, na który użytkownicy trafiają najczęściej w głównym przepływie (lista/dashboard po logowaniu,
  a nie strona ustawień). Strona docelowa liczy się tylko wtedy, gdy już istnieje i jest
  zmianą, na której ci zależy — dostaje własną zmianę, a nie jedzie przy okazji.
- **Nie teraz:** widok, który jeszcze nie istnieje (zbuduj go przez zwykły łańcuch, a potem wróć),
  rebranding całego MVP, równoległe agenty lub `/goal` dla przepustowości (późniejsza lekcja),
  plik narzędzia projektowego jako jedyne źródło prawdy (ta umiejętność działa na uruchomionej aplikacji).

## Przy wywołaniu

1. **Rozwiąż cel.**
   - `context/changes/<arg>/` istnieje → ta zmiana; przeczytaj `change.md` oraz ewentualne `research.md`
     i `plan.md`, a następnie wznów od pierwszego kroku routera, który nie został jeszcze wykonany.
   - Trasa lub plik widoku → widok do audytu. Zaproponuj change-id i skopiuj
     `/10x-new <change-id>` do schowka; w `change.md` nazwij **jeden** widok oraz źródło tokenów
     (lub motyw), względem którego działa ta zmiana.
   - Brak argumentu → zapytaj, który widok, korzystając z powyższych wskazówek, i zatrzymaj się do czasu odpowiedzi.
   - Odrzuć ścieżki `context/archive/`: „Ta zmiana jest zarchiwizowana. Zamiast tego otwórz nową zmianę przez
     `/10x-new`.”
2. **Wstępny audyt (minuty, nie research):** zlokalizuj źródło wartości, katalog współdzielonych komponentów,
   plik(i) zasad dla agenta (plik konfiguracji AI projektu (AGENTS.md),
   `AGENTS.md`, `.cursor/rules/*`, `.windsurfrules`, `copilot-instructions.md`) i uruchom poniższe skanowanie
   wartości zakodowanych na sztywno na plikach widoku. Zgłoś liczby — wskazują, który wariant kontraktu obowiązuje.
3. **Przekaż do `/10x-research`** brief audytu z sekcji *Audyt* poniżej; skopiuj
   polecenie do schowka. Zarzuty trafiają do `research.md` pod `## Charges`.

> **Schowek.** Przekaż dokładne polecenie potokiem do `pbcopy` / `clip.exe` / `xclip -selection
> clipboard` / `Set-Clipboard`, po cichu użyj fallbacku, jeśli żadne nie istnieje, a następnie wypisz je w osobnej
> linii z sufiksem `(✓ copied)`.

## Router

1. `/10x-new <change-id>` — jeden widok, jedno źródło tokenów lub motyw.
2. `/10x-research <change-id>` — dwukierunkowy audyt poniżej. Wynik: `## Charges` w `research.md`.
3. `/10x-plan <change-id>` — fazy w tej kolejności: **środowisko/biblioteka → wartości tokenów →
   jeden widok → stany**. Każdy zarzut mapuje do fazy lub jest wymieniony jako odroczony. Faza stanów
   zawiera macierz 7 stanów jako kryteria sukcesu.
4. `/10x-implement <change-id>` faza po fazie. Po każdej fazie wizualnej: zrzut ekranu na
   desktopie i przy jednej szerokości mobilnej oraz ponowne uruchomienie skanowania wartości zakodowanych na sztywno na widoku.
5. **Bramka wizualna** — kitchen sink lub `toHaveScreenshot` na tym jednym widoku.
6. **Zostaw zabezpieczenie** — regułę (oraz, jeśli repozytorium ma linter, kontrolę), która utrzymuje następnego
   agenta przy kontrakcie. Zobacz *Utrwal zmianę*.
7. `/10x-impl-review` — ustalenia UI domyślnie nie są „pomijane jako kosmetyczne”. Następnie pętla przeglądu.

## Audyt: trzy kategorie zarzutów

Przed jakimkolwiek CSS przejdź przez widok i zapisz **3–5 zarzutów**. Każdy zarzut otrzymuje **plik i
linię** oraz **jedno zdanie o wpływie na użytkownika**. Lista zarzutów jest wejściem do
planu; „zrób to ładniej” nim nie jest.

Audyt przebiega w **dwóch kierunkach**:

- **Źródło → widoki.** Gdzie żyją wartości, gdzie żyją współdzielone komponenty i które
  widoki faktycznie je odczytują? Policz użycia klas/zmiennych tokenów oraz importy z
  katalogu komponentów na widok. Plik tokenów, którego nic nie odczytuje, jest ustaleniem, a nie bazą.
- **Widok → źródło.** Dla każdego literału w widoku: który token lub komponent powinien był
  go pokryć? To jest dowód zarzutu.

Przeczytaj również plik(i) zasad dla agenta pod kątem instrukcji UI. Reguła, która mówi agentowi, aby używał
jednorazowych wartości (np. „używaj wartości arbitralnych, takich jak `w-[123px]`, dla precyzyjnych projektów”) jest
zarzutem dotyczącym przypadkowej architektury: to dlatego widoki się rozjechały i to cofnie poprawkę.

| Kategoria | Jak wygląda | Dowód do zapisania | Typowa poprawka |
| --- | --- | --- | --- |
| **Brakujące tokeny** | literały kolorów w widoku — hex/rgb/oklch, a w Tailwind **klasy palety** (`bg-blue-900`, `text-purple-200`, `from-indigo-900`) i wartości arbitralne (`p-[13px]`); trzy odcienie tego samego „primary”; odstępy wymyślane osobno dla każdego pliku | plik:linia literału oraz token, który powinien go pokryć | przenieś wartość do źródła tokenów tego repozytorium i odwołaj się do niej przez rolę (`bg-primary`, `text-muted-foreground` w wariancie Tailwind) |
| **Brakujący współdzielony komponent** | drugi `Button` zbudowany z `div`+klas, karta kopiująca kartę DS, kopiowane-wklejane pole formularza | plik:linia duplikatu oraz komponent, który przesłania (lub ten, który należy dodać) | zaimportuj właściwy komponent albo dodaj go przez własną ścieżkę stosu (np. `npx shadcn add <name>`) |
| **Przypadkowa architektura** | ekran odzwierciedla kolejność dodawania funkcji: nieuwierzytelniona trasa zwracająca surowy JSON, modal będący stroną, zakładka „settings” zawierająca cztery niepowiązane rzeczy, reguła agenta zachęcająca do jednorazowych stylów | ścieżka trasy/komponentu/reguły oraz to, co widzi użytkownik, gdy trafia tam w ten sposób | popraw punkt wejścia (guard, redirect, layout) albo regułę, nie kolor |

Trzecia kategoria jest najtrudniejsza do zauważenia na zrzucie ekranu i najłatwiejsza do pominięcia. Zapytaj
wprost: *co dzieje się, jeśli ktoś trafi na ten widok po wylogowaniu, bez danych albo bezpośrednio
z linku?*

Zarzuty, których plan nie adresuje, pozostają w `## Charges` oznaczone jako **deferred** z uzasadnieniem —
nie są usuwane.

### Skanowanie wartości zakodowanych na sztywno

Lista kandydatów, nie werdykt — każde trafienie to możliwy zarzut dotyczący brakującego tokenu. Uruchamiaj je na
plikach widoku (nigdy na samym źródle tokenów), we wstępnym audycie i po każdej fazie wizualnej:

```bash
grep -nE '#[0-9a-fA-F]{3,8}\b|rgba?\(|hsla?\(|oklch\(|-\[[0-9.]+(px|rem)\]|\b(bg|text|border|ring|outline|from|via|to|fill|stroke|shadow|divide)-(slate|gray|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose|white|black)\b' <view files>
```

Stosy inne niż Tailwind: zachowaj część dotyczącą funkcji kolorów, usuń część dotyczącą klas palety i dodaj
własną formę literału stosu (hex wewnątrz styled-component, kolor we właściwości `sx`).
Liczba powinna spadać faza po fazie; liczba, która rośnie, oznacza regresję.

## Kontrakt systemu projektowego

Kontrakt ma dwie połowy i żadna nie wskazuje narzędzia:

1. **Tokeny semantyczne** — jedno źródło wartości, z nazwami opisującymi **rolę** (`primary`,
   `surface`, `muted`, `destructive`), nigdy kolor (`purple-600`).
2. **Importowalne komponenty żyjące w repozytorium** — czytelne dla agenta, a nie zależność
   typu czarna skrzynka, o której może tylko zgadywać.

Bez obu agent na każdym widoku na nowo wynajduje prymitywy. Z obiema ma miejsce, gdzie może szukać.

| Stos | Gdzie żyją wartości | Gdzie żyją komponenty |
| --- | --- | --- |
| **Tailwind v4 + shadcn/ui** (wariant kursu) | `:root` / `.dark` w CSS, publikowane przez `@theme` / `@theme inline` | skopiowane do repozytorium, zwykle `src/components/ui` |
| **CSS Modules / zwykłe zmienne CSS** | plik zmiennych (`:root`, często `theme.css` / `variables.css`) | katalog współdzielonych komponentów, importowany przez ścieżkę |
| **CSS-in-JS z motywem** (styled-components, vanilla-extract, Panda) | obiekt motywu lub plik tokenów `.css.ts` | stylizowane prymitywy eksportowane z jednego modułu |
| **Biblioteka komponentów z motywem** (MUI, Chakra, Mantine) | obiekt motywu/konfiguracji biblioteki, rozszerzony w twoim kodzie | komponenty biblioteki, opakowane lokalnie tam, gdzie je dostosowujesz |

Szczegół Tailwind: `:root` / `.dark` przechowują **wartości**, `@theme inline` **publikuje** je jako
`--color-*`, a dopiero wtedy istnieje `bg-primary`. Surowe kolory wpisane bezpośrednio do
`@theme inline` to klasyczna awaria dark mode — wartości `.dark` są obecne, przełącznik
nic nie robi. Inne stosy mają własną wersję tego podziału; znajdź ją, zanim zaczniesz edytować.

Wybierz wariant pasujący do repozytorium i zapisz go w `change.md`:

- **Istniejący system projektowy** — przeczytaj jego źródło wartości, współdzielone komponenty i notatki projektowe,
  zanim cokolwiek zaproponujesz. Rozszerz go; nie twórz drugiej palety ani nie uruchamiaj drugiego
  `shadcn init`. Istniejący, gorszy system jest lepszy niż lepszy system, który przynosisz.
- **Świeży starter z martwym plikiem tokenów** — najczęstszy przypadek greenfield: starter
  dostarcza tokeny i jeden lub dwa komponenty, a ekrany używają klas literałowych. Faza 1 nie polega
  na wyborze motywu; polega na tym, aby istniejący widok odczytywał tokeny już tam obecne. Dopiero potem
  warto wybierać nowe wartości.
- **Brak systemu projektowego** — wprowadzenie kontraktu **jest** zmianą. Zaproponuj go przy trzech
  warunkach określonych w `change.md`: (1) oznaczony jako dodający zależność — decyzję podejmuje uczeń;
  (2) ograniczony do bloku tokenów oraz 2–3 komponentów używanych przez ten widok, a nie całej biblioteki;
  (3) przegrywa z czymkolwiek, co repozytorium już ma.
- **Nazwany motyw lub preset** — odwzoruj jego wartości na istniejące nazwy zmiennych; zachowaj niewielką
  liczbę (primary, surface, border, muted, destructive, plus radius i skala odstępów).

Niezależnie od źródła wartości, **zdeponuj je w repozytorium**: surowe wartości w pliku w
folderze zmiany oraz linię wskazującą, skąd pochodzą, obok edytowanego bloku.
Wartości żyjące wyłącznie w oknie czatu to wartości, które następna sesja ponownie wymyśli.

Dodawanie komponentu: użyj własnej ścieżki stosu — `npx shadcn add <name>` (lub shadcn MCP, jeśli
jest już skonfigurowany) w wariancie kursu. Nie wymagaj `mcp init`, aby ukończyć zmianę.

## Definicja ukończenia: macierz 7 stanów

Faza stanów jest ukończona, gdy każda komórka jest **pokazana** (w kitchen sink) lub oznaczona jako
**N/A z uzasadnieniem** — nie wtedy, gdy szczęśliwa ścieżka wygląda dobrze.

| Stan | Co sprawdzić |
| --- | --- |
| default | zbudowany wyłącznie z tokenów i komponentów repozytorium |
| hover | widoczna zmiana, sterowana tokenami |
| focus-visible | fokus klawiatury widoczny na każdej kontrolce; własny token (`--ring` w shadcn), nie domyślny przeglądarki |
| disabled | wygląda i zachowuje się jak wyłączony, nadal jest czytelny |
| error | komunikat obok pola/akcji, token `destructive`, nie sam kolor |
| empty | brak danych: prawdziwy stan pusty, nie pusta ramka ani surowy JSON |
| loading | skeleton lub spinner; brak skoku layoutu po nadejściu danych |

`disabled`, `error` i `focus-visible` odchodzą od siebie jako pierwsze, ponieważ nic na szczęśliwej ścieżce
ich nie ćwiczy. Przesunięcie akcentu nie przesuwa tokenu fokusu — sprawdź go osobno.

Minimum, nie kurs WCAG: każda kontrolka ma dostępną nazwę, a kontrast przy zmianach tokenów
przetrwa oba motywy, jeśli aplikacja ma dark mode. Dark mode zmienia się na warstwie tokenów;
przejście dark mode, które edytuje klasy komponentów, to zamaskowany zarzut o brakujące tokeny.
Desktop plus **jedna** szerokość mobilna — nie macierz responsywności.

## Bramka wizualna

Jeden widok, każdy stan widoczny jednocześnie. Najtańsza forma nie wymaga runnera testów: strona
**kitchen-sink** renderująca widok we wszystkich siedmiu stanach obok siebie, ze zrzutami ekranu na
desktopie i przy jednej szerokości mobilnej. Służy podwójnie jako dowód przeglądu i działa na każdym stosie.

Jeśli repozytorium ma już narzędzie testowania zrzutów ekranu, podłącz do niego bramkę — np.
`await expect(page).toHaveScreenshot({ maxDiffPixels: 100 })` z Playwright, maskując
niestabilne regiony (daty, avatary, liczniki). Nie instaluj go tylko po to, aby spełnić tę umiejętność.
Nigdy nie aktualizuj baseline'u, aby CI było zielone, bez uprzedniego wyjaśnienia różnicy wizualnej.

## Utrwal zmianę

Poprawka trwa jedną sesję, chyba że następnemu agentowi powiesz, gdzie szukać. Przed przeglądem:

1. **Reguła.** Dodaj krótki blok UI do pliku zasad agenta w repozytorium (tego znalezionego przez wstępny audyt;
   rozszerz go, nie twórz drugiego; zapisz go **poza** blokiem
   `<!-- BEGIN @przeprogramowani/10x-cli -->` … `<!-- END … -->`, który CLI przepisuje
   przy każdym `get`): gdzie żyją tokeny, gdzie żyją komponenty,
   „sprawdź `<components dir>` przed utworzeniem komponentu; dodaj brakujące przez
   `<stack's path>`”, „żadnych literałów kolorów ani wartości arbitralnych w widokach — używaj tokenów” oraz
   gdzie żyje kitchen sink. Usuń lub przepisz każdą regułę, która zachęca do jednorazowych wartości.
2. **Kontrola.** Jeśli repozytorium ma już linter lub hook pre-commit, dodaj skanowanie wartości zakodowanych na sztywno
   (lub własną regułę lintera dla tego) ograniczone do widoków oczyszczonych przez tę zmianę —
   nieprzechodząca kontrola jest lepsza od reguły, o której agent zapomniał. Nowa zależność lint podlega tym samym trzem
   warunkom co nowy system projektowy: jest proponowana, ograniczona zakresem, a decyzję podejmuje uczeń.
3. **Dokumentacja jako kontekst.** Jeśli repozytorium ma Storybook lub dokumentację komponentów, wskaż w regule je
   zamiast je powtarzać.

## Pętla przeglądu: od zarzutu do PR

Widok może być technicznie poprawny, a nadal nieczytelny — nagłówek konkurujący z głównym
przyciskiem, każda informacja o tej samej wadze, każda drobna rzecz we własnej karcie.
Oceniaj layout **po** zbudowaniu widoku z tokenów i komponentów repozytorium; na ekranie posklejanym
z jednorazowych klas krytyka layoutu sprowadza się do kosmetycznych poprawek.

1. Uzyskaj krytykę: `/10x-impl-review`, plus opcjonalne przejście layoutu (Impeccable,
   `frontend-design`), jeśli uczeń ma je zainstalowane. Nie dostarczaj tych narzędzi razem z repozytorium.
2. Posortuj każde ustalenie według **wpływu na użytkownika**: brakujący focus ring lub martwa zakładka
   zasługują na równie konkretną decyzję jak błąd logiki.
3. Napraw albo zapisz ustalenie jako odroczone z uzasadnieniem. Cisza nie jest triage.
4. Uruchom ponownie bramkę wizualną. Ustalenie, które zmieniło widok bez zmiany baseline'u, jest
   sygnałem ostrzegawczym.

Lista kontrolna mergowania: [`ui-quality-checklist`](references/ui-quality-checklist.md). Samo zielone CI
nie jest bramką — test zrzutu ekranu chętnie przejdzie na widoku, którego stan `disabled` nigdy nie został
wyrenderowany.

## Routing modeli (najpierw faza, potem dostępność)

Żaden konkretny model nie jest wymagany; praca wymaga **vision** (odczytania zrzutu ekranu) oraz **tool use**.
Kieruj według fazy: najsilniejszy dostępny model do audytu, planu i przeglądu (zła decyzja tam
kosztuje tuzin edycji w złym kierunku; do przeglądu najlepiej nie ten, który napisał
kod); tańsza warstwa robocza do wdrażania zarzutów w pętli render → compare → fix.
Eskaluj tylko wtedy, gdy **ten sam zarzut przetrwa dwie rundy**. Jeden model do wszystkiego jest w porządku —
podział zmienia koszt, nie metodę. Zupełnie bez vision? Wyrenderuj kitchen sink,
opisz stany tekstowo, zachowaj bramkę zrzutów ekranu w CI.

## Twarde reguły

- Żadnego promptu, który brzmi wyłącznie „zrób to ładniej / atrakcyjniej”.
- Każdy zarzut zawiera plik, linię i wpływ na użytkownika, zanim trafi do planu.
- Kolory, typografia, radius, odstępy: token z systemu **tego repozytorium**, nie literał w widoku.
- Użyj ponownie istniejącego komponentu albo dodaj go przez własną ścieżkę stosu; nigdy drugiego `Button`.
- Jeden widok plus globalne tokeny na zmianę. Nie rebranding całego MVP.
- Zmiana kończy się regułą w pliku zasad agenta, nie tylko ładniejszym zrzutem ekranu.

## Awarie do odrzucenia

| Zapach | Zrób to zamiast tego |
| --- | --- |
| Domyślny fioletowo/niebieski gradient „AI landing” | Najpierw zmień `--primary` / tokeny motywu |
| Nowy prymityw `div`+CSS kopiujący komponent DS | Zaimportuj właściwy komponent |
| Zakładka „Selected”, która jest tylko CSS i nie można jej kliknąć | Podłącz stan |
| Nieuwierzytelnione wejście lub wejście w stanie pustym, które wyrzuca surowy JSON / pustą ramkę | Popraw punkt wejścia; to zarzut architektoniczny |
| `shadcn init` (lub odpowiednik) w repozytorium, które już dostarcza tokeny i komponenty | Rozszerz pierwszy system |
| Aktualizowanie baseline'u zrzutu ekranu, aby CI było zielone | Wyjaśnij różnicę wizualną; aktualizuj tylko, jeśli jest zamierzona |
| „Napraw design” jako jedno zadanie agenta w wątku CRUD | Nowy folder zmiany, audyt, plan, bramka |

## Playbooki

Wszystkie zachowują listę zarzutów.

- **Motyw lub restyle jednego widoku** — domyślna ścieżka, zacznij od tokenów.
- **Odziedziczony slop agenta** — spodziewaj się wszystkich trzech kategorii; napraw tokeny i współdzielony komponent
  przed dotknięciem layoutu oraz sprawdź plik zasad agenta pod kątem instrukcji, która to spowodowała.
- **Pojedynczy komponent** — pomiń fazę tokenów tylko wtedy, gdy jego wartości już pochodzą z tokenów.
- **Dark mode** — warstwa tokenów, oba motywy w kitchen sink, kontrola kontrastu w bramce.
- **Przejście focus/keyboard** — zawiera je faza stanów: focus-visible, nazwy kontrolek, kolejność tabulacji.