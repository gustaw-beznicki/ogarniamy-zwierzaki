# Hand-off schema

`context/foundation/tech-stack.md` is the file `/10x-tech-stack-selector` writes and `/10x-scaffold-adapter` reads. This doc is the contract for its shape. `/10x-bootstrapper` sees the handoff only to verify its hash against the adapter manifest; it never derives commands from it.

Two contracts live in this doc:

1. **Frontmatter** — 3 required top-level keys (`starter_id`, `project_name`, `hints`), optional `package_manager`, and optional `components` (required for `language_family: multi`).
2. **Body** — exactly one `## Why this stack` heading with one paragraph (≤ 200 words). Nothing else.

The schema is **language-agnostic**. `package_manager` is an open string drawn from whatever the chosen starter's `toolchain.package_manager` field prescribes. `hints.deployment_target` is starter-prescribed (whatever appears in the card's `deployment_defaults` array).

Rich rationale stays in conversation. The body paragraph is a one-paragraph summary — downstream skills do not parse it; it exists for human readers.

# Frontmatter fields

```yaml
---
starter_id: <string>            # required; key from references/starter-registry.yaml
package_manager: <string>       # optional; open string per chosen card; may be omitted entirely
project_name: <string>          # required; kebab-case
components:                     # optional; required and >=2 items for language_family: multi
  - id: <string>                # stable kebab-case identifier, unique in this list
    starter_id: <string>        # key from references/starter-registry.yaml
    package_manager: <string>   # optional; open string per component card
    project_name: <string>      # kebab-case name passed to that component's scaffold CLI
    target_dir: <string>        # safe relative destination in the repository
    language_family: <enum>     # concrete family; never multi
hints:                          # required object; subfields below
  language_family: <enum>
  team_size: <enum>
  deployment_target: <string>
  ci_provider: <enum>
  ci_default_flow: <enum>
  bootstrapper_confidence: <enum>
  path_taken: <enum>
  quality_override: <bool>
  self_check_answers: <object | null>
  has_auth: <bool>
  has_payments: <bool>
  has_realtime: <bool>
  has_ai: <bool>
  has_background_jobs: <bool>
---
```

## Required top-level keys

### `starter_id` (string, required)

The key from the `starters:` map in `references/starter-registry.yaml`. For a single-component handoff, this is the chosen starter. For a multi-component handoff, it mirrors the primary component for backward compatibility.

Examples: `10x-astro-starter`, `next`, `t3`, `fastapi`, `django`, `rails`, `spring`, `laravel`, `go`, `rust`, `expo`, `flutter`, `dotnet`.

### `package_manager` (string, optional — may be omitted)

Open string. Whatever the chosen starter's `toolchain.package_manager` prescribes. Common values include:

- JS family: `npm`, `pnpm`, `yarn`, `bun`
- Python: `uv`, `poetry`, `pip`
- Ruby: `bundle`
- Java: `gradle`, `maven`
- Rust: `cargo`
- Go: `go-modules` (or omit entirely — Go has no external choice)
- PHP: `composer`
- .NET: `dotnet`, `nuget`
- Dart: `pub`

The field MAY be omitted from frontmatter for ecosystems where there's no external choice — Go is the canonical example. For a multi-component handoff, it mirrors the primary component and each component records its own value when applicable.

Do NOT add ecosystem-incompatible values here. The value must match the chosen starter's `toolchain.package_manager`. This is selection metadata, not an executable instruction; `/10x-scaffold-adapter` still verifies the current toolchain from official sources.

### `project_name` (string, required)

Kebab-case. Drawn from PRD's `project` field unless the user overrode it during the project-name confirmation step. For a multi-component handoff, it mirrors the primary component's project name.

### `components` (list, conditionally required)

Omit for a normal single-component project. It is required when `hints.language_family: multi`, must contain at least two items, and becomes the authoritative component breakdown for `/10x-scaffold-adapter`.

Each item must contain:

- `id` — unique kebab-case identifier used in adapter filenames.
- `starter_id` — a key present in this registry.
- `project_name` — kebab-case name intended for that component's official scaffold tool.
- `target_dir` — relative POSIX-style destination such as `apps/web` or `services/api`.
- `language_family` — one concrete permitted family; never `multi`.
- `package_manager` — optional open string, with the same rules as the top-level field.

