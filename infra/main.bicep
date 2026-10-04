// Resource-group-scope entry point for the MVP environment, deployed into rg-ogarniamy-mvp.
// The resource group, the deploy identity's role and the budget live in bootstrap/main.bicep.
targetScope = 'resourceGroup'

@description('Backend region (App Service).')
param location string

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
var postgresServerName = 'psql-ogarniamy-${uniqueString(resourceGroup().id)}'

// Computed here, not from App Service outputs; bootstrap/storage-access.bicep must use the same values.
// No role assignment is created here: the API's Blob access is granted once by the owner (storage-access.bicep).
var storageAccountName = 'stogarniamy${uniqueString(resourceGroup().id)}'
var originalsContainerName = 'originals'

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
  }
}

module postgres 'modules/postgres.bicep' = {
  name: 'ogarniamy-postgres'
  params: {
    serverName: postgresServerName
    location: location
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
