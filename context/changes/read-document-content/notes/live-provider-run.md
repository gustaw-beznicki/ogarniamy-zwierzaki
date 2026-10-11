# Controlled live-provider run (Phase 3, manual 3.4)

Status: **PASSED on 2026-10-11.** Sanitized evidence only: IDs are shortened, no extracted text is recorded beyond
the fact that the fictional fixture was recovered.

## Setup

- Provider: `di-ogarniamy-2mnni3xfesmhm` (S0, swedencentral), `prebuilt-read`, API `2024-11-30`, signed in with the
  owner's `az login` (`DocumentReading:Auth=DeveloperCredential`).
- API from this branch (after the phase 3 review fixes, 6aca6c0) on `127.0.0.1:5190`, worker enabled, against a
  separate local database `ogarniamy_s03`, so the reading backfill could not pick up documents from the shared local
  database. Local Azurite for originals.
- Fixture: a synthetic 8-page image-only PDF (the fictional `synthetic-note.png` with a page number per page,
  616,266 bytes). It has no text layer, so the local parser routed all pages to OCR (`pages=1-8`).
- A synthetic account and animal were created through the API; the document was captured through the normal upload
  routes.

## Steps and observations

| Step | Observation |
| --- | --- |
| 1. Capture | Completion 200; job queued; worker claimed it (attempt 1). |
| 2. Submission | One Analyze POST; run file `submitted`, ranges `1-8`, operation ID saved, cleanup `pending`, local-parse checkpoint saved (F3). |
| 3. Crash | API killed with SIGKILL right after the "submitted for OCR" log line, before the result was fetched. Job left `reading` with a live lease (about 1 min 50 s left). |
| 4. Restart | Same binary restarted; after the lease expired the worker reclaimed the job (attempt 2) and logged "Resuming saved OCR operation". No second Analyze POST (one submission log line across both runs). |
| 5. Publication | Job `read`, published generation 1, 8 pages with method `ocr` in page order, fixture text recovered on every page. Read in about 1.2 s after the reclaim. |
| 6. Cleanup | Run file `retrieved`, cleanup `acknowledged` on the first try (DELETE 204). A later GET of that result ID returned 404 `{"code":"NotFound","message":"Analyze result does not exist."}`. |
| 7. Original | Downloaded through the API after reading; sha256 identical to the fixture. |
| 8. Logs | No extracted text, fixture text or file name in the API log. |

## Provider behaviour and limitations

- DELETE of a result that does not exist answers 204 (also with a wrong model ID); a wrong path answers 404
  `{"error":{"code":"404",...}}`. A 204 is therefore only a deletion request, never proof that a result existed or was
  erased. GET of a deleted result answers 404 with top-level `code` `NotFound`.
- A crash between the Analyze POST and saving the operation ID (not exercised here) leaves an analysis that cannot be
  deleted early; it relies on the provider's bounded retention (up to 24 hours).
- Reclaim after a non-graceful stop waits for the two-minute lease to expire; the reclaim counts as a new attempt
  (graceful shutdown does not, F1).

## Result sizes (input for review finding F2)

Measured with a separate synthetic submission of the same fixture (result deleted with 204 afterwards):

| Pages | Result JSON | Per page | OCR text characters | JSON bytes per text character |
| --- | --- | --- | --- | --- |
| 8 | 59,300 bytes | about 7.4 KB | 1,552 | about 38 |

The JSON is about 38 times the extracted text (words carry polygons, spans and confidences). Real photos from 1.4
yielded 860-2,600 characters per page, which suggests roughly 35-100 KB of JSON per dense page. A 64 MiB result cap
would then hold several hundred dense pages per file; the 10M-character document limit cannot be reached through a
single OCR result under any cap that fits F1 memory.
