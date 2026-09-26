---
name: 10x-archive
description: Archive a completed change by moving its folder into context/archive/ and stamping change.md with archived status
---
# /10x-archive — Zamknij zmianę

Przenieś folder ukończonej zmiany z `context/changes/<change-id>/` do `context/archive/<created-date>-<change-id>/`, oznacz `change.md` za pomocą `status: archived` + `archived_at`, użyj `git mv`, aby historia plików została zachowana, oraz — jeśli `context/foundation/roadmap.md` zawiera element roadmapy, którego `Change ID` jest równe `<change-id>` — zamknij również ten element: zmień jego `Status` na `done` i dodaj wpis do sekcji `## Done` roadmapy.

Brama jest **łagodna, wyłącznie ostrzegająca** — `/10x-archive` blokuje działanie przy niezatwierdzonych zmianach wewnątrz folderu zmiany lub wcześniej istniejących zmianach w stagingu. Wszystko inne (nieukończony Progress, brak impl-review, status spoza `{implemented, impl_reviewed}`) jest zgłaszane jako ostrzeżenie, po którym następuje monit o potwierdzenie; użytkownik nadal może zarchiwizować zmianę.

Po archiwizacji każda inna umiejętność 10x odmawia zapisu wewnątrz `context/archive/<...>/` (każda chroniona umiejętność sprawdza prefiks rozwiązanej ścieżki i przerywa działanie ze stałym komunikatem). Zarchiwizowane foldery są zgodnie z konwencją tylko do odczytu.

## Początkowa odpowiedź

Gdy to polecenie zostanie wywołane:

1. **Sprawdź, czy podano argument**:
   - Jeśli podano argument, przeanalizuj go (zobacz „Parsowanie argumentu” poniżej) i przejdź do „Rozwiązania”.
   - Jeśli NIE podano argumentu, odpowiedz następującym komunikatem i **ZATRZYMAJ SIĘ**:

```
I'll archive a completed change. Please provide a change-id (kebab-case slug) or path:

Examples:
  /10x-archive context-dir-restructure
  /10x-archive @context/changes/oauth-login/

You can list active changes with: `ls context/changes/`
```

   Następnie **poczekaj**, aż użytkownik poda argument.

## Parsowanie argumentu

Weź pierwszy token rozdzielony białymi znakami. Znormalizuj:

1. Usuń początkowy `@`, jeśli występuje.
2. Usuń końcowy `/`, jeśli występuje.
3. Jeśli wynik zawiera `/`, weź ostatni niepusty segment ścieżki.

Wynikiem jest `<change-id>`.

## Rozwiązanie

1. Rozwiąż `<change-id>` do `context/changes/<change-id>/`. Jeśli ta ścieżka nie istnieje:
   - Sprawdź `context/archive/` pod kątem katalogu, którego nazwa kończy się na `-<change-id>` — jeśli zostanie znaleziony, wypisz: `error: change \"<change-id>\" is already archived at <path>.` i ZATRZYMAJ SIĘ.
   - W przeciwnym razie wypisz: `error: no change folder at context/changes/<change-id>/. Run `ls context/changes/` to list active changes.` i ZATRZYMAJ SIĘ.
2. Odczytaj frontmatter `context/changes/<change-id>/change.md` (`status`, `created`).
   - Jeśli `status: archived`, wypisz: `error: change \"<change-id>\" is already archived in change.md but its folder is still under context/changes/. Inspect manually before re-running.` i ZATRZYMAJ SIĘ.
   - Jeśli brakuje `created` lub nie ma formatu `YYYY-MM-DD`, wypisz: `error: change.md.created is missing or malformed; cannot derive archive folder name.` i ZATRZYMAJ SIĘ.

## Bezwzględna odmowa: niezatwierdzone zmiany

Dwie kontrole przedstartowe. Niepowodzenie którejkolwiek blokuje archiwizację.

**1. Niezatwierdzone edycje wewnątrz folderu zmiany.** Uruchom:

```bash
git status --porcelain "context/changes/<change-id>/"
```

Jeśli wynik nie jest pusty, **zablokuj** i wypisz:

```
✗ Cannot archive: context/changes/<change-id>/ has uncommitted changes.

  <one line per offending path from git status --porcelain>

Commit or stash them first, then re-run /10x-archive.
```

