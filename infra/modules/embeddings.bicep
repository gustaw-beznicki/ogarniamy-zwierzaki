// Azure OpenAI account and text-embedding-3-large deployment for semantic indexing. Shared by main.bicep (CI) and
// bootstrap/embedding-access.bicep (owner). Entra ID only: local (key) authentication is disabled.

@description('Account name, computed by the caller; also used as the custom subdomain of the endpoint.')
param accountName string

@description('Region of the account (the backend region; EU only).')
param location string

@description('Name of the embedding model deployment.')
param deploymentName string

resource account 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: accountName
  location: location
  sku: {
    name: 'S0'
  }
  kind: 'OpenAI'
  properties: {
    // Required for Entra ID tokens; the endpoint becomes https://<accountName>.openai.azure.com/.
    customSubDomainName: accountName
    disableLocalAuth: true
    // The API reaches the public endpoint with its managed identity; there is no private endpoint for it in this slice.
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

resource embeddingDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: account
  name: deploymentName
  // Standard (regional) keeps processing inside the account's region; Global and DataZone SKUs are not used.
  sku: {
    name: 'Standard'
    // Thousands of tokens per minute; small on purpose, raise it only if throttling shows up.
    capacity: 50
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'text-embedding-3-large'
      version: '1'
    }
  }
}

output name string = account.name
output endpoint string = account.properties.endpoint
output deploymentName string = embeddingDeployment.name
