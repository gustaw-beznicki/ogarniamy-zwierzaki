---
project: "Ogarniamy zwierzaki"
context_type: greenfield
created: 2026-09-15
updated: 2026-09-15
product_type: web-app
target_scale:
  users: medium
  qps: low
  data_volume: small
timeline_budget:
  mvp_weeks: 7
  hard_deadline: 2026-11-04
  after_hours_only: true
checkpoint:
  current_phase: 8
  phases_completed: [1, 2, 3, 4, 5, 6, 7]
  gray_areas_resolved:
    - topic: "pain category"
      decision: "data trapped (unsearchable archive) + workflow friction on capture + pre-visit decision paralysis"
    - topic: "decision paralysis vs. no-medical-answer rule"
      decision: "paralysis is resolved by completeness of the result set, never by relevance ranking; product does not judge medical importance"
    - topic: "moment served by MVP"
      decision: "two moments: preparing for the next vet visit, and archiving right after a visit"
    - topic: "primary persona scope"
      decision: "market is any pet owner; MVP persona is narrowed to an owner whose animal has an accumulated treatment history"
    - topic: "insight vs. status quo"
      decision: "search by meaning not filename; event date is not upload date; 'why has nobody built this' resolved in phase 7 and folded into Vision"
    - topic: "auth model"
      decision: "one account per person, login required; each person sees only what they have access to"
    - topic: "sharing and roles"
      decision: "removed from the shape: a stated MVP non-goal with the work scheduled for a later step; only the pet<->caretaker-with-role data shape is kept, since it costs nothing now and keeps that later step migration-free"
    - topic: "attaching a document to an animal without typing"
      decision: "one tap at capture time, defaulting to the last-used animal; no inference from document content"
    - topic: "event-date extraction"
      decision: "cut from MVP; replaced by a single editable date field pre-filled with today's date"
    - topic: "timeline"
      decision: "user committed to sustained after-hours work for 7 weeks rather than scoping down further; weekly budget later raised to 12-15 h with scope deliberately held flat; hard deadline 2026-11-04"
    - topic: "pet age vs. date of birth"
      decision: "moot - species and date of birth were both cut from the profile in the Socratic round; only the name remains"
    - topic: "document type"
      decision: "cut from MVP together with the type filter; automatic type suggestion lost its consumer and moved to Non-Goals"
    - topic: "search shape"
      decision: "keyword search removed entirely; MVP is purely semantic, with an explicit unavailable notice instead of a silent fallback"
    - topic: "deletion"
      decision: "nothing is deleted in the MVP; an animal can be marked inactive instead"
    - topic: "result unit"
      decision: "the document is the unit; the best-matching fragment is shown as justification"
    - topic: "no-match behaviour"
      decision: "a similarity threshold, below which nothing is returned - an empty result beats a false match in a medical archive"
    - topic: "document content and external processing"
      decision: "content may leave the boundary only for the duration of the processing that reads it, retained nowhere afterwards, never used for training"
    - topic: "sharing timing"
      decision: "later step, after the core proves itself (OCR check, threshold tuning); moved into Non-Goals so it cannot creep back without an explicit decision"
    - topic: "why nobody built this"
      decision: "records fragmented across clinics with no incentive to share; pet apps chase the healthy-animal mass market; and a claimed shift in how owners treat pets - the last part recorded as the user's premise, not verified fact"
  frs_drafted: 14
  quality_check_status: accepted
---

# Shape notes

Seed input: user-supplied scope note (2026-09-15), captured verbatim below.

## Seed note (verbatim)

Kontekst: nowa, samodzielna aplikacja. Termin 4.11.2026, 4–10 h/tydz.

