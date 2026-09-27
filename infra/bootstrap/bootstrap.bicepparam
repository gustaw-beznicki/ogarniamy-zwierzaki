using './main.bicep'

param location = 'swedencentral'
param githubRepo = 'gustaw-beznicki/ogarniamy-zwierzaki'
param budgetAmount = 20
param budgetStartDate = '2026-09-01'
// The repository is public: the alert recipient comes from the local environment only.
param budgetContactEmail = readEnvironmentVariable('BUDGET_ALERT_EMAIL')
