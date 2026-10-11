// Azure Document Intelligence (S0) for reading stored documents. Shared by main.bicep (CI) and
// bootstrap/document-reading-access.bicep (owner). Entra ID only: local (key) authentication is disabled.

@description('Account name, computed by the caller; also used as the custom subdomain of the endpoint.')
param accountName string

@description('Region of the account (the backend region).')
param location string

resource account 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: accountName
  location: location
  // S0: the free tier analyses only the first pages of a file and accepts smaller files than capture stores.
  sku: {
    name: 'S0'
  }
  kind: 'FormRecognizer'
  properties: {
    // Required for Entra ID tokens; the endpoint becomes https://<accountName>.cognitiveservices.azure.com/.
    customSubDomainName: accountName
    disableLocalAuth: true
    // The API reaches the public endpoint with its managed identity; there is no private endpoint for it in this slice.
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

output name string = account.name
output endpoint string = account.properties.endpoint