Rdzeń
- Profile zwierząt — nazwa wymagana, gatunek opcjonalny
- Wgrywanie PDF/zdjęć, oryginał w prywatnym Blob Storage jako źródło prawdy
- Ekstrakcja tekstu: PDF-y z warstwą tekstową od razu, skany przez gotowe API OCR
- Chunkowanie + embeddingi w pgvector
- Wyszukiwanie hybrydowe: słowa kluczowe i zapytanie naturalne, filtry po zwierzaku, typie i dacie
- Wynik pokazuje pasujący fragment i otwiera oryginał
- Wyszukiwanie dokładne działa, gdy embeddingi są niedostępne lub się przebudowują
- Data zdarzenia z dokumentu nie jest datą wgrania; oś czasu i „ostatnie badanie X" liczą się z niej
- Logowanie, wdrożenie na Azure w pierwszym tygodniu, dokumenty kontekstowe, jeden test z perspektywy użytkownika

Miłe, jeśli zostanie czas
- Plany leków i log podań — bez współbieżności i widoku Dziś
- Automatyczna propozycja typu dokumentu

Poza zakresem
- CocoIndex, kolejki review i confidence, zapasy, parsowanie głosu, powiadomienia, wielotenantowość, link dla opiekuna, koszty

Reguły produktu
- Wyszukiwarka zwraca dokumenty i fragmenty, nigdy wygenerowanej odpowiedzi medycznej
- Usunięcie dokumentu kasuje też tekst, chunki i embeddingi
- Gatunek niczego nie wnioskuje

## Vision & Problem Statement

An owner comes home from the vet with a discharge summary — on paper, or in an email — and files
it away. The archive grows for years. When a specific page is needed again, finding it "borders on
a miracle": the binder has no index and the mailbox has no vocabulary for what the document is
about. The real cost is not lost paper; it is walking into the next appointment with nothing and
reconstructing the animal's treatment history from memory. Three pains stack on top of each other:
the archive is unsearchable, capturing a document costs enough effort that some never get filed at
all, and the owner cannot tell what is worth bringing to the next visit.

The insight is in the shape of the questions the owner actually asks: "visits related to Czarek's
heart", "Sonia's blood counts". These are not filename lookups — they name a topic and an animal at
once, and the document may say "cardiomyopathy" and "murmur" where the query says "heart". A drive
with OCR can only match the words literally present. A pet-health app asks the owner to transcribe
the document into structured fields, which nobody sustains for three years. Second half of the
insight: every file system orders by upload date, but an owner who digitises half a binder in one
evening destroys the treatment chronology in the process — the ordering that matters is the date of
the event described inside the document.

Why the gap has stayed open, per the user, has three parts. The records are fragmented across
clinics, each of which sees only its own slice and has no reason to hand data over — the only party
who wants the cross-clinic, multi-year view is the owner, and the owner has no leverage. The
existing pet apps chase the mass-market owner of a healthy animal, because that is where the numbers
are, which leaves the narrow group with years of treatment history unserved. And until recently
private individuals simply did not care much about keeping their animals' medical records at all —
the user's read is that this is changing as pets move into the place families used to reserve for
children. That last part is a market premise the user holds, not a verified finding; it is recorded
as their reasoning, not as established fact.

## User & Persona

Primary persona: an owner whose animal has an accumulated treatment history — a chronic condition
or several years of visits — so the archive has passed the size at which scanning it by eye still
works. That threshold is what justifies the whole search pipeline; an owner of a healthy animal with
two documents does not have this problem.

Context: the owner manages the archive alone, across more than one animal (the user's own case:
Czarek and Sonia), with documents arriving in two formats — paper handed over at the clinic, and
email from the vet.

The moments they reach for the product:
1. **Preparing for the next visit** — at home, evening before, assembling everything on one topic
   for one animal. Tolerates keyboard-typed queries and a few seconds of latency.
2. **Right after a visit** — archiving the document just received. Value here is measured in how
   few steps it takes to get from a photo to a stored document.

Addressable market, per the user, is any pet owner; the MVP is designed for the persona above.

> Socrates: challenged on "everyone" as a persona — market and persona were separated. Market stays
> broad; MVP persona narrowed to owners with an accumulated treatment history.

