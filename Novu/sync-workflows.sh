#!/usr/bin/env bash
#
# Pushes every workflow definition in ./workflows to Novu.
#
# Creates the workflow when it does not exist yet and updates it otherwise, so the
# script is safe to re-run: the JSON files in this folder are the source of truth and
# the dashboard is the mirror, not the other way around.
#
# Usage:
#   NOVU_SECRET_KEY=<key> ./sync-workflows.sh
#   NOVU_SECRET_KEY=<key> ./sync-workflows.sh carrier-notification
#
set -euo pipefail

API_URL="${NOVU_API_URL:-https://api.novu.co}"
WORKFLOW_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/workflows"

if [[ -z "${NOVU_SECRET_KEY:-}" ]]; then
  echo "NOVU_SECRET_KEY is not set. Take it from the Novu dashboard under Settings > API Keys." >&2
  exit 1
fi

# The secret key is environment-scoped, so whichever environment it belongs to is the
# one that gets written. Promote to production with promote-workflows.sh afterwards.
sync_workflow() {
  local file="$1"
  local workflow_id
  workflow_id="$(basename "$file" .json)"

  local response_body status
  response_body="$(mktemp)"
  trap 'rm -f "$response_body"' RETURN

  status="$(curl -s -o /dev/null -w '%{http_code}' \
    -X GET "$API_URL/v2/workflows/$workflow_id" \
    -H "Authorization: ApiKey $NOVU_SECRET_KEY")"

  local method url
  if [[ "$status" == "200" ]]; then
    method="PUT"
    url="$API_URL/v2/workflows/$workflow_id"
  else
    method="POST"
    url="$API_URL/v2/workflows"
  fi

  status="$(curl -s -o "$response_body" -w '%{http_code}' \
    -X "$method" "$url" \
    -H "Authorization: ApiKey $NOVU_SECRET_KEY" \
    -H "Content-Type: application/json" \
    --data-binary "@$file")"

  if [[ "$status" == "200" || "$status" == "201" ]]; then
    printf '  ok      %-28s (%s)\n' "$workflow_id" "$method"
    return 0
  fi

  printf '  FAILED  %-28s (%s, HTTP %s)\n' "$workflow_id" "$method" "$status" >&2
  cat "$response_body" >&2
  echo >&2
  return 1
}

targets=()
if [[ $# -gt 0 ]]; then
  for name in "$@"; do
    targets+=("$WORKFLOW_DIR/${name%.json}.json")
  done
else
  while IFS= read -r file; do targets+=("$file"); done \
    < <(find "$WORKFLOW_DIR" -maxdepth 1 -name '*.json' | sort)
fi

echo "Syncing ${#targets[@]} workflow(s) to $API_URL"

failed=0
for file in "${targets[@]}"; do
  if [[ ! -f "$file" ]]; then
    printf '  FAILED  %-28s (file not found)\n' "$(basename "$file")" >&2
    failed=1
    continue
  fi
  sync_workflow "$file" || failed=1
done

exit "$failed"
