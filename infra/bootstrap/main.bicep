// One-time bootstrap, applied by hand (never from CI): GitHub OIDC identities and the subscription budget.
targetScope = 'subscription'

@description('Region of the CI/CD resource group and identities.')
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
var deployIdentityName = 'id-ogarniamy-github'
var prIdentityName = 'id-ogarniamy-github-pr'

var contributorRoleId = 'b24988ac-6180-42a0-ab88-20f7382dd24c'
var readerRoleId = 'acdd72a7-3385-48ef-bd42-f606fba81ae7'

resource cicdResourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: cicdResourceGroupName
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
    description: 'Validate and preview subscription deployments without writing resources.'
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

resource deployContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, cicdResourceGroupName, deployIdentityName, contributorRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', contributorRoleId)
    principalId: identities.outputs.deployPrincipalId
    principalType: 'ServicePrincipal'
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