> Socrates: challenged that "pre-visit decision paralysis" contradicts the user's own rule that the
> product never produces a generated medical answer. Resolution: paralysis is addressed by the
> *completeness* of the returned set ("did I miss anything on this topic"), never by ranking medical
> relevance. Judgement stays with the owner and the vet.

## Access Control

Login is required; there is one account per person. A person sees only their own animals and only
the documents belonging to those animals — in lists and in search results alike.

In the MVP there is exactly one role in effect: **owner**, and an animal belongs to exactly one
account. The owner can create and edit animals, mark one inactive, upload documents, and search
them. Nothing is deleted (see Non-Goals).

Sharing an animal between several caretakers is **out of scope for the MVP** and is listed as a
non-goal. It is planned work for a later step, not an open edge of this shape. What that later step
would add — a caretaker who holds their own account, a read-only caretaker role with no delete and
no edit, and caretaker access granted for a limited period that expires on its own — is recorded
here only so the later step has somewhere to start. None of it ships now: no invitation flow, no
role-selection UI, no time-based expiry, and no second role to enforce anywhere.

One thing remains binding from day one, and it is a data-shape decision rather than a feature:
ownership of a document is expressed through the animal, and the relationship between a person and
an animal carries a role. That costs nothing today and is what keeps the later step cheap — adding
caretakers will not require re-assigning ownership of every stored document.

> Socrates: the smallest access model that still makes the MVP useful is a single gated account.
> The user chose one-account-per-person over that, because the addressable market is any pet owner
> and a single-tenant build would have to be undone immediately.

> Socrates: time-expiring access was challenged as the only state in the product that changes
> without a user action — it must be enforced on every read and is the hardest thing in the MVP to
> test. Resolution: first deferred, then removed outright. Sharing is a non-goal for the MVP and
> scheduled as later work; only the data shape that makes it cheap remains.

## Success Criteria

### Primary

- The smallest complete flow runs end to end, in this order:
  1. The owner registers and signs in.
  2. An onboarding screen sets up the first animal — name (required), species, date of birth —
     without the owner having to hunt for it in the app.
  3. The owner photographs a document straight after a vet visit, one tap selects the animal
     (defaulting to the last-used one), and the event date is pre-filled with today and editable.
  4. The document is stored, its content becomes searchable, and the original is kept as the
     system of record.
  5. Later, the owner types a natural-language query — e.g. "rabies vaccination" — and gets back the
     matching documents.
  6. Each result shows the matching fragment and opens the original.

  Four distinct user actions stand between an empty app and visible value.

### Secondary

- A real archive fits inside: several dozen of Czarek's and Sonia's documents are uploaded and
  searchable, proving the pipeline survives real volume rather than three test files.

### Guardrails

- An uploaded original is never reachable without signing in — no address that works when pasted
  into a browser.
- An uploaded original is always retrievable. Reading its content may fail and search may be
  temporarily unavailable; the archive must never lose a page the owner has already thrown out of
  the binder.

## Timeline acknowledgment

Budget arithmetic at the time of the decision: 2026-09-15 to the hard deadline of 2026-11-04 is
~7.1 weeks. The costed core estimate is 35-60 h after cutting event-date extraction.

Acknowledged on 2026-09-15: a 7-week MVP requires sustained dedication across evenings and weekends,
including stretches where the pipeline is not yet joined up and progress is invisible; user
accepted.

The user subsequently raised the weekly budget from 4-10 h to 12-15 h (85-107 h total) and
deliberately did **not** raise scope. The surplus is buffer, not room for features. Deferred items —
event-date extraction, caretaker roles, medication plans, automatic document-type suggestion — are a
stretch list, pulled in only if the core demonstrably works with time to spare.

> Socrates: challenged that "12-15 h/week after hours" is ~2 hours every single day for seven weeks
> including weekends, and that the original 4-10 h estimate was likely the realistic one. Resolution:
> budget raised, scope held flat, surplus reserved for the integration that overruns.

