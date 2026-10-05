---
name: 10x-lessons-sync
description: Download missing unlocked 10xDevs lesson prose and automatically update an existing local HTML summary portal in Polish and English. Use for weekly course-material updates, not for downloading 10x CLI skills, prompts, rules, or configs.
---

# 10x Lessons Sync

Use the bundled `scripts/sync-lessons.mjs`. It reads the active Zen session, discovers every unlocked lesson from a course page, and writes only missing Markdown exports. After a real sync, update the existing team-summary portal as part of the same task; do not stop at downloading files.

## Safety and authorization

- The script reads only the `token` cookie for `platforma.przeprogramowani.pl` from a local Zen profile and sends it only to that HTTPS origin.
- Never print, log, persist, or place the cookie in a command argument.
- If the user has not explicitly approved using the Zen session for this run, ask immediately before executing the script. A preview also contacts the platform and therefore needs the same authorization.
- Treat the platform as read-only. The downloader writes only newly downloaded `.md` files and `.10x-lessons.json` tracking metadata. The agent may also edit the existing local summary portal and its existing supporting files to incorporate downloaded lessons.
- Never overwrite an existing lesson. Do not add a force or refresh mode.

## Run

Resolve the script relative to this `SKILL.md`, then preview:

```bash
node <skill-dir>/scripts/sync-lessons.mjs --dry-run
```

If the user requested synchronization, run it after inspecting the preview:

```bash
node <skill-dir>/scripts/sync-lessons.mjs
```

Defaults:

- course: Polish 10xDevs 4;
- destination: `./lekcje-10xdevs-4` in the current working directory.

Use `--url <course-or-lesson-url>` for another course, language, or stable starting lesson, and `--output <directory>` for another destination. For Foundations, use `https://platforma.przeprogramowani.pl/courses/10xdevs-foundations/pl`. Run `--help` for syntax.

## Expected behavior

- `.10x-lessons.json` maps lesson references to local files. A mapped lesson whose file still exists is skipped without downloading its Markdown again.
- On the first run over an existing folder, the script recognizes the platform filename with or without its numeric order prefix, records it, and skips the download.
- Newly unlocked lessons are written with an order prefix and the filename supplied by the platform.
- Locked lessons are ignored. Existing Markdown is never compared with or replaced by a newer upstream version; this skill is incremental download, not content refresh.
- Stop on expired login, an unexpected origin, malformed course markup, or a non-Markdown response. Do not fall back to `10x get`: that command downloads agent artifacts rather than lesson prose.

After execution, report the destination plus downloaded, already-present, and failed counts. Verify that every newly created file is nonempty Markdown and not an HTML login page.

## Update the summary portal

After a non-preview run, look for `docs/10xdevs-portal/index.html` in the current repository, or use the portal path established by the user. If it exists, follow [references/update-summary-portal.md](references/update-summary-portal.md) to reconcile lesson coverage and update both languages, module decision trees and the whole-course work map without asking for separate confirmation. Reconcile even when no files were downloaded: earlier local lessons may still be missing from the portal.

`--dry-run` changes neither exports nor the portal. If no portal exists, report that and finish downloading; do not scaffold a new site. If authentication or discovery fails, stop synchronization and leave the portal unchanged. After a partial download, update only successfully verified local lessons and report the remaining failures.

The Node script downloads prose; summary authoring is the subsequent agent step, not a capability of the standalone script. Do not download skill packages, install tools, publish, commit or push unless separately requested. Report the portal path, incorporated lessons/modules, PL/EN parity and checks alongside the download counts.
