// Owner-run entry point (never from CI): grants Cognitive Services OpenAI User on the Azure OpenAI embeddings account
// to the API's managed identity and, optionally, to developers who run embeddings locally with `az login`.
// The CI deploy identity is Contributor and cannot create role assignments, so these grants are applied by hand,
// before the embedding worker is enabled. It provisions the same account through the shared module, so it
// can run before or after the regular deployment, and creates no other role assignment.
targetScope = 'resourceGroup'

@description('Backend region; must match location in environments/mvp.bicepparam.')
param location string

@description('Object id of the existing App Service system-assigned managed identity.')
param apiPrincipalId string

@description('Object ids of Entra users who may call the account with their own sign-in (local embedding runs). Empty by default.')
param developerPrincipalIds array = []

// Must match the values in ../main.bicep, so both entry points manage the same account and deployment.
var embeddingsAccountName = 'oai-ogarniamy-${uniqueString(resourceGroup().id)}'
var embeddingDeploymentName = 'text-embedding-3-large'

// Cognitive Services OpenAI User: call inference (embeddings); no keys, no deployment management and no role management.
var cognitiveServicesOpenAiUserRoleId = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'

module embeddings '../modules/embeddings.bicep' = {
  name: 'ogarniamy-embedding-access-account'
  params: {
    accountName: embeddingsAccountName
    location: location
    deploymentName: embeddingDeploymentName
  }
}

resource account 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = {
  name: embeddingsAccountName
}

// Scoped to the Azure OpenAI account only, not to the resource group.
resource apiOpenAiUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(account.id, apiPrincipalId, cognitiveServicesOpenAiUserRoleId)
  scope: account
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesOpenAiUserRoleId)
    principalId: apiPrincipalId
    principalType: 'ServicePrincipal'
  }
  dependsOn: [
    embeddings
  ]
}

resource developerOpenAiUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for developerPrincipalId in developerPrincipalIds: {
    name: guid(account.id, developerPrincipalId, cognitiveServicesOpenAiUserRoleId)
    scope: account
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesOpenAiUserRoleId)
      principalId: developerPrincipalId
      principalType: 'User'
    }
    dependsOn: [
      embeddings
    ]
  }
]

output embeddingsAccountName string = embeddings.outputs.name
output embeddingsEndpoint string = embeddings.outputs.endpoint
