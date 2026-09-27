#!/usr/bin/env bash
# Single entry point for infrastructure commands, used locally and by CI.
# Usage: infra/deploy.sh <lint|what-if|apply>
set -euo pipefail

INFRA_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEPLOYMENT_NAME='ogarniamy-mvp'
DEPLOYMENT_LOCATION='swedencentral'
TEMPLATE_FILE="${INFRA_DIR}/main.bicep"
PARAMETERS_FILE="${INFRA_DIR}/environments/mvp.bicepparam"

usage() {
  echo "Usage: $0 <lint|what-if|apply>" >&2
  exit 2
}

[[ $# -eq 1 ]] || usage

case "$1" in
  lint)
    az bicep lint --file "${TEMPLATE_FILE}"
    az bicep lint --file "${INFRA_DIR}/bootstrap/main.bicep"
    ;;
  what-if)
    az deployment sub what-if \
      --location "${DEPLOYMENT_LOCATION}" \
      --name "${DEPLOYMENT_NAME}" \
      --template-file "${TEMPLATE_FILE}" \
      --parameters "${PARAMETERS_FILE}"
    ;;
  apply)
    az deployment sub create \
      --location "${DEPLOYMENT_LOCATION}" \
      --name "${DEPLOYMENT_NAME}" \
      --template-file "${TEMPLATE_FILE}" \
      --parameters "${PARAMETERS_FILE}" \
      --query properties.outputs \
      --output json
    ;;
  *)
    usage
    ;;
esac
