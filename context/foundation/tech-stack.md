---
starter_id: dotnet
package_manager: dotnet
project_name: ogarniamy-zwierzaki
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

The project combines an Astro and TypeScript frontend on Azure Static Web Apps Standard with an ASP.NET Core Web API on Azure App Service. This keeps the core in the developer's strongest ecosystem while supporting private Blob Storage, Queue Storage with a WebJob for OCR and embeddings, PostgreSQL with pgvector, and Azure AI services. The .NET starter passes all four agent-friendly quality gates and has verified bootstrapper support, which reduces risk for a solo, after-hours seven-week MVP. GitHub Actions will deploy automatically after merges to main. Azure resources and deployment configuration will be defined in Bicep, the simplest native infrastructure-as-code option for this Azure-only architecture.