> Socrates: challenged that cutting event-date extraction discards one of the two insights captured
> in Phase 1. Resolution: it does not — the manual date field preserves the rule that a document's
> event date is distinct from its upload date. Only the convenience of auto-filling it was cut.

## Functional Requirements

All 14 requirements are must-have. The Socratic round removed every nice-to-have from this list:
medication plans and automatic document-type suggestion were both moved to Non-Goals rather than
kept as deferred requirements.

### Accounts and access

- FR-001: Owner can create an account and sign in. Priority: must-have
  > Socrates: Counter-argument considered: "auth is 3-6 h that proves nothing about whether semantic
  > search over vet documents works at all, so it should be deferred." Resolution: rejected — the
  > guardrail says an original must never be reachable without signing in, so dropping this breaks a
  > guardrail on day one. Stands as written.

- FR-002: Owner can see only their own data — including in semantic search results, which never
  reach documents belonging to another account. Priority: must-have
  > Socrates: Counter-argument considered: "as written this is too weak — the real risk is not the
  > document list but a vector query that searches every account's embeddings and returns a fragment
  > of someone else's document." Resolution: accepted; the requirement was rewritten to make search
  > isolation explicit rather than implied.

### Animal profiles

- FR-003: Owner can create an animal profile during onboarding; only the name is required.
  Priority: must-have
  > Socrates: Counter-argument considered: "species and date of birth serve nothing — the user's own
  > rule says species infers nothing, and neither field affects search or ordering. They are data
  > collected just in case." Resolution: accepted; both fields cut. Onboarding is a single field.

- FR-004: Owner can keep several animals at once. Priority: must-have
  > Socrates: Counter-argument considered: "multi-animal support is the author's own situation, not
  > an MVP need; cutting it also removes the per-animal filter and the tap-to-assign step — three
  > requirements for a few hours saved." Resolution: rejected — two animals were in the product
  > rationale from Phase 1, and half of the user's own example queries name an animal.

- FR-005: Owner can edit an animal profile and mark it inactive; deletion does not exist in the MVP.
  Priority: must-have
  > Socrates: Counter-argument considered: "deleting an animal is an undefined bomb — nothing says
  > what happens to its documents, and it is the one action that can wipe years of archive."
  > Resolution: accepted. With document deletion cut from the MVP (see Business Logic), animal
  > deletion was replaced by an inactive flag: the animal drops off the main list and the capture
  > default, the archive stays intact and searchable.

### Capture

- FR-006: Owner can add a document by photographing it or by uploading a PDF. Priority: must-have
  > Socrates: Counter-argument considered: "two input paths mean two sets of failure modes, and the
  > described first flow is photo-only." Resolution: no counter-argument accepted — documents arrive
  > both on paper and by email, so cutting either halves the source.

- FR-007: Owner can assign a document to an animal with a single tap, defaulting to the last-used
  animal. Priority: must-have
  > Socrates: Counter-argument considered: "the last-used default is a silent error — upload five of
  > Czarek's documents, the sixth is Sonia's, and it disappears from her history forever."
  > Resolution: no counter-argument accepted; one tap is the smallest cost at which the per-animal
  > filter works at all.

- FR-008: Owner can set the document's event date, pre-filled with today's date and editable.
  Priority: must-have
  > Socrates: Counter-argument considered: "nobody will correct a pre-filled field — you get data
  > that looks trustworthy and lies, which is worse than no field, because the ordering will look
  > credible while being wrong." Resolution: risk accepted deliberately. The default is correct for
  > the primary moment (archiving right after a visit) and wrong for bulk-loading an old binder.
  > Recorded as a known limitation; it propagated into FR-011 and FR-014 below.

### Searchability

