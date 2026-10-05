# Review follow-ups: capture-document-original

Queued from `reviews/impl-review.md` (2026-10-04). Each item needs its own change (`/10x-new`); none is part of this change's scope.

## F10 — Antiforgery for every cookie-authenticated mutation

- **Where**: `services/api/Animals/AnimalEndpoints.cs:17` (`POST /api/animals/`), `services/api/Auth/AuthEndpoints.cs:14-16` (auth POST routes, including logout).
- **Problem**: Only `/api/document-uploads` uses `AntiforgeryHeaderFilter`. The other mutations rely on the JSON content type and SameSite cookies, so logout, for example, can be triggered cross-site. The gap predates capture-document-original.
- **Proposed change**: Apply `AntiforgeryHeaderFilter` to all cookie-authenticated mutations, and send the `X-CSRF-TOKEN` header from `apps/web/src/lib/api.ts` for them, reusing the existing token-fetch path.
- **Open question**: how sign-in and register obtain a token before a session exists (anonymous antiforgery token vs. keeping these routes on content-type + SameSite and documenting why).
