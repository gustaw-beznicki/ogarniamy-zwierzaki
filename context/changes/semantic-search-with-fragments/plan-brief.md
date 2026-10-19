# Semantic Search with Fragments (S-04) — Plan Brief

> Full plan: `context/changes/semantic-search-with-fragments/plan.md`
> Research: `context/changes/semantic-search-with-fragments/research.md`

## What & Why

The north-star slice: an owner asks in ordinary words ("Czarek's heart") and gets back their own documents related
in meaning ("cardiomyopathy"), one card per document with the fragment that matched and a link to the original.
It tests the product's central claim that searching by meaning finds what exact-word search misses.

## Starting Point

S-03 already reads every stored document into per-page text (`document_text_pages`) through a crash-safe DB-queue
worker. There is no vector storage, no embedding service, no search endpoint, and `/search/` is a placeholder. The
PRD allowed content to leave the product only for text reading.

## Desired End State

The search screen returns the owner's related documents across all animals (inactive included), never another
account's, with an explicit empty state below the threshold and a note when some documents are not searchable yet.
Documents are embedded automatically after reading, after a re-read, after an animal rename, and once for the
existing archive.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Data leaving the product | Allowed for embeddings, EU only; PRD amended | Owner decision, consistent with the S-03 OCR exception | Plan |
| Model and residency | `text-embedding-3-large`, regional Standard in `swedencentral`, 1536 dims | Processing stays in Sweden and the best multilingual OpenAI model | Plan |
| Abuse monitoring | Accept EU-stored monitoring, record in PRD; modified monitoring parked | Unblocks S-04 before the 2026-11-04 deadline | Plan |
| Vector store | pgvector 0.8.2, exact cosine search, no ANN index | Perfect recall at thousands of chunks, no post-filter pitfall | Research |
| Pipeline | Embedding job queued in the reading publish transaction; DB-queue worker | Reuses the proven reading pattern, crash-safe | Research |
| Chunking | ~1,200 chars within a page, ~15% overlap, offsets stored | Short vet pages; page and offset make the fragment citable | Plan (external research) |
| Embedding input | Animal name + chunk text; rename re-embeds | Owner choice to match queries that name the animal | Plan |
| Ranking | Best chunk per document, one absolute threshold in config | PRD: one result per document, empty beats a false match | Plan |
| Threshold | Provisional 0.30, calibrated on ~20 real Polish queries | No public Polish benchmark; calibration decides | Plan |
| Transport | `POST /api/search` with antiforgery | Medical query words stay out of URLs, logs and history | Plan |
| Result card | Animal, event date, page, fragment, open original, details link, "not searchable yet" note | Matches the redesign; honest about documents in progress | Plan |
| S-04/S-05 boundary | Unchanged: filter and dedicated unavailable notice stay in S-05 | Keep the north star small; failures show a generic error | Plan |

## Scope

**In scope:** PRD amendment; Azure OpenAI account and deployment; pgvector allowlist; chunk/embedding tables, worker,
backfill and rename re-embedding; owner-scoped search API with isolation tests; search screen; deploy, calibration,
docs.

**Out of scope:** per-animal filter and unavailable notice (S-05); keyword/hybrid search, reranking, generated
answers; ANN index; document titles; boilerplate stripping; relative score cut; B1 move.

## Architecture / Approach

Reading publish → enqueue embedding job (same transaction) → `DocumentEmbeddingWorker` chunks pages, embeds
"name + chunk" via Azure OpenAI v1 REST with a managed-identity token → replaces `document_chunks` (vector(1536))
if reading generation and name are still current. `POST /api/search` embeds the query, runs one raw-SQL exact
cosine query restricted to stored documents owned through `animal_members`, takes each document's best chunk above
`Search:MinSimilarity`, and returns cards plus `notSearchableCount`. `SearchView` renders the redesign states.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. PRD and infrastructure | Amended PRD, OpenAI account/deployment, VECTOR allowlist, settings, role bootstrap | Region quota or token audience surprises |
| 2. Vector storage and pipeline | Tables, migration + backfill, chunker, client, worker, enqueue points, tests | Stale-work fencing and type mapping after `CREATE EXTENSION` |
| 3. Search API | `POST /api/search`, threshold, best fragment, isolation tests | Cross-account leak in raw SQL (FR-002) |
| 4. Search screen | `SearchView` with all states, i18n, API client | Accessibility of live status updates |
| 5. Deploy and calibrate | Activation, backfill, threshold, latency check, docs | Polish quality or F1 latency below target |

**Prerequisites:** owner can run the bootstrap role assignment; Azure OpenAI quota for `text-embedding-3-large`
Standard in `swedencentral`; Docker for tests.
**Estimated effort:** ~5–7 sessions across 5 phases, Phase 2 the largest.

## Open Risks & Assumptions

- Text-embedding-3 quality on Polish OCR text is unbenchmarked; calibration may show weak recall (fallback: another model in a later change).
- The animal-name prefix may lift unrelated documents of the named animal above the threshold; calibration checks it.
- Azure abuse-monitoring retention for embeddings is not documented precisely; accepted as EU-stored.
- Cold starts on F1 may exceed ~2 s; B1 trigger applies.

## Success Criteria (Summary)

- "serce Czarka" returns the cardiomyopathy document with a readable fragment and an openable original.
- A second account never sees the owner's documents or originals through search.
- Newly captured, re-read and renamed-animal documents become searchable without user action.
