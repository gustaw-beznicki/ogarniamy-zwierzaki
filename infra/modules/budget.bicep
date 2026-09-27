// Subscription-wide monthly cost budget. Called only from bootstrap/main.bicep, never from CI.
targetScope = 'subscription'

@description('Monthly budget amount in the billing currency.')
param budgetAmount int

@description('Budget start date, first day of a month (yyyy-MM-01).')
param budgetStartDate string

@description('Recipient of budget alerts.')
@secure()
param budgetContactEmail string

resource budget 'Microsoft.Consumption/budgets@2024-08-01' = {
  name: 'budget-ogarniamy-monthly'
  properties: {
    category: 'Cost'
    timeGrain: 'Monthly'
    amount: budgetAmount
    timePeriod: {
      startDate: budgetStartDate
    }
    notifications: {
      actual50: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 50
        thresholdType: 'Actual'
        contactEmails: [
          budgetContactEmail
        ]
      }
      actual80: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 80
        thresholdType: 'Actual'
        contactEmails: [
          budgetContactEmail
        ]
      }
      actual100: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 100
        thresholdType: 'Actual'
        contactEmails: [
          budgetContactEmail
        ]
      }
      forecasted100: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 100
        thresholdType: 'Forecasted'
        contactEmails: [
          budgetContactEmail
        ]
      }
    }
  }
}
