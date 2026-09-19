---
starter_id: dotnet
package_manager: dotnet
project_name: ogarniamy-zwierzaki-api
components:
  - id: api
    starter_id: dotnet
    package_manager: dotnet
    project_name: ogarniamy-zwierzaki-api
    target_dir: services/api
    language_family: dotnet
  - id: web
    starter_id: astro
    package_manager: npm
    project_name: ogarniamy-zwierzaki-web
    target_dir: apps/web
    language_family: js
hints:
  language_family: multi
  team_size: solo
  deployment_target: azure-app-service
  ci_provider: github-actions
  ci_default_flow: auto-deploy-on-merge
  bootstrapper_confidence: verified
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
  has_ai: true
  has_background_jobs: true
---

## Why this stack

Ogarniamy zwierzaki is split into an Astro and TypeScript frontend in `apps/web` and an ASP.NET Core API in `services/api`, with .NET retained as the primary component and Azure App Service as the recorded deployment target. This keeps authentication, private document storage, PostgreSQL with vector search, OCR, embeddings, and background processing in the developer's strongest ecosystem while giving the mobile-first interface a lightweight frontend. Astro passes all four agent-friendly gates, but its content-first bias is an accepted tradeoff for this application-shaped UI; the separate .NET API owns application logic. Both registered paths have verified historical scaffolding confidence, which the scaffold-adapter will re-check against current official CLI documentation. For scaffolding, the API is explicitly constrained to the locally installed .NET SDK `10.0.112`; SDK `10.0.401` is not required. GitHub Actions remains configured for automatic deployment after merges.
