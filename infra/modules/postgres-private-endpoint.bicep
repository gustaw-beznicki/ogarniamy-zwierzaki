// Private endpoint of the PostgreSQL Flexible Server, with the private DNS zone linked to the virtual network, so the
// server's usual host name resolves to a private address inside the VNet. Called from main.bicep after postgres.bicep:
// the server rejects concurrent child updates, and the endpoint adds a private endpoint connection to it.

@description('Name of the existing PostgreSQL Flexible Server.')
param serverName string

@description('Region of the private endpoint (the backend region).')
param location string

@description('Resource id of the virtual network the DNS zone is linked to.')
param vnetId string

@description('Resource id of the private-endpoint subnet.')
param subnetId string

resource server 'Microsoft.DBforPostgreSQL/flexibleServers@2025-08-01' existing = {
  name: serverName
}

resource privateEndpoint 'Microsoft.Network/privateEndpoints@2024-05-01' = {
  name: 'pe-${serverName}'
  location: location
  properties: {
    subnet: {
      id: subnetId
    }
    privateLinkServiceConnections: [
      {
        name: 'postgres'
        properties: {
          privateLinkServiceId: server.id
          groupIds: [
            'postgresqlServer'
          ]
        }
      }
    ]
  }
}

// <server>.postgres.database.azure.com is a CNAME to <server>.privatelink.postgres.database.azure.com; inside the
// linked VNet this zone answers it with the endpoint's private address, so the connection string does not change.
resource dnsZone 'Microsoft.Network/privateDnsZones@2024-06-01' = {
  name: 'privatelink.postgres.database.azure.com'
  location: 'global'
}

resource dnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2024-06-01' = {
  parent: dnsZone
  name: 'vnet-ogarniamy-mvp'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnetId
    }
  }
}

// Keeps the zone's A record in step with the endpoint's address.
resource dnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = {
  parent: privateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'postgres'
        properties: {
          privateDnsZoneId: dnsZone.id
        }
      }
    ]
  }
}

output privateEndpointName string = privateEndpoint.name
