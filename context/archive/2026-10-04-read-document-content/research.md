---
date: 2026-10-04T12:41:03+02:00
researcher: Codex
git_commit: 8a4887c081d510295b9c50b60a25d0eb6b54d03c
branch: capture-document-original-p1
repository: 10xdevs
topic: "S-03 integration points for durable background document reading"
tags: [research, codebase, documents, background-processing]
status: complete
last_updated: 2026-10-04
last_updated_by: Codex
last_updated_note: "Owner resolved provider privacy, PDF routing and retry behavior during planning."
---

# Research: Read document content

## Research Question

Where should S-03 attach durable reading of stored PDFs/photos, how can originals remain independently retrievable, and which boundaries allow implementation alongside S-06?

## Summary

The inspected upload completion path already has a PostgreSQL transaction suitable for scheduling durable work (`services/api/Documents/OwnedDocuments.cs:181`). Processing requires new state, extraction adapters and a worker; the inspected host registers capture/storage services but no background processing service (`services/api/Program.cs:87`). Original access depends on ownership and storage completion, not text extraction (`services/api/Documents/OwnedDocuments.cs:80`).

S-02 remains in progress in `context/foundation/roadmap.md`; its manual acceptance and Phase 5 checks are prerequisites for releasing dependent work. Code evidence here is the local HEAD, rather than proof of production rollout.

## Detailed Findings

### Scheduling and restart safety

- Completion reconciles Blob receipts before marking a document stored (`services/api/Documents/DocumentCaptureService.cs:133`). Its repository transaction locks the document, updates storage state/upload time and serializes the account preference (`services/api/Documents/OwnedDocuments.cs:181`). A job inserted within that transaction would avoid a commit/enqueue gap; this is a proposed integration, not existing behavior.
- Repeated completion returns the stored resource without repeating preference mutation on this inspected path. Tests compare repeated completion responses (`services/api.Tests/DocumentRecoveryTests.cs:88`). Keep processing status out of the stable completion response; use an owner-scoped status endpoint instead.
- Existing stored documents need restart-safe, unique job backfill. Uploading rows must be excluded, matching the stored visibility predicate (`services/api/Documents/OwnedDocuments.cs:286`).
- Proposed durable PostgreSQL jobs need a claim token, lease expiry, due time, bounded attempts and conditional publication. Network calls must run outside claim transactions; stale lease holders must not publish after another worker claims the job.

### Text and provenance

- The capture validator accepts one PDF or up to ten ordered JPEG/PNG files, each within its byte limit (`services/api/Documents/DocumentCaptureService.cs:258`, `services/api/Documents/DocumentFile.cs:9`). A byte-size limit does not establish a PDF page-count bound.
- Store extracted text with file ID, image position and PDF page number. `DocumentPage` currently names a pagination DTO (`services/api/Documents/DocumentPage.cs:3`), so a text-page entity needs a distinct name.
- Original storage exposes immutable writes and read-only streams (`services/api/Storage/IOriginalStorage.cs:3`). Processing should use server-resolved keys, leave original bytes unchanged and publish a complete text revision atomically.

### Authorization and UI

- Owner HTTP queries reach documents through animal memberships with the owner role (`services/api/Documents/OwnedDocuments.cs:279`). A system worker needs a separate documented repository boundary; it must not expose unscoped reads to HTTP callers.
- `DocumentDetails.tsx:27` fetches the original detail once. A separate status component with bounded polling can display queued/reading/read/failed states without changing stable capture DTOs.
- New retry mutations, if chosen, need the antiforgery pattern used by `services/api/Documents/DocumentEndpoints.cs:24`. Foreign and missing documents should keep the existing indistinguishable 404 behavior.

### Deployment and tests

- The checked-in environment defaults to F1 (`infra/environments/mvp.bicepparam:5`), and its App Service definition disables Always On for F1 (`infra/modules/app-service.bicep:52`). `context/foundation/infrastructure.md` already assigns the B1 upgrade and VNet/private PostgreSQL endpoint transition to S-03. A hosted worker on the current tier cannot establish unattended liveness.
- API tests use real PostgreSQL and Azurite (`services/api.Tests/ApiFactory.cs:9`). Inject deterministic extractors, clocks and worker controls for retry/lease tests; existing upload fixtures are not evidence of readable OCR inputs.
- Production runtime access uses managed identities. Local credential storage already exists in user secrets and gitignored environment files (`docs/local-development.md`, Configuration).

## External Research

External sources inform provider selection; they do not establish current repository behavior.

- [Document Intelligence data privacy](https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/document-intelligence/data-privacy-security) documents temporary input/result storage and a 24-hour retention period after completion, with earlier removal through Delete Analyze Result. This conflicts with a literal interpretation of immediate post-processing non-retention in the PRD; provider policy requires a user decision.
- [Delete Analyze Result v4 API](https://learn.microsoft.com/en-us/rest/api/aiservices/document-models/delete-analyze-result?view=rest-aiservices-v4.0+(2024-11-30)) describes marking a result for deletion. Do not interpret its 204 as independently verified physical deletion timing.
- [Read model](https://learn.microsoft.com/en-us/azure/ai-services/document-intelligence/concept-read) is the candidate off-the-shelf PDF/image text-reading model. Its suitability for the owner's veterinary photos remains unmeasured.
- [App Service VNet integration](https://learn.microsoft.com/en-gb/azure/app-service/overview-vnet-integration) supports Basic-tier integration; [PostgreSQL Private Link](https://learn.microsoft.com/en-us/azure/postgresql/network/concepts-networking-private-link) supports the planned migration from public access mode. Implementation must stage private connectivity before disabling the public route.

## Code References

- `services/api/Documents/OwnedDocuments.cs:181` — transactional storage completion.
- `services/api/Documents/OwnedDocuments.cs:279` — membership ownership boundary.
- `services/api/Documents/DocumentCaptureService.cs:194` — independent original read path.
- `services/api/Data/AppDbContext.cs:64` — storage-state constraints.
- `services/api.Tests/ApiFactory.cs:9` — database/storage integration fixture.
- `apps/web/src/components/DocumentDetails.tsx:27` — current detail fetch.

## Architecture Insights

Separate storage completion from reading status. A durable database queue is a feasible new design using the existing database; it is not mandated by existing code. Keep S-03 extraction/status work separate from S-06 activity/capture eligibility. Compose shared edits in OwnedDocuments, AppDbContext, API client, translations and docs, and serialize EF migration generation against an integrated model.

## Historical Context

`context/changes/capture-document-original/plan.md` explicitly defers OCR/search and broader animal management. Its original-storage contracts are supported by the current source paths above, but its initial Current State section predates the implementation and is not a current code inventory.

## Related Research

`context/changes/animal-profiles-and-history/research.md` covers activity filtering and upload/deactivation boundaries.

## Open Questions

Internal integration discovery is complete. On 2026-10-04 the owner selected Azure with the explicit bounded retention exception now recorded in the PRD, local PDF text with OCR fallback, and automatic retries plus an owner Retry action. These supersede the unresolved choices above. Real-photo quality remains an implementation acceptance risk, not an unresolved architecture choice. No real owner records were sent to an external provider, and no production state was queried.
