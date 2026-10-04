#!/usr/bin/env bash
# End-to-end smoke test of the deployed app. Used by CI and runnable locally.
# Usage: scripts/smoke.sh <swa-host> <api-host>
set -euo pipefail

usage() {
  echo "Usage: $0 <swa-host> <api-host>" >&2
  exit 2
}

[[ $# -eq 2 ]] || usage

SWA_HOST="$1"
API_HOST="$2"
RETRY_INTERVAL_SECONDS=20
RETRY_TIMEOUT_SECONDS=600

BODY_FILE="$(mktemp)"
trap 'rm -f "${BODY_FILE}"' EXIT

fail() {
  echo "FAIL: $1" >&2
  exit 1
}

# Prints the HTTP status code ("000" on connection failure) and writes the body to BODY_FILE.
fetch() {
  local status
  status="$(curl --silent --show-error --max-time 60 --output "${BODY_FILE}" --write-out '%{http_code}' "$1")" || true
  echo "${status:-000}"
}

# Check 1: both locale sign-in pages are served and carry the app marker.
for path in /signin/ /en/signin/; do
  url="https://${SWA_HOST}${path}"
  status="$(fetch "${url}")"
  [[ "${status}" == "200" ]] || fail "check 1: ${url} returned ${status}, expected 200"
  grep -q 'data-app="ogarniamy-zwierzaki"' "${BODY_FILE}" || fail "check 1: ${url} body does not contain the app marker"
  echo "PASS: check 1: ${url} returned 200 and contains the app marker"
done

# Check 2: the API and database are healthy through the Static Web Apps /api proxy (retried for cold start and link propagation).
url="https://${SWA_HOST}/api/health"
deadline=$((SECONDS + RETRY_TIMEOUT_SECONDS))
while true; do
  status="$(fetch "${url}")"
  if [[ "${status}" == "200" ]] && grep -q '"status":"ok"' "${BODY_FILE}"; then
    echo "PASS: check 2: ${url} returned 200 with \"status\":\"ok\""
    break
  fi
  if (( SECONDS >= deadline )); then
    fail "check 2: ${url} did not return 200 with \"status\":\"ok\" within ${RETRY_TIMEOUT_SECONDS}s (last status ${status})"
  fi
  echo "WAIT: check 2: ${url} returned ${status}; retrying in ${RETRY_INTERVAL_SECONDS}s"
  sleep "${RETRY_INTERVAL_SECONDS}"
done

# Check 2b: the authenticated API is reachable through the proxy and refuses an anonymous request without redirecting.
# fetch does not send cookies or follow redirects; a login redirect therefore fails this check.
url="https://${SWA_HOST}/api/me"
status="$(fetch "${url}")"
[[ "${status}" == "401" ]] || fail "check 2b: ${url} returned ${status}, expected 401 without a session"
echo "PASS: check 2b: ${url} returned 401 without a session"

# Check 2c: document routes refuse anonymous reads, so a pasted original link is useless without the owner's session.
# Read-only GET requests without a session; the session check runs before any lookup, so a placeholder ID is enough.
PROBE_ID="00000000-0000-4000-8000-000000000000"
for path in \
  "/api/capture-defaults" \
  "/api/animals/${PROBE_ID}/documents" \
  "/api/documents/${PROBE_ID}" \
  "/api/documents/${PROBE_ID}/files/${PROBE_ID}/original" \
  "/api/documents/${PROBE_ID}/files/${PROBE_ID}/original?download=true"; do
  url="https://${SWA_HOST}${path}"
  status="$(fetch "${url}")"
  [[ "${status}" == "401" ]] || fail "check 2c: ${url} returned ${status}, expected 401 without a session"
  echo "PASS: check 2c: ${url} returned 401 without a session"
done

# Check 3: the App Service refuses direct traffic because it is a linked backend.
url="https://${API_HOST}/api/health"
status="$(fetch "${url}")"
# Only an authorization refusal proves the lock; 000, 404 and 5xx mean the backend is broken, not locked.
[[ "${status}" == "401" || "${status}" == "403" ]] || fail "check 3: ${url} returned ${status}, expected 401 or 403 (direct access refused)"
echo "PASS: check 3: ${url} refused direct access with status ${status}"