**2. Istniejące wcześniej zmiany w stagingu gdziekolwiek.** Krok commitu archiwizacji (zobacz „Przenieś i oznacz” poniżej) pakuje do commitu wszystko, co jest w stagingu w chwili commitowania. Jeśli użytkownik ma niepowiązane zmiany w stagingu z wcześniejszej pracy, po cichu trafiłyby one do commitu `chore(archive): close ...`. Uruchom:

```bash
git diff --cached --quiet
```

Jeśli kod wyjścia nie jest zerowy, **zablokuj** i wypisz:

```
✗ Cannot archive: pre-existing staged changes would be bundled into the archive commit.

  <output of `git diff --cached --name-only`>

Either commit them first or `git reset` to unstage, then re-run /10x-archive.
```

Niepowodzenie którejkolwiek kontroli → ZATRZYMAJ SIĘ. Nie przechodź do monitu ostrzegawczego; są to bezwzględne blokady.

Jeśli `git` nie jest dostępny lub repozytorium nie jest repozytorium git, wypisz: `warning: not a git repository — skipping uncommitted-changes block.` i kontynuuj. (Archiwizacja nadal działa bez git; tracimy jedynie zachowanie historii przez `git mv` i pomijamy krok commitu archiwizacji).

## Miękkie ostrzeżenia (nieblokujące)

Zbierz następujące ostrzeżenia, a następnie przedstaw je wszystkie naraz za pomocą pojedynczego monitu o potwierdzenie.

1. **Kontrola statusu**: odczytaj `change.md.status`. Jeśli NIE należy do `{implemented, impl_reviewed}`, dodaj do kolejki: `Status is \"<status>\"; expected \"implemented\" or \"impl_reviewed\".`
2. **Kontrola oczekującego Progress**: przeanalizuj sekcję `## Progress` w `context/changes/<change-id>/plan.md` (jeśli `plan.md` istnieje). Dla każdego bloku `### Phase N:` zidentyfikuj jego podsekcje `#### Automated` i `#### Manual` oraz policz osobno wiersze `- [ ]` pod każdą z nich. Niech `<X>` = łączna liczba oczekujących automatycznych pozycji we wszystkich fazach, `<Y>` = łączna liczba oczekujących ręcznych pozycji we wszystkich fazach, `<N>` = `<X> + <Y>`.

   - **Jeśli plan używa podsekcji Auto/Manual** (dowolny blok `### Phase N:` zawiera nagłówek `#### Automated` lub `#### Manual`) i `<N> > 0`, dodaj do kolejki: `<N> Progress items still pending (<X> automated, <Y> manual): <comma-separated list of \"N.M <title>\" tokens, truncated to 5 with \"…\" if longer>.` Uporządkuj połączoną listę tokenów najpierw według pozycji automatycznych (w kolejności dokumentu), a następnie ręcznych (w kolejności dokumentu); limit skrócenia wynoszący 5 dotyczy połączonej listy.
   - **Starsze zachowanie awaryjne**: jeśli żaden blok `### Phase N:` w Progress nie zawiera nagłówka `#### Automated` ani `#### Manual`, wróć do pierwotnego zachowania — policz linie `- [ ]` pod podnagłówkami `### Phase`; jeśli jakiekolwiek pozostają, dodaj do kolejki: `<N> Progress items still pending: <comma-separated list of \"N.M <title>\" tokens, truncated to 5 with \"…\" if longer>.` (bez rozbicia w nawiasie). Zachowuje to zerową zmianę zachowania dla planów utworzonych przed workflow-v2.
   - Jeśli brakuje `plan.md`, dodaj do kolejki: `No plan.md found in change folder.` i pomiń liczenie Progress.
