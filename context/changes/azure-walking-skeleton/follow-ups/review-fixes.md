# Review follow-ups: azure-walking-skeleton

Queued from `reviews/impl-review.md` (2026-09-27).

- [ ] **F3 (deferred to S-01)**: narrow the deploy identity. Create the application resource group in the bootstrap, scope Contributor to it, and give the subscription only a deployment-only custom role. Remove the persistence path through `rg-ogarniamy-cicd` (the federated credentials and the budget).
- [ ] **F9 (verify during manual check 3.9)**: after the second `deploy` run, confirm the `linkedBackends/api` re-PUT succeeded, the smoke test passed, and the direct API URL still returns 401/403. If the apply fails ("preexisting Azure Static Web Apps configuration") or the lock is lost, apply the plan's `linkBackend` fallback (Migration Notes).
- [ ] **F4 (user action)**: re-run the bootstrap once, so the `@secure()` budget email replaces the stored `ogarniamy-bootstrap` deployment record.
