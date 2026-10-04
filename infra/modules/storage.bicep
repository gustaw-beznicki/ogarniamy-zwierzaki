// Private Blob Storage for document originals. Shared by main.bicep (CI) and bootstrap/storage-access.bicep (owner).
// No keys, SAS or anonymous access: the API reaches the container with its managed identity only.

@description('Storage account name (3-24 lowercase letters and digits), computed by the caller.')
param storageAccountName string

@description('Region of the storage account (the backend region).')
param location string

@description('Name of the private container holding the originals.')
param originalsContainerName string

// Recovery window for deleted blobs, overwritten versions and deleted containers. Not an independent backup.
var softDeleteRetentionDays = 30

resource account 'Microsoft.Storage/storageAccounts@2025-06-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    // Entra ID (managed identity) only: account keys and SAS signed with them are rejected.
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
    allowCrossTenantReplication: false
    // The F1 App Service has no VNet integration, so the account stays on the public endpoint behind Entra ID.
    publicNetworkAccess: 'Enabled'
  }
}

// No lifecycle management policy: originals never expire automatically.
resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2025-06-01' = {
  parent: account
  name: 'default'
  properties: {
    isVersioningEnabled: true
    deleteRetentionPolicy: {
      enabled: true
      days: softDeleteRetentionDays
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: softDeleteRetentionDays
    }
  }
}

resource originals 'Microsoft.Storage/storageAccounts/blobServices/containers@2025-06-01' = {
  parent: blobService
  name: originalsContainerName
  properties: {
    publicAccess: 'None'
  }
}

output name string = account.name
output blobEndpoint string = account.properties.primaryEndpoints.blob
output originalsContainerName string = originals.name