`target_dir` must not be absolute, empty, `.`, contain `.` or `..` path segments, or begin with `.git`, `context/archive`, or `.bootstrap-scaffold`. Component destinations must be unique, non-overlapping, and non-nested. The selector must reject ambiguous layouts rather than asking the adapter or bootstrapper to infer one.

### `hints` (object, required)

Subfields below. The set is intentionally minimal. New subfields require a schema bump and coordinated updates to the selector, adapter, and bootstrapper contracts.

## Permitted `hints` subfields

### `language_family`

Enum: `js | python | ruby | java | go | rust | php | dotnet | dart | multi`

Drawn from Q0 of the residual interview. (PRD frontmatter does not carry tech_preferences, so `language_family` is typically Q0-derived.) `multi` is reserved for a concrete handoff with at least two independently scaffolded components; it must not mean "I'm not sure" or a single full-stack starter.

### `team_size`

Enum: `solo | small | mixed`

`solo` = 1 person; `small` = 2–5; `mixed` = junior + senior together (the "mixed-experience" option in Q2).

Standard path skips Q2; in that case, default to `solo` (the recommended-default registry is itself optimized for solo, so this is the right baseline when no answer was gathered).

### `deployment_target`

Open string drawn from the chosen starter's `deployment_defaults` array. It describes the deployment decision; neither downstream skill may invent deployment files solely from this value.

If the user picked "I don't know yet" at Q4, this lands as the card's first `deployment_default` value (NOT the literal string `unspecified`). Bootstrapper does not need to handle a missing or unspecified value.

Common values: `cloudflare-pages`, `cloudflare-workers`, `vercel`, `fly`, `railway`, `render`, `self-host`, `aws-lambda`, `google-cloud-run`, `appstore-via-eas`, `testflight`.

### `ci_provider`

Enum: `github-actions | gitlab-ci | circleci | cloudflare-builds`

Drawn from Q5a. Default `github-actions`.

### `ci_default_flow`

Enum: `auto-deploy-on-merge | manual-promotion`

Drawn from Q5b. Default `auto-deploy-on-merge`.

### `bootstrapper_confidence`

Enum: `verified | first-class | best-effort`

Copied verbatim from the chosen card's `bootstrapper_confidence` field. For a multi-component handoff, store the weakest component value (`best-effort` < `first-class` < `verified`). This is a historical selection signal only. The live evidence status written by `/10x-scaffold-adapter` controls whether execution can proceed.

Semantics:

- `verified` — registry authors previously ran the path end-to-end.
- `first-class` — registry authors knew a valid CLI path, without end-to-end proof.
- `best-effort` — historical support was limited; expect additional adapter review.

### `path_taken`

Enum: `standard | custom`

Records which Q0 branch the user chose. `standard` means the recommended-defaults pick was accepted; `custom` means the user walked the full residual interview.

### `quality_override`

Bool. `true` only when the user proceeded with a starter that failed ≥1 of the four agent-friendly quality gates (typed / convention-based / popular_in_training / well_documented), against the skill's Socratic challenge. `false` otherwise.

`true` is informational, not blocking. Any ecosystem-specific compensation belongs in the selection rationale or later project planning; downstream skills must not synthesize an `AGENTS.md` from this flag.

### `self_check_answers`

Object or `null`.

When `path_taken: custom`, this is an object with 5 boolean keys recording the user's answer to each statement of the Q8 self-check (see `references/residual-interview.md`):

```yaml
self_check_answers:
  typed: <bool>
  from_official_starter: <bool>
  conventions: <bool>
  docs_current: <bool>
  can_judge_agent: <bool>
```

When `path_taken: standard`, this is `null` (the standard path is itself the safer choice — no self-check is asked).

### Feature flags (`has_*`)

Five booleans drawn from Q1 (custom path) or detected from PRD FRs (standard path):

- `has_auth` — auth/login/OAuth/JWT in scope.
- `has_payments` — payments/checkout/subscription in scope.
- `has_realtime` — websockets/live-update/presence in scope.
- `has_ai` — LLM/embedding/AI features in scope.
- `has_background_jobs` — queues/cron/scheduled work in scope.

The set is intentionally fixed. Free-text features the user names beyond these five do NOT add new keys; they are surfaced in conversation rationale only. Adding a new `has_*` key requires a schema bump (this doc) and writer/reader updates.

# Body convention

Exactly one heading: `## Why this stack`.

