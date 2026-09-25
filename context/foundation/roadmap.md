---
project: "Ogarniamy zwierzaki"
version: 1
status: draft                    # draft | active | locked
created: 2026-09-25
updated: 2026-09-25
prd_version: 1
main_goal: learn
top_blocker: time
milestone_id: first-searchable-archive
milestone_seq: 1
milestone_status: open           # open | done
---

# Roadmap: Ogarniamy zwierzaki

> Derived from `context/foundation/prd.md` (v1) + `tech-stack.md`, `infrastructure.md`, `shape-notes.md` (Forward: technical-roadmap) + auto-researched codebase baseline.
> Edit-in-place; archive when superseded.
> Slices below are listed in dependency order. The "At a glance" table is the index.

## Milestone

**M-1: First searchable archive** — Status: open

- **Intent:** An owner can sign in, archive a vet document straight from the phone, and later find it by meaning, not only by exact words, across all of their animals. This covers the full primary flow from the PRD's Success Criteria.
- **Source materials:** `context/foundation/prd.md` (v1)
- **Done when:** every F-NN and S-NN below is `done`.
- **Scope anchors:** FR-001 – FR-014 (all must-have), US-01, US-02, §Success Criteria (Primary + Guardrails), §Non-Functional Requirements, §Access Control.

## Vision recap

Owners of animals with years of treatment history keep vet documents on paper and in email, and cannot find the right page when the next appointment comes. Their questions name a topic and an animal at once ("Czarek's heart"), while the document says "cardiomyopathy". The product reads each document without anyone typing it in and answers such questions with the owner's own original documents and the fragment that matched. It never produces a generated medical answer.

## North star

**S-04: Owner types a natural-language question and gets back their related documents, each with the matching fragment and a link to the original.** This slice tests the product's central claim (that searching by meaning finds what exact-word search misses), and it makes the most use of the technology the owner wants to learn (embeddings, vector search).

> "North star" here means the smallest end-to-end slice whose delivery would prove the product works. It is placed as early as its Prerequisites allow, because everything else only matters if this works.

## At a glance

| ID   | Change ID                            | Outcome (user can …)                                                                 | Prerequisites | PRD refs                                    | Status   |
| ---- | ------------------------------------ | ------------------------------------------------------------------------------------ | ------------- | ------------------------------------------- | -------- |
| F-01 | azure-walking-skeleton               | (foundation) empty web app + API run on Azure, auto-deployed from main, budget alert | —             | §NFR (phone + desktop), §Guardrails         | ready    |
| F-02 | whole-app-ui-mockup                  | (foundation) low-fidelity mockup of every primary-flow screen, phone + desktop       | —             | §Success Criteria Primary, US-01, US-02, §NFR (phone + desktop) | ready    |
| S-01 | account-and-first-animal             | register, sign in, set up the first animal, and see only own data                    | F-01, F-02    | FR-001, FR-002, FR-003, §Access Control     | proposed |
| S-02 | capture-document-original            | photograph or upload a document, assign animal + date, reopen the private original   | S-01          | US-01, FR-006, FR-007, FR-008, §Guardrails  | proposed |
| S-03 | read-document-content                | have each stored document's content read in the background with nothing typed       | S-02          | US-01, FR-009, §NFR (privacy, original always openable) | proposed |
| S-04 | semantic-search-with-fragments       | ask in ordinary words and get related own documents with the matching fragment       | S-03          | US-02, FR-010, FR-013, FR-002, §Business Logic | proposed |
| S-05 | search-filter-and-unavailable-notice | narrow results to one animal; see an explicit notice when search is unavailable      | S-04          | US-02, FR-011, FR-012                       | proposed |
| S-06 | animal-profiles-and-history          | keep several animals, edit / mark inactive, see an animal's documents by event date  | S-02          | FR-004, FR-005, FR-014                      | proposed |

## Streams

Navigation aid that groups items sharing a Prerequisites chain. The dependency graph below remains the canonical order. This table is the suggested reading order across parallel tracks.