- FR-009: Owner can search the content of a document without transcribing anything — both a PDF
  carrying a text layer and a scan or photo. Priority: must-have
  > Socrates: Counter-argument considered: "nobody has verified that OCR survives a phone photo of a
  > vet discharge summary — handwriting, stamps, Latin abbreviations, poor lighting. If OCR returns
  > noise, embeddings are computed from noise and semantic search fails while every component
  > reports success." Resolution: no counter-argument accepted — this is the product. The risk is
  > acknowledged and recorded as the project's largest untested assumption.

### Search

- FR-010: Owner can ask a natural-language question and get semantically related documents across
  all of their animals. Priority: must-have
  > Socrates: Counter-argument considered: "semantic search alone is enough — the whole Phase 1
  > insight was searching by meaning, and keyword search is the Ctrl+F that already lost. Keyword
  > search is in scope because that is how it is normally done, not because it solves the problem."
  > Resolution: accepted. Keyword search was first demoted to a fallback mode, then removed entirely
  > when FR-012 was resolved. The MVP is purely semantic.

- FR-011: Owner can narrow results to a chosen animal. Priority: must-have
  > Socrates: Counter-argument considered: "at a few dozen documents across two animals, filters are
  > unnecessary — and if semantic search cannot handle that, filters mask a weak core instead of
  > fixing it." Resolution: partially accepted. The per-animal filter stays, because it follows
  > directly from the user's own example queries. The event-date-range filter was cut, because it
  > would rest on the dates FR-008 admits may be noise.

- FR-012: Owner sees an explicit notice when content search is temporarily unavailable, instead of
  quietly worse results. Priority: must-have
  > Socrates: Counter-argument considered: "a silent fallback is worse than an error — the user gets
  > keyword results, believes they are seeing semantic search, and concludes the product is bad. An
  > honest 'the index is rebuilding, try again shortly' is cheaper and truer." Resolution: accepted
  > in full. The keyword fallback from the seed note was dropped and replaced by this notice. Knock-on
  > effect: there is now no keyword search anywhere in the MVP.

- FR-013: Owner sees the matching fragment in a result and can open the original. Priority: must-have
  > Socrates: Counter-argument considered: "an OCR fragment can be unreadable out of context, so
  > showing it to justify a result may actively mislead." Resolution: no counter-argument accepted —
  > the fragment is the evidence that search understood the query; without it the owner cannot tell
  > why a document surfaced.

### Animal profile

- FR-014: Owner can see an animal's documents on its profile, ordered by event date, newest first.
  Priority: must-have
  > Socrates: Counter-argument considered: "this rests on exactly the dates you just refused to trust
  > when cutting the date filter — and presenting noise as a treatment chronology is a worse use of
  > those dates than filtering by them." Resolution: accepted in part. This is now default ordering
  > on the profile, not a "timeline" feature. The promise of a reliable treatment chronology is
  > withdrawn; what remains is a second route to a document when the owner cannot think of the right
  > search term.

## User Stories

### US-01: Owner archives a document straight after a vet visit

- **Given** a signed-in owner with at least one animal profile
- **When** they photograph the discharge summary they were just handed, tap the animal it belongs to,
  and confirm the date
- **Then** the original is stored, its content becomes searchable, and the document appears on
  that animal's profile

#### Acceptance Criteria
- The animal selector defaults to the last-used animal; changing it takes one tap
- The event date is pre-filled with today's date and can be changed before saving
- Saving does not require the owner to type anything
- The original remains openable even if its content cannot be read
- A PDF received by email follows the same path and lands in the same place

### US-02: Owner assembles everything on one topic before the next visit

- **Given** a signed-in owner whose archive holds documents for more than one animal
- **When** they type a natural-language query such as "rabies vaccination" or "Czarek's heart"
- **Then** they get back the semantically related documents across all of their animals, each showing
  the fragment that matched

#### Acceptance Criteria
- Results match on meaning, not only on words literally present — a query for "heart" surfaces a
  document that says "cardiomyopathy"
