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

module appService 'modules/app-service.bicep' = {
  name: 'ogarniamy-app-service'
  params: {
    location: location
    appServiceSku: appServiceSku
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
