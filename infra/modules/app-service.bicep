// Linux App Service plan and the ASP.NET Core API web app.

@description('Region of the plan and the web app.')
param location string

@description('App Service plan SKU. F1 (Free) rejects Always On.')
@allowed([
  'F1'
  'B1'
  'S1'
])
param appServiceSku string

@description('PostgreSQL Flexible Server name; the host is <postgresServerName>.postgres.database.azure.com.')
param postgresServerName string

@description('Blob service endpoint of the originals storage account, for example https://<account>.blob.core.windows.net/.')
param storageBlobEndpoint string

@description('Name of the private container holding the originals.')
param originalsContainerName string

@description('Document Intelligence endpoint, for example https://<account>.cognitiveservices.azure.com/. Not a secret.')
param documentIntelligenceEndpoint string

@description('Document Intelligence model used for reading.')
param documentReadingModelId string

@description('Document Intelligence REST API version.')
param documentReadingApiVersion string

@description('Azure OpenAI endpoint, for example https://<account>.openai.azure.com/. Not a secret.')
param embeddingEndpoint string

@description('Name of the embedding model deployment.')
param embeddingDeployment string

@description('Number of dimensions requested from the embedding model; must match the vector column.')
param embeddingDimensions int

@description('Minimum cosine similarity for a search hit; provisional until calibrated on real documents.')
param searchMinSimilarity string

// The site's own settings need its name, so it is computed once instead of read back from the resource.
var siteName = 'app-ogarniamy-api-${uniqueString(subscription().id)}'

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: 'asp-ogarniamy-mvp'
  location: location
  kind: 'linux'
  sku: {
    name: appServiceSku
  }
  properties: {
    reserved: true
  }
}

resource api 'Microsoft.Web/sites@2024-04-01' = {
  name: siteName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    // The API keeps no in-process session state, so instance-affinity cookies (ARRAffinity) are not needed.
    clientAffinityEnabled: false
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: appServiceSku != 'F1'
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'Database__Auth'
          value: 'AzureManagedIdentity'
        }
        {
          // No password: in AzureManagedIdentity mode the API signs in with an Entra token for its managed identity.
          name: 'ConnectionStrings__Default'
          value: 'Host=${postgresServerName}.postgres.database.azure.com;Database=ogarniamy;Username=${siteName};Ssl Mode=Require'
        }
        {
          // No key or connection string: the API reaches Blob Storage with its managed identity.
          name: 'Storage__Auth'
          value: 'AzureManagedIdentity'
        }
        {
          name: 'Storage__BlobServiceUri'
          value: storageBlobEndpoint
        }
        {
          name: 'Storage__OriginalsContainer'
          value: originalsContainerName
        }
        {
          // No key: the Document Intelligence account disables local auth; the API uses its managed identity.
          name: 'DocumentReading__Auth'
          value: 'AzureManagedIdentity'
        }
        {
          name: 'DocumentReading__Endpoint'
          value: documentIntelligenceEndpoint
        }
        {
          name: 'DocumentReading__ModelId'
          value: documentReadingModelId
        }
        {
          name: 'DocumentReading__ApiVersion'
          value: documentReadingApiVersion
        }
        {
          // Background reading is on. To pause it, set this back to 'false' and deploy; jobs, text and pending
          // cleanup stay in the database (see docs/sop/operate-document-reading.md).
          name: 'DocumentReading__WorkerEnabled'
          value: 'true'
        }
        {
          // No key: the Azure OpenAI account disables local auth; the API uses its managed identity.
          name: 'Embedding__Auth'
          value: 'AzureManagedIdentity'
        }
        {
          name: 'Embedding__Endpoint'
          value: embeddingEndpoint
        }
        {
          name: 'Embedding__Deployment'
          value: embeddingDeployment
        }
        {
          name: 'Embedding__Dimensions'
          value: string(embeddingDimensions)
        }
        {
          // Off on the first deploy; set to 'true' in a separate release once the owner has granted the role
          // (see docs/deployment.md, "Rolling out semantic search").
          name: 'Embedding__WorkerEnabled'
          value: 'false'
        }
        {
          name: 'Search__MinSimilarity'
          value: searchMinSimilarity
        }
      ]
    }
  }
}

output id string = api.id
output name string = api.name
output defaultHostName string = api.properties.defaultHostName
output principalId string = api.identity.principalId
output possibleOutboundIpAddresses string = api.properties.possibleOutboundIpAddresses
