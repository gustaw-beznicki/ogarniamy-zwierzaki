#!/usr/bin/env bash
# Single entry point for infrastructure commands, used locally and by CI.
# Usage: infra/deploy.sh <lint|what-if|apply>
set -euo pipefail

INFRA_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEPLOYMENT_NAME='ogarniamy-mvp'
RESOURCE_GROUP='rg-ogarniamy-mvp'
TEMPLATE_FILE="${INFRA_DIR}/main.bicep"
PARAMETERS_FILE="${INFRA_DIR}/environments/mvp.bicepparam"

usage() {
  echo "Usage: $0 <lint|what-if|apply>" >&2
  exit 2
}

# Prints false when the PostgreSQL firewall already allows exactly the App Service outbound IPs, otherwise true:
# first deployment, changed IPs, or anything that cannot be read. Re-applying the rules costs about a minute each.
firewall_rules_needed() {
  local app server wanted existing
  app="$(az webapp list --resource-group "${RESOURCE_GROUP}" --query '[0].name' --output tsv 2>/dev/null)" || app=''
  server="$(az postgres flexible-server list --resource-group "${RESOURCE_GROUP}" --query '[0].name' --output tsv 2>/dev/null)" || server=''
  if [[ -z "${app}" || -z "${server}" ]]; then
    echo true
    return
  fi
  wanted="$(az webapp show --resource-group "${RESOURCE_GROUP}" --name "${app}" \
    --query possibleOutboundIpAddresses --output tsv 2>/dev/null | tr ',' '\n' | sort -u)" || wanted=''
  existing="$(az postgres flexible-server firewall-rule list --resource-group "${RESOURCE_GROUP}" --server-name "${server}" \
    --query '[].startIpAddress' --output tsv 2>/dev/null | sort -u)" || existing=''
  if [[ -n "${wanted}" && "${wanted}" == "${existing}" ]]; then
    echo false
  else
    echo true
  fi
}

# Prints the postgresPublicNetworkAccess value set in the parameters file (Enabled when it is not set).
postgres_public_network_access() {
  local value
  value="$(sed -n "s/^[[:space:]]*param[[:space:]]\{1,\}postgresPublicNetworkAccess[[:space:]]*=[[:space:]]*'\([A-Za-z]*\)'.*/\1/p" \
    "${PARAMETERS_FILE}" | tail -n 1)"
  echo "${value:-Enabled}"
}

# In private mode the firewall rules are skipped: they only govern the public endpoint, so there is nothing to compare.
apply_firewall_rules() {
  if [[ "$(postgres_public_network_access)" == 'Disabled' ]]; then
    echo false
  else
    firewall_rules_needed
  fi
}

[[ $# -eq 1 ]] || usage

case "$1" in
  lint)
    az bicep lint --file "${TEMPLATE_FILE}"
    az bicep lint --file "${INFRA_DIR}/bootstrap/main.bicep"
    az bicep lint --file "${INFRA_DIR}/bootstrap/storage-access.bicep"
    az bicep lint --file "${INFRA_DIR}/bootstrap/document-reading-access.bicep"
    ;;
  what-if)
    apply_firewall="$(apply_firewall_rules)"
    echo "PostgreSQL public access: $(postgres_public_network_access); firewall rules: applyFirewallRules=${apply_firewall}" >&2
    # ProviderNoRbac: full validation that needs only read permission, so the read-only PR identity can run it.
    az deployment group what-if \
      --validation-level ProviderNoRbac \
      --resource-group "${RESOURCE_GROUP}" \
      --name "${DEPLOYMENT_NAME}" \
      --template-file "${TEMPLATE_FILE}" \
      --parameters "${PARAMETERS_FILE}" \
      --parameters applyFirewallRules="${apply_firewall}"
    ;;
  apply)
    apply_firewall="$(apply_firewall_rules)"
    # Stderr only: stdout carries the deployment outputs that the deploy workflow parses as JSON.
    echo "PostgreSQL public access: $(postgres_public_network_access); firewall rules: applyFirewallRules=${apply_firewall}" >&2
    az deployment group create \
      --resource-group "${RESOURCE_GROUP}" \
      --name "${DEPLOYMENT_NAME}" \
      --template-file "${TEMPLATE_FILE}" \
      --parameters "${PARAMETERS_FILE}" \
      --parameters applyFirewallRules="${apply_firewall}" \
      --query properties.outputs \
      --output json
    ;;
  *)
    usage
    ;;
esac
