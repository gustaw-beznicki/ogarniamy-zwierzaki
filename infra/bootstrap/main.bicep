// One-time bootstrap, applied by hand (never from CI): GitHub OIDC identities, the application
// resource group with the deploy identity's scoped role, and the subscription budget.
targetScope = 'subscription'

@description('Region of the CI/CD and application resource groups and of the identities.')
param location string

@description('GitHub OIDC repository subject prefix without "repo:" (immutable form owner@ownerId/name@repoId), trusted by the federated credentials.')
param githubRepo string

@description('Monthly budget amount in the billing currency.')
param budgetAmount int

@description('Budget start date, first day of a month (yyyy-MM-01). Cannot be changed after creation.')
param budgetStartDate string

@description('Recipient of budget alerts. Supplied from the local environment only.')
@secure()
param budgetContactEmail string

var cicdResourceGroupName = 'rg-ogarniamy-cicd'
var appResourceGroupName = 'rg-ogarniamy-mvp'
var deployIdentityName = 'id-ogarniamy-github'
var prIdentityName = 'id-ogarniamy-github-pr'

var readerRoleId = 'acdd72a7-3385-48ef-bd42-f606fba81ae7'

resource cicdResourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: cicdResourceGroupName
  location: location
}

// The application template deploys into this resource group; the deploy identity is Contributor here only.
resource appResourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: appResourceGroupName
  location: location
}

module identities 'identities.bicep' = {
  name: 'ogarniamy-github-identities'
  scope: cicdResourceGroup
  params: {
    location: location
    githubRepo: githubRepo
    deployIdentityName: deployIdentityName
    prIdentityName: prIdentityName
  }
}

resource whatIfRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: guid(subscription().id, 'Ogarniamy What-If')
  properties: {
    roleName: 'Ogarniamy What-If'
    description: 'Validate and preview deployments without writing resources.'
    type: 'CustomRole'
    assignableScopes: [
      subscription().id
    ]
    permissions: [
      {
        actions: [
          'Microsoft.Resources/deployments/validate/action'
          'Microsoft.Resources/deployments/whatIf/action'
        ]
        notActions: []
      }
    ]
  }
}

module appResourceGroupRoles 'app-rg-roles.bicep' = {
  name: 'ogarniamy-app-rg-roles'
  scope: appResourceGroup
  params: {
    deployPrincipalId: identities.outputs.deployPrincipalId
    deployIdentityName: deployIdentityName
  }
}

resource prReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, cicdResourceGroupName, prIdentityName, readerRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', readerRoleId)
    principalId: identities.outputs.prPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource prWhatIf 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, cicdResourceGroupName, prIdentityName, whatIfRole.name)
  properties: {
    roleDefinitionId: whatIfRole.id
    principalId: identities.outputs.prPrincipalId
    principalType: 'ServicePrincipal'
  }
}

module budget '../modules/budget.bicep' = {
  name: 'ogarniamy-budget'
  params: {
    budgetAmount: budgetAmount
    budgetStartDate: budgetStartDate
    budgetContactEmail: budgetContactEmail
  }
}

output clientId string = identities.outputs.deployClientId
output prClientId string = identities.outputs.prClientId
output tenantId string = tenant().tenantId
