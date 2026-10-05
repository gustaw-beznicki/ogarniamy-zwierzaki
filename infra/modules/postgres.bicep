// PostgreSQL Flexible Server with Entra-only authentication and the application database.
// The API reaches it through a private endpoint (postgres-private-endpoint.bicep). While public access stays enabled,
// it is limited to the API's outbound IPs. The API's managed identity is the only administrator.

@description('Server name; the host is <serverName>.postgres.database.azure.com.')
param serverName string

@description('Region of the server.')
param location string

@description('App Service name, used as the Entra administrator principal name (and the database user name).')
param apiAppName string

@description('Object id of the App Service system-assigned managed identity.')
param apiPrincipalId string

@description('App Service outbound IP addresses allowed through the firewall, one rule per address.')
param allowedIpAddresses array

@description('Deploy the firewall rules. False skips the module; existing rules stay in place (deployments are incremental).')
param applyFirewallRules bool

@description('Public network access. Disabled leaves only the private endpoint; the old firewall rules are not deleted but no longer apply.')
@allowed([
  'Enabled'
  'Disabled'
])
param publicNetworkAccess string

resource server 'Microsoft.DBforPostgreSQL/flexibleServers@2025-08-01' = {
  name: serverName
  location: location
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '17'
    storage: {
      storageSizeGB: 32
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    network: {
      publicNetworkAccess: publicNetworkAccess
    }
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Disabled'
      tenantId: tenant().tenantId
    }
  }
}

resource administrator 'Microsoft.DBforPostgreSQL/flexibleServers/administrators@2025-08-01' = {
  parent: server
  name: apiPrincipalId
  properties: {
    principalType: 'ServicePrincipal'
    principalName: apiAppName
    tenantId: tenant().tenantId
  }
}

// Server-level operations are serialized: the flexible server rejects concurrent child updates.
resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2025-08-01' = {
  parent: server
  name: 'ogarniamy'
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
  dependsOn: [
    administrator
  ]
}

// A separate module: the rule count is known only at deployment time, so what-if cannot expand this module.
// Keeping the loop in its own module lets what-if still show the server, administrator and database above.
// Skipped in private mode: firewall rules only govern the public endpoint.
module firewall 'postgres-firewall.bicep' = if (applyFirewallRules && publicNetworkAccess == 'Enabled') {
  name: '${deployment().name}-firewall'
  params: {
    serverName: server.name
    allowedIpAddresses: allowedIpAddresses
  }
  dependsOn: [
    database
  ]
}

output name string = server.name
output fullyQualifiedDomainName string = server.properties.fullyQualifiedDomainName
