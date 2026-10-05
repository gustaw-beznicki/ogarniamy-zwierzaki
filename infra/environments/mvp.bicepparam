using '../main.bicep'

param location = 'swedencentral'
param swaLocation = 'eastus2'
param appServiceSku = 'B1'
// Staged: Enabled until the private endpoint path is verified from the API; then Disabled in a separate approved apply.
param postgresPublicNetworkAccess = 'Enabled'