- Each result names the animal it belongs to
- Results can be narrowed to one animal
- Opening a result opens the stored original
- Results never include a document belonging to another account
- The result set is documents and fragments only — never a generated sentence about the animal's
  health
- When content search is unavailable, the owner is told so explicitly rather than shown
  degraded results

## Business Logic

**Given a question asked in ordinary language, the product returns the complete set of the owner's
own documents whose content is related in meaning to that question above a fixed similarity
threshold — each accompanied by the fragment that justifies the match — and never returns a sentence
about the animal's health that it did not read out of a document.**

What the rule consumes: the question as the owner types it, in ordinary words rather than the
vocabulary of the document; the content of every document the owner has stored, as read from the
document itself with nothing typed in by hand; and, optionally, one animal the owner chose to narrow
to.

What it produces: a list whose unit is the document, not the paragraph. A long discharge summary
appears once, carrying the single fragment that best explains why it surfaced. Below the similarity
threshold nothing is returned at all — an empty result is the correct answer when the archive holds
nothing on the topic, because in a medical archive a false match is expensive: the owner concludes
that is everything they have and walks into the appointment incomplete.

Where the owner meets it: the search screen, typing the way they actually think — "visits about
Czarek's heart", "Sonia's blood counts" — and, in the other direction, the animal's profile, where
the same documents are listed by event date for the times when no search term comes to mind.

### Product rules carried over from the seed note

- **A generated medical answer is never returned.** Results are source documents and the fragments
  inside them. This rule survived the Socratic round intact and is now part of the one-sentence rule
  above.
- *(Moot)* "Deleting a document also removes everything derived from its content" — document
  deletion was cut from the MVP in Phase 4, so the rule has nothing to apply to. It returns when
  deletion does.
- *(Moot)* "Species infers nothing" — species was cut from the animal profile in Phase 4, so there is
  nothing to infer from. The intent behind the rule — the product does not guess — survives, and
  was the reason automatic document-type suggestion was rejected too.

## Non-Functional Requirements

- A search returns its result in under about two seconds as the owner perceives it, and shows
  continuous visible progress whenever it takes longer.
- Document content leaves the product's boundary only for the duration of the processing that reads
  it, and is not retained anywhere outside the product once that processing completes. It is never
  used to train anything.
- The product is usable in current browsers on both a phone and a desktop — capture happens on the
  phone right after a visit, assembly happens wherever the owner sits the evening before the next
  one.
- A stored original stays openable regardless of whether its content could be read or search is
  currently available.

## Non-Goals

Functional:

- **No sharing an animal between caretakers, no caretaker role, no expiring access.** The MVP has one
  role — the owner — and an animal belongs to exactly one account. This is deferred work rather than
  a permanent exclusion: it is planned for a later step, and the ownership shape chosen now is what
  keeps that step cheap. Closing it now matters because it is the item the user reached back
  toward once mid-session, and it is the only piece of scope that could grow without anything else
  forcing the issue.
- **No medication plans or dose logs.** The archive is about the past and about retrieval; a
  medication plan is about the future and about routine. They share only the animal.
- **No document type, and nothing that suggests one.** Semantic search finds "vaccinations" from the
  content, so a separate type field is largely redundant — and automatic typing would be the product
  guessing, which the owner explicitly did not want.
- **No deletion of documents or animal profiles.** Nothing is destroyed in the MVP; an animal can be
  marked inactive. This protects the guardrail that an original is always retrievable from quiet
  erosion.

Non-functional:

- **No cost optimisation.** Reading and indexing a document costs money per document, but at this
  user count cost is not a dimension being optimised, and saying so prevents tuning it in week six.
- **No notifications or reminders.** The owner comes to the product when they have a reason to. This
  is accepted knowingly: a product with no trigger is visited rarely.
- **No offline operation.** Capture happens in a clinic where signal can be poor, and it will simply
  not work there. Written down before it is discovered.
