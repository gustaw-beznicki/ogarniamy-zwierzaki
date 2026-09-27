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
  name: 'app-ogarniamy-api-${uniqueString(subscription().id)}'
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: plan.id
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
      ]
    }
  }
}

output id string = api.id
output name string = api.name
output defaultHostName string = api.properties.defaultHostName
