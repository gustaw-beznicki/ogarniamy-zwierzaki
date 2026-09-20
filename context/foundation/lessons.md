# Lessons Learned

> Append-only register of recurring rules and patterns. Re-read at start by /10x-frame, /10x-research, /10x-plan, /10x-plan-review, /10x-implement, /10x-impl-review.

## Uruchamiaj polecenia bez login shell

- **Context**: Uruchamianie poleceń terminalowych przez agenta w sandboxie tego repozytorium.
- **Problem**: Login shell ładuje `/etc/profile.d/im-config_wayland.sh`, który próbuje pisać do journald przez `systemd-cat`. Sandbox odmawia utworzenia deskryptora i generuje trzy mylące komunikaty `Failed to create stream fd: Operation not permitted`, mimo że właściwa komenda kończy się powodzeniem.
- **Rule**: Uruchamiaj polecenia bez login shell (`login: false`), chyba że zadanie rzeczywiście wymaga profilu logowania. Ten komunikat uznawaj za nieszkodliwy wyłącznie wtedy, gdy właściwa komenda zakończyła się kodem `0`.
- **Applies to**: all
