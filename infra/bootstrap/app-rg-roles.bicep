// Role assignments scoped to the application resource group. Called only from bootstrap/main.bicep.
targetScope = 'resourceGroup'

@description('Principal id of the deploy-path identity.')
param deployPrincipalId string

@description('Name of the deploy-path identity; part of the role assignment name.')
param deployIdentityName string

var contributorRoleId = 'b24988ac-6180-42a0-ab88-20f7382dd24c'

// The name includes the resource group scope: role assignment names are unique across the tenant,
// so it must differ from the former subscription-scope assignment.
resource deployContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, deployIdentityName, contributorRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', contributorRoleId)
    principalId: deployPrincipalId
    principalType: 'ServicePrincipal'
  }
}