3. **Kontrola pokrycia przeglądami**: zbierz numery wszystkich faz z `## Progress` (w tym oczekujących faz), a następnie odczytaj `reviews/impl-review*.md` i utwórz sumę zbiorów numerów faz faktycznie sprawdzonych dla tego planu. Nowe raporty deklarują `Reviewed phases`; pełny raport wymieniający wszystkie fazy obejmuje je tak samo jak kilka raportów fazowych. W przypadku starszych raportów akceptuj jednoznaczny zakres, taki jak `Phase 3 of 4` (obejmuje tylko 3), jawny zakres/listę lub pełny przegląd, którego treść ustala dokładnie, które fazy zostały sprawdzone. Sama nazwa pliku, status `impl_reviewed`, werdykt lub samodzielna etykieta „Full plan” nie są dowodem. Ignoruj raporty dla innych planów. Sprzeczny lub niejasny zakres → dodaj do kolejki `Uncertain review coverage: <report paths and reason>` bez zgadywania numerów faz. Dodaj do kolejki `Missing review coverage for phases: <numbers>` dla różnicy między Progress a potwierdzonym pokryciem. Jeśli żadne raporty nie istnieją, dodaj również do kolejki `No impl-review found at reviews/impl-review*.md.` Jeśli Progress jest nieobecny lub nie można go przeanalizować, ostrzeż, że nie można określić pokrycia; nie twierdź, że pokrycie jest pełne.
4. **Kontrola brakujących SHA**: przeanalizuj sekcję `## Progress` w `plan.md` (jeśli istnieje). Policz wiersze `- [x]`, których linia NIE kończy się na ` — <sha>`, gdzie `<sha>` ma 7+ znaków szesnastkowych (tj. regex ` — [0-9a-f]{7,}$` nie pasuje). Jeśli liczba jest niezerowa, dodaj do kolejki: `<N> Progress rows missing SHA suffix: <comma-separated \"N.M <title>\" tokens, truncated to 5 with \"…\" if longer>.` Wiersze bez SHA są uzasadnione dla faz z pustym diffem oraz planów ukończonych przed wdrożeniem kontraktu SHA — jest to miękki sygnał, a nie defekt. Pomiń bez komunikatu, jeśli brakuje `plan.md` (kontrola oczekującego Progress już obsłużyła ten przypadek).

5. **Kontrola historii SHA**: postępuj zgodnie z [diagnozą SHA i przepinaniem](references/archive-sha.md) dla ukończonych wierszy z istniejącymi sufiksami SHA. Zbierz jego diagnostykę wraz z powyższymi ostrzeżeniami. Zakończ całą diagnozę wyłącznie do odczytu oraz potwierdzenie kandydata przed dokonaniem jakichkolwiek edycji plików.

Jeśli dodano do kolejki co najmniej jedno ostrzeżenie, wypisz:

```
⚠ /10x-archive warnings for <change-id>:

  - <warning 1>
  - <warning 2>
  - <warning 3>
```

Jeśli gotowe jest zweryfikowane mapowanie przepięcia, pokaż problem, gałąź docelową, mapowanie stare → nowe SHA, tytuł commitu/zakres zmiany, identyfikatory dotkniętych wierszy oraz łączną liczbę wierszy, wraz ze wszystkimi ostrzeżeniami. Zapytaj raz: **Update and archive / Archive without repointing / Cancel** (po polsku: **Zaktualizuj i archiwizuj / Archiwizuj bez podmiany / Anuluj**). Wyraźne zatwierdzenie jest wymagane nawet wtedy, gdy PR potwierdza mapowanie. Update and archive → przenieś wyłącznie to zatwierdzone mapowanie do „Przenieś i oznacz”; Archive without repointing → pozostaw Progress bez zmian; Cancel → ZATRZYMAJ SIĘ bez zmian plików. Zastępuje to zwykły monit ostrzegawczy poniżej; nigdy nie traktuj potwierdzenia kandydata jako zgody na edycję. Przy niepełnej diagnostyce lub braku zweryfikowanego mapowania nie oferuj ani nie zalecaj aktualizacji; użyj zwykłego monitu ostrzegawczego.

W przeciwnym razie zapytaj użytkownika:

- pytanie: `Archive \"<change-id>\" anyway?`
  nagłówek: `Archive`
  opcje:
  - etykieta: `Continue archiving`
    opis: `Move the folder to context/archive/ despite the warnings.`
  - etykieta: `Resume implementation`
    opis: `Don't archive. Suggest /10x-implement <change-id> next.`
  - etykieta: `Cancel`
    opis: `Don't archive. Exit cleanly without further action.`
  multiSelect: false

**Zachęta tylko dla ręcznych pozycji**: jeśli kontrola oczekującego Progress powyżej dodała do kolejki ostrzeżenie, którego rozbicie wynosiło dokładnie `0 automated, <Y> manual`, gdzie `<Y> ≥ 1`, dodaj ` (Recommended)` do etykiety `Continue archiving`, aby monit wyraźnie zachęcał do archiwizacji — kontrole ręczne są często celowo odraczane, a archiwizacja jest oczekiwaną ścieżką. We wszystkich innych przypadkach (mieszane oczekujące, wyłącznie automatyczne, ostrzeżenie starszego zachowania awaryjnego lub brak ostrzeżenia Progress) przedstaw etykiety dosłownie.

