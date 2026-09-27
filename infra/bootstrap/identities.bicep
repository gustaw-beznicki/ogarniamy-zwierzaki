// GitHub OIDC identities in rg-ogarniamy-cicd. Called only from bootstrap/main.bicep.
// Each identity has exactly one federated credential; Azure rejects concurrent credential
// writes on one identity, so chain any additional credential with dependsOn or @batchSize(1).

@description('Region of the identities.')
param location string

@description('GitHub repository in owner/name form.')
param githubRepo string

@description('Name of the deploy-path identity.')
param deployIdentityName string

@description('Name of the pull request what-if identity.')
param prIdentityName string

var githubIssuer = 'https://token.actions.githubusercontent.com'
var githubAudience = 'api://AzureADTokenExchange'

resource deployIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: deployIdentityName
  location: location
}

resource deployCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: deployIdentity
  name: 'github-production'
  properties: {
    issuer: githubIssuer
    subject: 'repo:${githubRepo}:environment:production'
    audiences: [
      githubAudience
    ]
  }
}

resource prIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: prIdentityName
  location: location
}

resource prCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: prIdentity
  name: 'github-pull-request'
  properties: {
    issuer: githubIssuer
    subject: 'repo:${githubRepo}:pull_request'
    audiences: [
      githubAudience
    ]
  }
}

output deployPrincipalId string = deployIdentity.properties.principalId
output deployClientId string = deployIdentity.properties.clientId
output prPrincipalId string = prIdentity.properties.principalId
output prClientId string = prIdentity.properties.clientId