| Stream | Theme                 | Chain                                                        | Note                                                                                    |
| ------ | --------------------- | ------------------------------------------------------------ | --------------------------------------------------------------------------------------- |
| A      | Archive-and-search    | `F-01` → `S-01` → `S-02` → `S-03` → `S-04` → `S-05`          | Brings in the new technology (OCR, background processing, vector search) as early as it is needed. |
| B      | UI mockup             | `F-02`                                                       | Joins Stream A at `S-01`; runs alongside `F-01`. Screens are refined slice by slice.   |
| C      | Animal profiles       | `S-06`                                                       | Joins Stream A at `S-02`; runs alongside `S-03`–`S-05`.                                |

## Baseline

What's already in place in the codebase as of `2026-09-25` (auto-researched + user-confirmed).
Foundations below assume these are present and do NOT re-scaffold them.

- **Frontend:** partial — Astro 7.3 starter scaffold in `apps/web`, placeholder `src/pages/index.astro` only; no routing, layout or components.
- **Backend / API:** partial — ASP.NET Core 10 minimal-API template in `services/api/Program.cs`; only the sample `/weatherforecast` endpoint.
- **Data:** absent — no database driver, schema or migrations.
- **Auth:** absent — no identity provider, sessions or middleware.
- **Deploy / infra:** absent — no `.github/workflows`, no `infra/`; Bicep layout is planned in `infrastructure.md` only.
- **Observability:** absent — no logging/telemetry integration beyond framework defaults.

## Foundations

### F-01: Azure walking skeleton

- **Outcome:** (foundation) the empty web front end and API run on the Azure hosting target and are deployed automatically after each merge to main. A spending alert is in place against trial-credit expiry.
- **Change ID:** azure-walking-skeleton
- **PRD refs:** §Non-Functional Requirements (usable in current browsers on phone and desktop), §Success Criteria Guardrails; `shape-notes.md` Forward: technical-roadmap ("deploy to the hosting target in week one, before the pipeline is built").
- **Unlocks:** S-01, which is verified on the real hosting target rather than only locally. It also sets up the verification path used by every later slice: a smoke test on the deployed environment. It reduces the infrastructure risk "trial-credit expiry creates an unexpected bill".
- **Prerequisites:** Azure subscription with trial credits available (external state)
- **Parallel with:** F-02
- **Blockers:** —
- **Unknowns:**
  - App Service tier for the MVP: B1 has no staging slots, while Standard has slots but costs more. — Owner: user. Block: no.
- **Risk:** Sequenced first because the owner explicitly wants to deploy before the pipeline exists. The risk is letting it grow into full infrastructure up front. Database, private storage and queues belong in the slices that first need them (S-01, S-02, S-03).
- **Status:** ready

### F-02: Whole-app UI mockup

- **Outcome:** (foundation) a low-fidelity mockup of every screen on the primary flow exists as the shared visual reference for all slices, at both phone and desktop widths. The screens are sign-in/registration, first-animal onboarding, document capture, search with results and fragments, and the animal profile with its document list. No components are implemented.
- **Change ID:** whole-app-ui-mockup
- **PRD refs:** §Success Criteria Primary (steps 1–6), US-01, US-02, §Non-Functional Requirements (phone + desktop)
- **Unlocks:** S-01, S-02, S-04, S-05 and S-06 each take their screens from the mockup, refine them while being planned, and implement only the components their own scope needs. S-03's minimal "content read / reading failed" state is also taken from it.
- **Prerequisites:** —
- **Parallel with:** F-01
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Added at the owner's request (2026-09-25) to see the whole UI before building parts of it. Given the time pressure, the risk is polishing the mockup into a pixel-perfect design and delaying S-04. Keep it low-fidelity and do the detailed refinement inside each slice.
- **Status:** ready

## Slices

### S-01: Account and first animal

- **Outcome:** user can register, sign in, and set up their first animal (name only) on an onboarding screen, and sees only their own data.
- **Change ID:** account-and-first-animal
- **PRD refs:** FR-001, FR-002, FR-003, §Access Control, §Success Criteria Primary (steps 1–2)
- **Prerequisites:** F-01, F-02
- **Parallel with:** —
- **Blockers:** —
- **Unknowns:**
  - Sign-in approach: accounts managed by the API itself, or an external identity service. — Owner: user. Block: no (decided in `/10x-plan`).