- **No voice parsing, no medication stock tracking, no extraction-review queues with confidence
  scores.** Carried over from the seed note's out-of-scope list.

## Open Questions

1. **What is the similarity threshold, and how is it set?** — the business rule turns on it, but it
   can only be tuned against a real archive. Noted during Phase 6: one global threshold stops fitting
   everyone as the user count grows, and it is the first parameter someone will want to fix.
   Owner: user. By: whenever the archive is large enough to tune against.
2. **Bulk-loading the old binder will produce wrong event dates.** — FR-008 keeps today's date as the
   default and the risk was accepted deliberately. No mitigation was chosen, and the consequence
   lands on FR-014's ordering. Owner: user. Not blocking; revisit if the secondary success criterion
   (whole binder inside) is pursued.
3. **Is the premise that owners now want to keep their animals' medical records sound?** — the user's
   reasoning in Phase 1 rests partly on a claimed shift in how people treat pets. Recorded as the
   user's premise, not as a verified finding. Owner: user. Not blocking.

### Closed during the closing cross-check

- *Why has nobody built this?* — resolved in Phase 7; the three-part answer is now in Vision &
  Problem Statement.
- *When do caretaker sharing and roles land?* — resolved twice. First: not until the core has proven
  itself, meaning the OCR check below and the threshold question above are settled. Then the user
  went further and removed it from the shape altogether — it is now a stated non-goal for the MVP
  with the work scheduled for a later step, rather than an open edge. The data model is already
  waiting.
- *Does OCR survive a phone photo of a vet discharge summary?* — converted from an open question into
  a planned first step, recorded below.

## Step zero (before anything else is built)

Run the OCR check on five real photographs from the owner's own binder — handwriting, stamps, Latin
abbreviations, ordinary lighting — and read the text that comes back.

This precedes building the rest. FR-009 carries the whole product, and if the extracted text is noise
then embeddings are computed from noise and semantic search fails while every component reports
success. It is an afternoon's work and it is the only cheap moment to find out.

## Quality cross-check

Run 2026-09-15. All six greenfield gate items present; status `accepted`.

| Item | Result |
| --- | --- |
| Access Control | present — one account per person, single owner role, model prepared for caretakers |
| Business Logic | present — one-sentence rule with threshold and constraint |
| Project artifacts | present — shape-notes.md with a complete checkpoint |
| Timeline-cost acknowledged | present — 7 weeks exceeds the 3-week default; acknowledgment block recorded |
| Non-Goals | present — 4 functional, 4 non-functional |
| Preserved behavior | n/a (greenfield) |

No gaps to mirror into the PRD's Open Questions. Three risks that are not gate failures but were
surfaced at the gate and belong in front of anyone reading the PRD:

1. The product now rests on a single mechanism. The Socratic round removed keyword search and the
   fallback, leaving semantic search as the only route to a document apart from the animal's profile
   listing. Cleaner product, no plan B.
2. The OCR assumption under FR-009 is untested and carries everything. It is now Step zero above.
3. *(Closed after the cross-check.)* Caretaker sharing was initially left out of Non-Goals, leaving
   the shape's one open edge. The user then removed it outright: it is a stated non-goal for the MVP
   with the work scheduled for a later step. Only the data shape that keeps that step cheap remains.
   No scope in this shape can now grow without an explicit decision.

## Forward: technical-roadmap

Volunteered by the user, outside the PRD schema. Handed to the planning step downstream.

- Deploy to the hosting target in week one, before the pipeline is built.
- Keep context documents alongside the code.
- At least one test written from the user's perspective.

## Forward: tech-stack

Volunteered by the user in the seed note. NOT part of the PRD schema — handed to the
tech-stack-selection step after `/10x-prd`.

- Private Blob Storage as the system of record for uploaded originals
- pgvector for chunk embeddings
- Off-the-shelf OCR API for scanned documents (no self-hosted OCR)
- Azure as deployment target, deployed in week 1
- Explicit avoid: CocoIndex
