// Firewall rules of the PostgreSQL Flexible Server, one per allowed IP address. Called only from postgres.bicep.

@description('Name of the existing PostgreSQL Flexible Server.')
param serverName string

@description('IP addresses allowed through the firewall, one rule per address.')
param allowedIpAddresses array

resource server 'Microsoft.DBforPostgreSQL/flexibleServers@2025-08-01' existing = {
  name: serverName
}

// Serialized: the flexible server rejects concurrent child updates.
@batchSize(1)
resource firewallRules 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2025-08-01' = [
  for ip in allowedIpAddresses: {
    parent: server
    name: 'allow-api-${replace(ip, '.', '-')}'
    properties: {
      startIpAddress: ip
      endIpAddress: ip
    }
  }
]
