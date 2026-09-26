# Lessons Learned

> Append-only register of recurring rules and patterns. Re-read at start by /10x-frame, /10x-research, /10x-plan, /10x-plan-review, /10x-implement, /10x-impl-review.

## Uruchamiaj polecenia bez login shell

- **Context**: Uruchamianie poleceń terminalowych przez agenta w sandboxie tego repozytorium.
- **Problem**: Login shell ładuje `/etc/profile.d/im-config_wayland.sh`, który próbuje pisać do journald przez `systemd-cat`. Sandbox odmawia utworzenia deskryptora i generuje trzy mylące komunikaty `Failed to create stream fd: Operation not permitted`, mimo że właściwa komenda kończy się powodzeniem.
- **Rule**: Uruchamiaj polecenia bez login shell (`login: false`), chyba że zadanie rzeczywiście wymaga profilu logowania. Ten komunikat uznawaj za nieszkodliwy wyłącznie wtedy, gdy właściwa komenda zakończyła się kodem `0`.
- **Applies to**: all

## Write context and code in English; Polish only in translation files

- **Context**: Every context file (`context/**`, including plans, briefs, indexes, notes and roadmap edits) and every code file (identifiers, comments, docs, UI strings), excluding translation (i18n/locale) files.
- **Problem**: AI agents are not good at writing correct Polish. They use too many literal calques from English, which makes the Polish sound odd; their English is more correct.
- **Rule**: Always write context and code files in English, including UI copy in mockups and components. Never write Polish outside translation files; Polish UI text belongs only in locale/translation files.
- **Applies to**: all