- **Risk:** The first slice to bring in the data store and per-account isolation. From day one, the person–animal relationship carries a role (a binding requirement in §Access Control) so that sharing can be added cheaply later. It also turns the mockup's base layout into the app shell.
- **Status:** proposed

### S-02: Capture a document and keep the original

- **Outcome:** user can photograph a document or upload a PDF, assign it to an animal with one tap (the last-used animal is the default), confirm the event date (pre-filled with today), and later open the stored original. The original is never reachable without signing in.
- **Change ID:** capture-document-original
- **PRD refs:** US-01, FR-006, FR-007, FR-008, §Success Criteria Guardrails
- **Prerequisites:** S-01
- **Parallel with:** —
- **Blockers:** —
- **Unknowns:**
  - Size and format limits for phone-camera photos and emailed PDFs. — Owner: TBD. Block: no.
- **Risk:** Brings in private storage of originals as the system of record. Both guardrails (no public address, always retrievable) are proven here, before any reading of content exists that could obscure a failure.
- **Status:** proposed

### S-03: Read document content in the background

- **Outcome:** user can see that each stored document's content has been read in the background, from a photo, a scan or a PDF text layer, without typing anything. If reading fails, the user is told so and the original stays openable.
- **Change ID:** read-document-content
- **PRD refs:** US-01, FR-009, §Non-Functional Requirements (content leaves the product only while being processed and is never used for training; a stored original stays openable regardless)
- **Prerequisites:** S-02
- **Parallel with:** S-06
- **Blockers:** —
- **Unknowns:**
  - Which off-the-shelf text-reading (OCR) service and embedding service meet the no-retention, no-training requirement? — Owner: user. Block: no (researched in `/10x-plan`).
  - Does reading survive real phone photos of vet discharge summaries (handwriting, stamps, Latin abbreviations, poor lighting)? — Owner: user. Block: no. This slice exists to answer it.
- **Risk:** Sequenced right after capture because it tests the project's riskiest assumption: the belief that would sink the product if false, namely that text read from a phone photo is good enough to search. The learning goal also favours bringing OCR and background processing in early. Jobs must be safe to repeat after a restart.
- **Status:** proposed

### S-04: Search by meaning with fragments (north star)

- **Outcome:** user can type a question in ordinary words and get back their own documents that are related in meaning, across all their animals. Each result names its animal, shows the fragment that matched, and opens the original.
- **Change ID:** semantic-search-with-fragments
- **PRD refs:** US-02, FR-010, FR-013, FR-002, §Business Logic
- **Prerequisites:** S-03
- **Parallel with:** S-06
- **Blockers:** —
- **Unknowns:**
  - What is the similarity threshold, and how is it set? — Owner: user. Block: no. Start with a provisional value and tune it against a real archive (Open Roadmap Question 1).
- **Risk:** The north star. It carries the first test written from the user's perspective, from typing a query to opening the original (`shape-notes.md` Forward). The key risk is a vector query returning another account's fragment (FR-002), which is tested explicitly.
- **Status:** proposed

### S-05: Narrow to one animal and the "search unavailable" notice

- **Outcome:** user can narrow search results to one animal, and sees an explicit notice instead of quietly worse results when content search is temporarily unavailable.
- **Change ID:** search-filter-and-unavailable-notice
- **PRD refs:** US-02, FR-011, FR-012
- **Prerequisites:** S-04
- **Parallel with:** S-06
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Refines the north star and is sequenced after it so it cannot delay validation. The notice must not bring back any exact-word fallback (FR-012 removed keyword search on purpose).
- **Status:** proposed

### S-06: Animal profiles and document history

- **Outcome:** user can keep several animals, edit an animal's profile or mark it inactive, and see each animal's documents on its profile, ordered by event date with the newest first.
- **Change ID:** animal-profiles-and-history
- **PRD refs:** FR-004, FR-005, FR-014
- **Prerequisites:** S-02
- **Parallel with:** S-03, S-04, S-05
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Independent of the search pipeline, so a separate agent run can take it in parallel. An inactive animal drops off the main list and out of the capture default but stays searchable. The one-tap selector from S-02 only becomes meaningful once there is a second animal. The ordering depends on dates that FR-008 admits may be wrong (Open Roadmap Question 2).
- **Status:** proposed

