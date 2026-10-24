// Resource-group-scope entry point for the MVP environment, deployed into rg-ogarniamy-mvp.
// The resource group, the deploy identity's role and the budget live in bootstrap/main.bicep.
targetScope = 'resourceGroup'

@description('Backend region (App Service).')
param location string

@description('PostgreSQL region. Separate from the backend because swedencentral had no Burstable capacity for the new subscription (2026-10-24).')
param postgresLocation string

@description('Static Web App resource region; static content is served globally.')
@allowed([
  'westeurope'
  'eastus2'
  'centralus'
  'westus2'
  'eastasia'
])
param swaLocation string

@description('App Service plan SKU. F1 disables Always On.')
@allowed([
  'F1'
  'B1'
  'S1'
])
param appServiceSku string

@description('Deploy the PostgreSQL firewall rules. infra/deploy.sh sets false when the existing rules already match the App Service outbound IPs, because re-applying them takes about a minute per rule.')
param applyFirewallRules bool = true

// Computed once and passed to both modules, so the API's connection setting does not reference the server
// (whose Entra administrator in turn needs the API's managed identity).
// The region is part of the name: a server whose creation failed in swedencentral (2026-10-24) left an undeletable
// name reservation there, and a name cannot be reused in another region.
var postgresServerName = 'psql-ogarniamy-${uniqueString(resourceGroup().id, postgresLocation)}'

// Computed here, not from App Service outputs; bootstrap/storage-access.bicep must use the same values.
// No role assignment is created here: the API's Blob access is granted once by the owner (storage-access.bicep).
var storageAccountName = 'stogarniamy${uniqueString(resourceGroup().id)}'
var originalsContainerName = 'originals'

// Computed here for the same reason; bootstrap/document-reading-access.bicep must use the same value. The API's
// Cognitive Services User role is granted once by the owner (document-reading-access.bicep), never here.
var documentIntelligenceName = 'di-ogarniamy-${uniqueString(resourceGroup().id)}'
var documentReadingModelId = 'prebuilt-read'
var documentReadingApiVersion = '2024-11-30'

module documentIntelligence 'modules/document-intelligence.bicep' = {
  name: 'ogarniamy-document-intelligence'
  params: {
    accountName: documentIntelligenceName
    location: location
  }
}

module storage 'modules/storage.bicep' = {
  name: 'ogarniamy-storage'
  params: {
    storageAccountName: storageAccountName
    location: location
    originalsContainerName: originalsContainerName
  }
}

module appService 'modules/app-service.bicep' = {
  name: 'ogarniamy-app-service'
  params: {
    location: location
    appServiceSku: appServiceSku
    postgresServerName: postgresServerName
    storageBlobEndpoint: storage.outputs.blobEndpoint
    originalsContainerName: storage.outputs.originalsContainerName
    documentIntelligenceEndpoint: documentIntelligence.outputs.endpoint
    documentReadingModelId: documentReadingModelId
    documentReadingApiVersion: documentReadingApiVersion
  }
}

module postgres 'modules/postgres.bicep' = {
  name: 'ogarniamy-postgres'
  params: {
    serverName: postgresServerName
    location: postgresLocation
    apiAppName: appService.outputs.name
    apiPrincipalId: appService.outputs.principalId
    // Known only at deployment time, so the module loops over it (a template-level loop fails with BCP178).
    allowedIpAddresses: split(appService.outputs.possibleOutboundIpAddresses, ',')
    applyFirewallRules: applyFirewallRules
  }
}

module staticWebApp 'modules/static-web-app.bicep' = {
  name: 'ogarniamy-static-web-app'
  params: {
    swaLocation: swaLocation
    backendLocation: location
    apiAppId: appService.outputs.id
  }
}

output resourceGroupName string = resourceGroup().name
output apiAppName string = appService.outputs.name
output apiDefaultHostName string = appService.outputs.defaultHostName
output staticWebAppName string = staticWebApp.outputs.name
output staticWebAppDefaultHostName string = staticWebApp.outputs.defaultHostName
output postgresServerName string = postgres.outputs.name
output storageAccountName string = storage.outputs.name
output documentIntelligenceName string = documentIntelligence.outputs.name
output documentIntelligenceEndpoint string = documentIntelligence.outputs.endpoint