- **Continue archiving** → przejdź do „Przenieś i oznacz” poniżej.
- **Resume implementation** → wypisz `→ /10x-implement <change-id>` i skopiuj to do schowka przez `pbcopy 2>/dev/null || clip.exe 2>/dev/null || xclip -selection clipboard 2>/dev/null || true` (lub `Set-Clipboard` w PowerShell) (najlepszy wysiłek, wieloplatformowo). ZATRZYMAJ SIĘ.
- **Cancel** → wypisz `Cancelled. Folder unchanged.` i ZATRZYMAJ SIĘ.

Jeśli nie dodano do kolejki żadnych ostrzeżeń i nie zaproponowano mapowania przepięcia, pomiń monit i przejdź bezpośrednio dalej.

## Przenieś i oznacz

1. **Oblicz miejsce docelowe archiwum**:
   - `CREATED=$(awk '/^created:/ {print $2; exit}' context/changes/<change-id>/change.md)` (prefiks daty, np. `2026-04-29`).
   - `DEST=\"context/archive/${CREATED}-<change-id>\"`.
   - Jeśli `$DEST` już istnieje, wypisz: `error: archive destination \"<DEST>\" already exists. Inspect manually.` i ZATRZYMAJ SIĘ.

   Przed jakimikolwiek zapisami ponownie sprawdź warunki przedstartowe i upewnij się, że wiersze planu oraz kandydat nadal odpowiadają zatwierdzonym danym. Zmienione mapowanie wymaga ponownego zatwierdzenia. Jeśli zatwierdzono przepięcie, zastosuj teraz wyłącznie zatwierdzone edycje sufiksów i zapisz `reviews/archive-sha-repoint.md` zgodnie z opisem w referencji. Odrzucenie przepięcia lub anulowanie nie może utworzyć tej notatki.

2. **Oznacz `change.md`** (na miejscu, przed przeniesieniem):
   - Ustaw `status: archived`.
   - Ustaw `archived_at: <ISO-8601 datetime, today, UTC>` — utworzone przez `date -u +\"%Y-%m-%dT%H:%M:%SZ\"`.
   - Ustaw `updated: <today as YYYY-MM-DD>`.
   - Zaktualizuj każdą z trzech linii frontmatter. NIE zmieniaj żadnego innego pola; w szczególności pozostaw `created` i `change_id` bez zmian.

3. **Przenieś folder**:
   - Preferuj `git mv \"context/changes/<change-id>\" \"$DEST\"`, aby historia została zachowana.
   - Jeśli `git mv` nie powiedzie się (nie jest to repozytorium git lub git z jakiegoś powodu odmawia), użyj awaryjnie `mkdir -p context/archive`, a następnie `mv \"context/changes/<change-id>\" \"$DEST\"`. Wypisz ostrzeżenie, jeśli użyto zachowania awaryjnego.
   - Potwierdź po przeniesieniu: `[ -d \"$DEST\" ] && [ ! -d \"context/changes/<change-id>\" ]`. Jeśli któraś kontrola się nie powiedzie, wypisz diagnostykę i ZATRZYMAJ SIĘ.

4. **Dodaj oznaczenie do stagingu wraz ze zmianą nazwy.** Aktualizacja w kroku 2 zmodyfikowała `change.md` w drzewie roboczym, ale `git mv` umieszcza w stagingu tylko zmianę nazwy z zawartością pliku z HEAD. Uruchom `git add \"$DEST/change.md\"`, aby oznaczenie frontmatter trafiło do tego samego commitu co zmiana nazwy. Jeśli zatwierdzono przepięcie, uruchom również `git add \"$DEST/plan.md\" \"$DEST/reviews/archive-sha-repoint.md\"`. Zweryfikuj, że diff w stagingu zawiera zatwierdzone zamiany sufiksów i notatkę mapowania, jak również przeniesienie; samo `git mv` nie ujmuje tych edycji ani nowej notatki.

