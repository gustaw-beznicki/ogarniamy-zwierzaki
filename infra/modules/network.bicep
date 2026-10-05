// Virtual network for the API's outbound traffic: one subnet delegated to App Service VNet integration and one for
// private endpoints. Inbound traffic to the API stays on its public endpoint (the Static Web Apps linked backend).

@description('Region of the virtual network; must match the App Service plan for VNet integration.')
param location string

resource vnet 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: 'vnet-ogarniamy-mvp'
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.20.0.0/16'
      ]
    }
    subnets: [
      {
        // App Service VNet integration uses one address per instance; /26 leaves room for scale-out and swaps.
        name: 'snet-api'
        properties: {
          addressPrefix: '10.20.1.0/26'
          delegations: [
            {
              name: 'app-service'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
        }
      }
      {
        name: 'snet-private-endpoints'
        properties: {
          addressPrefix: '10.20.2.0/27'
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
    ]
  }
}

output id string = vnet.id
output apiSubnetId string = vnet.properties.subnets[0].id
output privateEndpointSubnetId string = vnet.properties.subnets[1].id