## Backlog Handoff

Mirrored in Linear project "Ogarniamy zwierzaki" (milestone "M-1: First searchable archive") on 2026-09-25; the Linear issue key is in Notes. The roadmap stays canonical.

| Roadmap ID | Change ID                            | Suggested issue title                                        | Ready for `/10x-plan` | Notes |
| ---------- | ------------------------------------ | ------------------------------------------------------------ | --------------------- | ----- |
| F-01       | azure-walking-skeleton               | Deploy empty web + API to Azure from main, with budget alert | yes                   | Run `/10x-plan azure-walking-skeleton`; Linear 10X-5 |
| F-02       | whole-app-ui-mockup                  | Low-fidelity mockup of all primary-flow screens              | yes                   | Design work: mockups via `/design`; no code; Linear 10X-6 |
| S-01       | account-and-first-animal             | Registration, sign-in and first-animal onboarding            | no                    | Waits on F-01, F-02; Linear 10X-7 |
| S-02       | capture-document-original            | Capture photo/PDF, assign animal + date, private original    | no                    | Waits on S-01; Linear 10X-8 |
| S-03       | read-document-content                | Background reading (OCR / text layer) of stored documents    | no                    | Waits on S-02; Linear 10X-9 |
| S-04       | semantic-search-with-fragments       | Search by meaning with matching fragment (north star)        | no                    | Waits on S-03; Linear 10X-10 |
| S-05       | search-filter-and-unavailable-notice | Per-animal filter and "search unavailable" notice            | no                    | Waits on S-04; Linear 10X-11 |
| S-06       | animal-profiles-and-history          | Multiple animals, edit/inactive, profile document list       | no                    | Waits on S-02; Linear 10X-12 |

## Open Roadmap Questions

1. **What is the similarity threshold, and how is it set?** It can only be tuned against a real archive, and one global threshold may stop fitting everyone as the user count grows. — Owner: user. Block: no (S-04 starts with a provisional value).
2. **Bulk-loading the old binder will produce wrong event dates.** FR-008 keeps today's date as the default and accepts this risk deliberately. The consequence lands on S-06's ordering. — Owner: user. Block: no; revisit if the secondary success criterion is pursued.
3. **Is the premise that owners now want to keep their animals' medical records sound?** Recorded as the user's premise, not a verified finding. — Owner: user. Block: no.
4. **When is the secondary success criterion (several dozen of Czarek's and Sonia's real documents uploaded and searchable) exercised?** It is the real-volume check for S-03 and S-04 and the input for tuning Question 1. — Owner: user. Block: no; roadmap-wide.
5. **How is the F-02 mockup kept current as slices refine their screens?** It could be updated per slice, or treated as a starting point only. — Owner: user. Block: no; roadmap-wide.

## Parked

- **Sharing an animal between caretakers, caretaker role, expiring access** — Why parked: PRD §Non-Goals. Deferred to a later step; S-01 keeps the ownership data shape that makes it cheap.
- **Medication plans and dose logs** — Why parked: PRD §Non-Goals.
- **Document type field or automatic type suggestion** — Why parked: PRD §Non-Goals (the product does not guess).
- **Deleting documents or animal profiles** — Why parked: PRD §Non-Goals; protects the "original always retrievable" guardrail.
- **Cost optimisation** — Why parked: PRD §Non-Goals (non-functional); only the F-01 budget alert remains.
- **Notifications and reminders** — Why parked: PRD §Non-Goals (non-functional).
- **Offline operation** — Why parked: PRD §Non-Goals (non-functional).
- **Voice parsing, medication stock tracking, extraction-review queues with confidence scores** — Why parked: PRD §Non-Goals, carried over from the seed note.
- **Keyword (exact-word) search, including as a fallback** — Why parked: PRD FR-010 / FR-012 resolution; the MVP is purely semantic.
- **Event-date-range filter** — Why parked: PRD FR-011 resolution; it would rest on unreliable dates.

## Milestone History

## Done
