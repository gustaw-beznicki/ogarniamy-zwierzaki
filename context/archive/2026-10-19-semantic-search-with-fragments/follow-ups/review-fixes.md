# Review follow-ups: semantic-search-with-fragments

Queued from the full implementation review of 2026-11-09 (`reviews/impl-review.md`).

## F8 — Cap the reading client's Retry-After

- **Where**: `services/api/DocumentReading/AzureDocumentTextReader.cs:338`
- **What**: Apply the same one-hour cap the embedding client uses (`AzureOpenAiEmbeddingClient.MaxRetryAfter`) to the
  provider `Retry-After` honoured by the reading client, with a test, so a hostile or broken header cannot park a
  reading job for days.
- **Why later**: Outside this change's files; belongs to the document-reading area.

## Phase 5 checks (F2, F3) — done 2026-11-09

- **5.4**: After PR #46 deploys, run "kardiomiopatia", "serce Czarka" and one unrelated query on Azure at `0.37` (`0.70` returned nothing for every query);
  lower `searchMinSimilarity` if the expected documents disappear; record the result in `notes/threshold-calibration.md`.
- **5.5**: Measure one search after about 20 minutes of F1 idle time and record the cold-start latency in the
  calibration note and `context/foundation/infrastructure.md` (B1 trigger 2).
