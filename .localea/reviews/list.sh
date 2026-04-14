#!/usr/bin/env bash
# Usage: list.sh [state]
#   state  optional filter: open | resolved
#
# Reads reviews.json from the workspace root (two levels above .localea/)
# and prints each thread with its comments.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REVIEWS_FILE="$(cd "$SCRIPT_DIR/../.." && pwd)/reviews.json"

if [[ ! -f "$REVIEWS_FILE" ]]; then
  echo "No reviews.json found at: $REVIEWS_FILE" >&2
  exit 1
fi

STATE_FILTER="${1:-}"

# Validate optional argument
if [[ -n "$STATE_FILTER" && "$STATE_FILTER" != "open" && "$STATE_FILTER" != "resolved" ]]; then
  echo "Usage: list.sh [open|resolved]" >&2
  exit 1
fi

if [[ -n "$STATE_FILTER" ]]; then
  jq --arg state "$STATE_FILTER" 'map(select(.state == $state))' "$REVIEWS_FILE"
else
  jq '.' "$REVIEWS_FILE"
fi
