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

// Computed once and passed to both modules, so the API's connection setting does not reference the server
// (whose Entra administrator in turn needs the API's managed identity).
var postgresServerName = 'psql-ogarniamy-${uniqueString(resourceGroup().id)}'

module appService 'modules/app-service.bicep' = {
  name: 'ogarniamy-app-service'
  params: {
    location: location
    appServiceSku: appServiceSku
    postgresServerName: postgresServerName
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
