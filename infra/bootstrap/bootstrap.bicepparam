using './main.bicep'

param location = 'swedencentral'
// GitHub immutable OIDC subject prefix: owner@ownerId/name@repoId (the repository uses use_immutable_subject).
param githubRepo = 'gustaw-beznicki@132839973/ogarniamy-zwierzaki@1372210399'
param budgetAmount = 40
param budgetStartDate = '2026-09-01'
// The repository is public: the alert recipient comes from the local environment only.
param budgetContactEmail = readEnvironmentVariable('BUDGET_ALERT_EMAIL')
