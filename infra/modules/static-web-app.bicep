// Static Web App (Standard) for the Astro build, with the API linked as its /api backend.

@description('Static Web App resource region.')
param swaLocation string

@description('Region of the linked App Service backend.')
param backendLocation string

@description('Resource id of the API web app.')
param apiAppId string

resource staticSite 'Microsoft.Web/staticSites@2024-04-01' = {
  name: 'swa-ogarniamy-web'
  location: swaLocation
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    stagingEnvironmentPolicy: 'Disabled'
  }
}

resource apiBackend 'Microsoft.Web/staticSites/linkedBackends@2024-04-01' = {
  parent: staticSite
  name: 'api'
  properties: {
    backendResourceId: apiAppId
    region: backendLocation
  }
}

output name string = staticSite.name
output defaultHostName string = staticSite.properties.defaultHostname
