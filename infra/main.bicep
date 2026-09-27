// Subscription-scope entry point for the MVP environment. The budget lives in bootstrap/main.bicep.
targetScope = 'subscription'

@description('Backend region (resource group and App Service).')
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

resource appResourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-ogarniamy-mvp'
  location: location
}

module appService 'modules/app-service.bicep' = {
  name: 'ogarniamy-app-service'
  scope: appResourceGroup
  params: {
    location: location
    appServiceSku: appServiceSku
  }
}

module staticWebApp 'modules/static-web-app.bicep' = {
  name: 'ogarniamy-static-web-app'
  scope: appResourceGroup
  params: {
    swaLocation: swaLocation
    backendLocation: location
    apiAppId: appService.outputs.id
  }
}

output resourceGroupName string = appResourceGroup.name
output apiAppName string = appService.outputs.name
output apiDefaultHostName string = appService.outputs.defaultHostName
output staticWebAppName string = staticWebApp.outputs.name
output staticWebAppDefaultHostName string = staticWebApp.outputs.defaultHostName
