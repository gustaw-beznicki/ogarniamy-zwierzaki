// Owner-run entry point (never from CI): grants Cognitive Services User on the Document Intelligence account to the
// API's managed identity and, optionally, to developers who run the local OCR probe with `az login`.
// The CI deploy identity is Contributor and cannot create role assignments, so these grants are applied by hand,
// before the first release that reads documents. It provisions the same account through the shared module, so it
// can run before or after the regular deployment, and creates no other role assignment.
targetScope = 'resourceGroup'

@description('Backend region; must match location in environments/mvp.bicepparam.')
param location string

@description('Object id of the existing App Service system-assigned managed identity.')
param apiPrincipalId string

@description('Object ids of Entra users who may call the account with their own sign-in (the local OCR probe). Empty by default.')
param developerPrincipalIds array = []

// Must match the value in ../main.bicep, so both entry points manage the same account.
var documentIntelligenceName = 'di-ogarniamy-${uniqueString(resourceGroup().id)}'

// Cognitive Services User: call the data plane (analyze, get and delete results); no keys and no role management.
var cognitiveServicesUserRoleId = 'a97b65f3-24c7-4388-baec-2e87135dc908'

module documentIntelligence '../modules/document-intelligence.bicep' = {
  name: 'ogarniamy-document-reading-access-account'
  params: {
    accountName: documentIntelligenceName
    location: location
  }
}

resource account 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = {
  name: documentIntelligenceName
}

// Scoped to the Document Intelligence account only, not to the resource group.
resource apiCognitiveServicesUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(account.id, apiPrincipalId, cognitiveServicesUserRoleId)
  scope: account
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesUserRoleId)
    principalId: apiPrincipalId
    principalType: 'ServicePrincipal'
  }
  dependsOn: [
    documentIntelligence
  ]
}

resource developerCognitiveServicesUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for developerPrincipalId in developerPrincipalIds: {
    name: guid(account.id, developerPrincipalId, cognitiveServicesUserRoleId)
    scope: account
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesUserRoleId)
      principalId: developerPrincipalId
      principalType: 'User'
    }
    dependsOn: [
      documentIntelligence
    ]
  }
]

output documentIntelligenceName string = documentIntelligence.outputs.name
output documentIntelligenceEndpoint string = documentIntelligence.outputs.endpoint
