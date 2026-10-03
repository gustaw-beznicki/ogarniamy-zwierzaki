# Account and First Animal — Plan Brief

> Full plan: `context/changes/account-and-first-animal/plan.md`

## What & Why

Owners can register and sign in with an email and password, set up their first animal (name only), and see only their own data. This covers roadmap slice S-01 (FR-001–FR-003, §Access Control). The slice brings in the database, the test project, the app shell and the ownership model that every later slice builds on. It also closes the F-01 follow-up F3 by narrowing the CI deploy identity before any personal data exists.

## Starting Point

A walking skeleton on Azure: a static Astro page and an ASP.NET Core API exposing only `/api/health`. They are served from one origin through Static Web Apps with the API as its linked backend. There is no database, no auth, no tests, and the CI deploy identity is Contributor on the whole subscription.

## Desired End State

A visitor lands on the Polish sign-in screen (English under `/en/`), creates an account, names their first animal, and lands on an Animals page inside the phone/desktop shell. They stay signed in for 14 days and can sign out. A second account never sees the first account's animals. On Azure the API reaches PostgreSQL with a managed identity and no stored password; self-hosted, it uses an ordinary connection string.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) |
| --- | --- | --- |
| Owner sign-in | ASP.NET Core Identity + HttpOnly cookie | Portable (no Azure lock-in), fits the E01 mockup and the same-origin SWA proxy F-01 set up for it. |
| DB authentication | Pluggable: password by default, Azure managed identity via `Database:Auth` | No password exists in Azure, and self-hosting needs only a connection string. |
| DB network | Public mode, firewall limited to App Service outbound IPs; private endpoint at S-03/B1 | Meets the standards' "only required IPs" fallback at $0 and keeps the private-endpoint path reversible. |
| Migrations | Applied at API startup | No database access needed from CI runners; single instance on F1. |
| F3 deploy identity | Included: Contributor only on `rg-ogarniamy-mvp`; template becomes RG-scoped | Close the risk before personal data lands. |
| Budget | $20 → $40 | Expected spend is about $28 per month (SWA Standard about $9 + database about $19), about 70 % of the budget. |
| Isolation | One owner-scoped access component (`OwnedAnimals`) joining via `animal_members` | Explicit, testable, and reusable for S-04's raw vector SQL. |
| Ownership shape | `animal_members(animal_id, user_id, role='owner')` | The PRD makes a role-carrying relationship binding from day one. |
| Account scope | Register, sign in, sign out; no reset or verification | No email service exists; the mockup has none. |
| Session | 14-day sliding persistent cookie | An owner capturing right after a visit is usually still signed in. |
| Onboarding | Shown whenever the owner has zero animals; then Animals list | Robust if registration is interrupted; makes isolation visible. |
| Tests | xUnit + WebApplicationFactory + Testcontainers PostgreSQL, run in CI | Same engine as production; proves isolation before deploy. |
| Frontend | Static Astro + React islands | A component model for later interactive screens, still static hosting. |
| UI language | Astro i18n: Polish default at `/`, English at `/en/`; typed `pl`/`en` catalogues | The real audience gets Polish; Polish lives only in `pl.ts`. |
| Polish copy | Agent drafts, human corrects (manual gate) | The recorded lesson says agent Polish needs review. |
| Local secrets | `dotnet user-secrets` + untracked `.env` | Nothing sensitive in git; becomes the project convention. |

## Scope

**In scope:** F3 identity narrowing and budget; PostgreSQL module; API data access in two modes; startup migrations; Identity with cookie; `animals` and `animal_members`; `/api/auth/*`, `/api/me`, `/api/animals`; integration tests incl. cross-account denial; React + i18n; shell, E01, E02, Animals list, placeholder Search/Add, sign-out, language switch; extended smoke test; README and infrastructure docs.

**Out of scope:** password reset, email verification, MFA and external sign-in (planned as PRD US-03/US-04 for milestone M-2); species and date of birth; animal edit, inactive flag and profile page (S-06); sharing and caretakers; documents and search (S-02 to S-05); private endpoint, VNet and B1 (S-03); Key Vault; row-level security; SSR; browser E2E framework.

## Architecture / Approach

Browser → SWA (static pages per locale, React islands) → `/api/*` proxied on the same origin → ASP.NET Core API (Identity cookie, fallback policy requiring authentication) → `OwnedAnimals` → EF Core/Npgsql → PostgreSQL. A single `NpgsqlDataSource` gets its password from the connection string or from a periodically refreshed Entra token. The API returns stable error codes, and the UI maps them to catalogue text.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. F3 and budget | Deploy identity limited to the app resource group; RG-scoped template; $40 budget | Scope switch must not replace resources; old role assignment must be deleted by hand |
| 2. Database foundation | PostgreSQL with Entra-only auth, two connection modes, startup migrations, Docker dev DB, test project in CI | Bicep dependency cycle and runtime outbound-IP loop |
| 3. Accounts and animals API | Identity cookie, data model with role, owner-scoped access, endpoints, isolation tests | An endpoint bypassing `OwnedAnimals` |
| 4. Frontend | React + i18n, shell, E01/E02, Animals, sign-out, language switch | Flash of protected content before the gate resolves; Polish copy quality |
| 5. Deployed verification | Smoke test for the real app; two-account end-to-end on Azure | `Set-Cookie` not surviving the SWA proxy (untested so far) |

**Prerequisites:** Owner (subscription Owner) available to re-apply the bootstrap, delete the old role assignment and register the PostgreSQL provider; Docker locally.
**Estimated effort:** about 6–8 after-hours sessions across 5 phases, each merged and deployed separately.

## Open Risks & Assumptions

- The SWA linked backend is assumed to pass `Set-Cookie` and `Cookie` through unchanged; Phase 5 proves it. If it fails, the auth transport has to be revisited.
- App Service outbound IPs are shared with other tenants on the same stamp, and they may change on tier changes (F1 → B1 in S-03). This is accepted until the private endpoint arrives.
- The API identity is the database's Entra admin with broad DDL rights. Accepted for the MVP.
- Data-protection keys are stored in PostgreSQL, so sessions survive restarts on App Service and when self-hosted; checked by the restart test.
- Registration reveals whether an email is already taken (409). Accepted as necessary UX.

## Success Criteria (Summary)

- A new owner goes from the sign-in screen to their first animal on the Animals page on a real phone and on desktop, in Polish and in English.
- A second account never sees the first account's animals: integration tests and the deployed two-account check both confirm it.
- The deploy identity holds Contributor only on the application resource group, and no database password exists in Azure.