5. **Zamknij pasujący element roadmapy.** Uruchom to przy **każdej** archiwizacji — wyszukiwanie roadmapy jest obowiązkowe. „Best effort” dotyczy tylko *edycji*: brakująca roadmapa lub nieznaleziony cel edycji są pomijane bez komunikatu i nigdy nie blokują, nie wycofują ani nie wywołują monitu dla archiwizacji. NIE oznacza to „załóż, że nie ma roadmapy i pomiń kontrolę”. Nieprzeprowadzenie wyszukiwania jest defektem — potwierdzenie (krok 7) musi zgłosić wynik w obu przypadkach.

   1. `test -f context/foundation/roadmap.md`. Jeśli nie istnieje, pomiń ten krok bez komunikatu.
   2. Zapisz, czy plik jest już zmodyfikowany: `ROADMAP_PREDIRTY=$(git status --porcelain context/foundation/roadmap.md 2>/dev/null)`. (Używane w podkroku 7 do podjęcia decyzji, czy dodać go do stagingu commitu archiwizacji).
   3. Odczytaj `context/foundation/roadmap.md`. Poszukaj użycia `<change-id>` jako `Change ID`:
      - w tabeli `## At a glance` — wiersz, którego komórka kolumny **Change ID** jest dokładnie równa `<change-id>`;
      - oraz w treściach `## Foundations` / `## Slices` — blok `### <ID>: …`, który zawiera linię `- **Change ID:** <change-id>`.

      `<ID>` jest lokalnym identyfikatorem roadmapy tego elementu (`F-NN` lub `S-NN`); `<Outcome>` jest tekstem jego linii `- **Outcome:**` (zachowaj początkowe `(foundation) `, jeśli występuje).
   4. **Brak dopasowania** → wypisz `ℹ context/foundation/roadmap.md has no item with Change ID \"<change-id>\" — roadmap left untouched.` i pomiń resztę tego kroku. Dopasowanie jest wyłącznie dokładnym ciągiem znaków; fragment roadmapy może tworzyć kilka zmian, więc niemal dopasowanie celowo *nie* jest zamykane.
   5. **Znaleziono dopasowanie** → zastosuj trzy poniższe edycje. Każda jest niezależna i best effort: jeśli cel nie znajduje się tam, gdzie umieszcza go szablon `/10x-roadmap` (ręcznie edytowana roadmapa, starszy format), pomiń tę podedycję, kontynuuj i odnotuj, co pominięto — nigdy nie przerywaj archiwizacji z powodu struktury roadmapy. Zmieniaj wyłącznie pola nazwane tutaj; pozostaw `Outcome`, `Prerequisites`, `Parallel with`, `Risk` itd. bez zmian.
      1. **`## At a glance`** — w dopasowanym wierszu tabeli ustaw komórkę kolumny **Status** na `done`.
      2. **Treść elementu** — w bloku `### <ID>: …` zmień linię `- **Status:**` na `- **Status:** done`.
      3. **Sekcja `## Done`** — dodaj jeden punkt pod nagłówkiem `## Done`, w udokumentowanym formacie tej sekcji:

         ```
         - **<ID>: <Outcome>** — Archived <today> → `context/archive/<CREATED>-<change-id>/`. Lesson: —.
         ```

         `<today>` to `date -u +%F` (`YYYY-MM-DD`); `<CREATED>` to wartość obliczona w kroku 1 „Oblicz miejsce docelowe archiwum”. Jeśli roadmapa nie ma nagłówka `## Done`, dodaj nagłówek oraz ten punkt na końcu pliku.
   6. Zwiększ aktualność frontmatter roadmapy: ustaw `updated: <today as YYYY-MM-DD>`. Pozostaw każdy inny klucz (`created`, `version`, `status`, `prd_version`, `main_goal`, `top_blocker`, …) bez zmian. Jeśli plik nie ma frontmatter YAML, pomiń ten podkrok.
   7. **Dodaj ją do stagingu commitu archiwizacji** — tylko jeśli `git` jest dostępny **i** `ROADMAP_PREDIRTY` (podkrok 2) było puste. Następnie uruchom `git add context/foundation/roadmap.md`, aby zamknięcie roadmapy trafiło do tego samego commitu co zmiana nazwy + oznaczenie. Jeśli `ROADMAP_PREDIRTY` nie było puste, plik już miał niezatwierdzone edycje; pozostaw zamknięcie roadmapy w drzewie roboczym i wypisz `⚠ context/foundation/roadmap.md had pre-existing uncommitted changes — closed roadmap item <ID> in the working tree but did NOT stage it. Commit it yourself.` Jeśli `git` jest niedostępny, edycja po prostu pozostaje w drzewie roboczym (kontrola przedstartowa już ostrzegła).
   8. Zapamiętaj `<ID>` i `<Outcome>` dla wyniku potwierdzenia.