One paragraph, ≤ 200 words, summarizing how PRD priors + residual answers led to the chosen starter. Cite 2–3 load-bearing factors (e.g., "solo + short timeline + has_auth → battle-tested + popular community → Astro+Supabase+Cloudflare wins on agent-friendly criteria"). No bulleted lists, no subsections, no code blocks — downstream skills do not parse this paragraph; it exists for human readers.

Rich rationale (alternatives considered, Socratic moments, quality gate analysis) stays in the conversation transcript only. The file is intentionally lean.

# Example (minimal but valid)

```yaml
---
starter_id: 10x-astro-starter
package_manager: npm
project_name: recipe-fridge
hints:
  language_family: js
  team_size: solo
  deployment_target: cloudflare-pages
  ci_provider: github-actions
  ci_default_flow: auto-deploy-on-merge
  bootstrapper_confidence: verified
  path_taken: standard
  quality_override: false
  self_check_answers: null
  has_auth: true
  has_payments: false
  has_realtime: false
  has_ai: true
  has_background_jobs: false
---

## Why this stack

A solo learner shipping a recipe-matching MVP in 1 week with auth and an LLM
suggestion step needs a battle-tested, agent-friendly starter that handles
auth + database + edge deploy out of the box. Astro+Supabase+Cloudflare is the
recommended default for `(web, js)` and clears all four agent-friendly gates;
its registry confidence is verified; the adapter will still re-check the live CLI. Auth
and AI feature flags are set; payments and realtime are out of scope per PRD
non-goals. CI runs on GitHub Actions with auto-deploy-on-merge — what the
starter ships with.
```

# Example (custom path with overrides)

```yaml
---
starter_id: fastapi
package_manager: uv
project_name: ingest-api
hints:
  language_family: python
  team_size: small
  deployment_target: fly
  ci_provider: github-actions
  ci_default_flow: manual-promotion
  bootstrapper_confidence: first-class
  path_taken: custom
  quality_override: false
  self_check_answers:
    typed: true
    from_official_starter: true
    conventions: true
    docs_current: true
    can_judge_agent: true
  has_auth: true
  has_payments: false
  has_realtime: false
  has_ai: false
  has_background_jobs: true
---

## Why this stack

Small Python team building an ingest API with background jobs. Custom path
because the team has Python expertise and rejected the JS recommended default
at Q0. FastAPI clears all four agent-friendly gates (the per-language-family
caveat applies — popular within Python training data). Fly is the deployment
default in the FastAPI card; manual promotion picked because the team gates
ingest changes through staging. Self-check came back clean across all five
points, so no Socratic nudge fired.
```

# Example (Go, omitted package_manager)

```yaml
---
starter_id: go
project_name: edge-router
hints:
  language_family: go
  team_size: solo
  deployment_target: self-host
  ci_provider: github-actions
  ci_default_flow: auto-deploy-on-merge
  bootstrapper_confidence: first-class
  path_taken: standard
  quality_override: false
  self_check_answers: null
  has_auth: false
  has_payments: false
  has_realtime: false
  has_ai: false
  has_background_jobs: false
---

## Why this stack

Solo developer building a small edge router in Go. Standard path — `go` is the
recommended default for `(api, go)`. Go modules are part of the toolchain, so
package_manager is omitted from frontmatter (no external choice to record).
Deployment defaults to self-host per the Go card; CI on GitHub Actions with
auto-deploy is the starter's standard shape.
```

# Example (multi-component)

```yaml
---
starter_id: next
package_manager: pnpm
project_name: control-room-web
components:
  - id: web
    starter_id: next
    package_manager: pnpm
    project_name: control-room-web
    target_dir: apps/web
    language_family: js
  - id: api
    starter_id: fastapi
    package_manager: uv
    project_name: control-room-api
    target_dir: services/api
    language_family: python
hints:
  language_family: multi
  team_size: small
  deployment_target: self-host
  ci_provider: github-actions
  ci_default_flow: manual-promotion
  bootstrapper_confidence: first-class
  path_taken: custom
  quality_override: false
  self_check_answers:
    typed: true
    from_official_starter: true
    conventions: true
    docs_current: true
    can_judge_agent: true
  has_auth: true
  has_payments: false
  has_realtime: true
  has_ai: false
  has_background_jobs: true
---

## Why this stack

The web UI and Python API have separate release and runtime boundaries, so the
handoff names both scaffold units explicitly. Next.js is the primary component
and is mirrored by the top-level compatibility fields; FastAPI owns the API.
Their non-overlapping target directories let downstream staging and merge
checks treat each component independently.
```
