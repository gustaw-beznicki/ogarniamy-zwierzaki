// Owner-run entry point (never from CI): grants the API's managed identity access to the originals container.
// The CI deploy identity is Contributor and cannot create role assignments, so this one grant is applied by hand,
// before the first release that needs storage. It provisions the same storage through the shared module, so it
// can run before or after the regular deployment, and creates no other role assignment.
targetScope = 'resourceGroup'

@description('Backend region; must match location in environments/mvp.bicepparam.')
param location string

@description('Object id of the existing App Service system-assigned managed identity.')
param apiPrincipalId string

// Must match the values in ../main.bicep, so both entry points manage the same account and container.
var storageAccountName = 'stogarniamy${uniqueString(resourceGroup().id)}'
var originalsContainerName = 'originals'

// Storage Blob Data Contributor: read, write and delete blobs; no account keys and no role management.
var blobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

module storage '../modules/storage.bicep' = {
  name: 'ogarniamy-storage-access-storage'
  params: {
    storageAccountName: storageAccountName
    location: location
    originalsContainerName: originalsContainerName
  }
}

resource account 'Microsoft.Storage/storageAccounts@2025-06-01' existing = {
  name: storageAccountName
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2025-06-01' existing = {
  parent: account
  name: 'default'
}

resource originals 'Microsoft.Storage/storageAccounts/blobServices/containers@2025-06-01' existing = {
  parent: blobService
  name: originalsContainerName
}

// Scoped to the originals container only, not to the account or the resource group.
resource apiBlobDataContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(originals.id, apiPrincipalId, blobDataContributorRoleId)
  scope: originals
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', blobDataContributorRoleId)
    principalId: apiPrincipalId
    principalType: 'ServicePrincipal'
  }
  dependsOn: [
    storage
  ]
}

output storageAccountName string = storage.outputs.name
output originalsContainerName string = storage.outputs.originalsContainerName