6. **Zacommituj archiwum.** Utwórz jeden commit:

   ```bash
   git commit -m "$(cat <<'EOF'
   chore(archive): close <change-id>
   EOF
   )"
   ```

   Bez treści — temat jest mechaniczny, a diff (zmiana nazwy + oznaczenie frontmatter oraz zamknięcie roadmapy, gdy nastąpiło dopasowanie) jest oczywisty. Nigdy nie przekazuj flag `--no-verify` ani flag omijających podpisywanie. Jeśli hook przed commitem się nie powiedzie, napraw podstawowy problem i utwórz NOWY commit.

   Pomiń ten krok całkowicie, jeśli `git` jest niedostępny lub repozytorium nie jest repozytorium git (kontrola przedstartowa już ostrzegła).

7. **Wypisz potwierdzenie**:

```
✓ Archived <change-id>
  context/changes/<change-id>/  →  <DEST>/

change.md updated:
  status:       archived
  archived_at:  <ISO datetime>
  updated:      <today>

roadmap.md:     closed <ID> "<Outcome>"  →  Status: done, entry added to ## Done    ← if matched; else print: no item with Change ID "<change-id>" — checked, left untouched. Always print one of the two; it proves the lookup ran.

Committed as: <short SHA> chore(archive): close <change-id>

The folder is now read-only by convention. To start a new change: /10x-new <new-id>
```

## Obsługa błędów

- Każdy nieoczekiwany błąd systemu plików podczas przenoszenia pozostawia folder źródłowy na miejscu — edycje `change.md` w stagingu trafiają przed przeniesieniem, więc przy częściowym niepowodzeniu użytkownik widzi `status: archived` w `context/changes/<change-id>/change.md`, ale folder nadal znajduje się w `context/changes/`. `/10x-status` pokaże to jako ostrzeżenie `status drift: archived in wrong folder`. Ponowne uruchomienie `/10x-archive` jest bezpieczne: kontrola rozwiązania na początku wykryje `status: archived` i poprosi użytkownika o ręczną inspekcję.
- NIE próbuj wycofania — edycje change.md oznaczają zamiar, a częściowy stan można odzyskać ręcznie.
- Krok zamknięcia roadmapy („Przenieś i oznacz”, krok 5) jest izolowany: każde niepowodzenie jest przechwytywane, odnotowywane w wyniku potwierdzenia i pomijane. Nigdy nie przerywa archiwizacji ani nie uruchamia wycofania. Połowicznie zastosowaną edycję roadmapy można odzyskać ręcznie.

## Czego ta umiejętność NIE robi

- Nie dodaje brakujących SHA ani nie zmienia stanu ukończenia. Tylko jawnie zatwierdzone istniejące sufiksy ukończonych wierszy mogą zostać przepięte przed przeniesieniem; wiersze bez SHA oraz oczekujące pozostają bez zmian.
- Nie przełącza ani nie tworzy gałęzi, nie otwiera PR ani nie przepisuje istniejących archiwów. Archiwizuj w bieżącej gałęzi, diagnozując pochodzenie SHA względem rozwiązanej gałęzi integracyjnej.
- Nie uruchamia `pnpm test` / `pnpm build` / `pnpm ci:local` jako bramy — brama jest celowo łagodna i wyłącznie ostrzegająca.
- Nie wykonuje push. Commit archiwizacji trafia lokalnie; `git push` jest decyzją użytkownika.
- Nie przepisuje roadmapy poza zamknięciem jednego dopasowanego elementu. Gdy `context/foundation/roadmap.md` zawiera element, którego `Change ID` jest równy archiwizowanemu `<change-id>`, ta umiejętność zmienia tylko `Status` tego elementu (komórka tabeli + linia treści `### <ID>:`), dodaje jeden punkt `## Done` oraz aktualizuje datę `updated:`. Nigdy nie zmienia kolejności fragmentów, nie przelicza grafu zależności, nie edytuje innych elementów ani nie tworzy roadmapy, która nie istnieje. Brak dopasowania (lub brak pliku roadmapy) → roadmapa pozostaje bez zmian.
- Nie zapisuje do `context/archive/<...>/` po przeniesieniu; zarchiwizowane foldery są zgodnie z konwencją tylko do odczytu. Inne umiejętności 10x (`/10x-research`, `/10x-frame`, `/10x-plan`, `/10x-plan-review`, `/10x-implement`, `/10x-impl-review`, `/10x-tdd`, `/10x-goal-implement`) odmawiają działania, gdy rozwiązana ścieżka zaczyna się od `context/archive/`.
- Nie cofa archiwizacji. Aby ponownie zająć się zarchiwizowaną zmianą, otwórz nową zmianę za pomocą `/10x-new` i odwołaj się do zarchiwizowanego folderu dla kontekstu.