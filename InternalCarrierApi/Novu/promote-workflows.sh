#!/usr/bin/env bash
#
# Copies workflows from the environment owning NOVU_SECRET_KEY (normally Development)
# into the target environment, so production is never edited by hand.
#
# Usage:
#   NOVU_SECRET_KEY=<dev-key> NOVU_TARGET_ENVIRONMENT_ID=<prod-env-id> ./promote-workflows.sh
#
set -euo pipefail

API_URL="${NOVU_API_URL:-https://api.novu.co}"
WORKFLOW_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/workflows"

if [[ -z "${NOVU_SECRET_KEY:-}" ]]; then
  echo "NOVU_SECRET_KEY is not set. Use the key of the SOURCE environment (Development)." >&2
  exit 1
fi

if [[ -z "${NOVU_TARGET_ENVIRONMENT_ID:-}" ]]; then
  echo "NOVU_TARGET_ENVIRONMENT_ID is not set. Take the target environment id from the dashboard." >&2
  exit 1
fi

failed=0
while IFS= read -r file; do
  workflow_id="$(basename "$file" .json)"

  response_body="$(mktemp)"
  status="$(curl -s -o "$response_body" -w '%{http_code}' \
    -X PUT "$API_URL/v2/workflows/$workflow_id/sync" \
    -H "Authorization: ApiKey $NOVU_SECRET_KEY" \
    -H "Content-Type: application/json" \
    -d "{\"targetEnvironmentId\":\"$NOVU_TARGET_ENVIRONMENT_ID\"}")"

  if [[ "$status" == "200" || "$status" == "201" ]]; then
    printf '  ok      %s\n' "$workflow_id"
  else
    printf '  FAILED  %s (HTTP %s)\n' "$workflow_id" "$status" >&2
    cat "$response_body" >&2
    echo >&2
    failed=1
  fi
  rm -f "$response_body"
done < <(find "$WORKFLOW_DIR" -maxdepth 1 -name '*.json' | sort)

exit "$failed"
