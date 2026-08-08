#!/bin/bash

set -euo pipefail

SERVER_URL="${MYMUSIC_SERVER_URL:-http://localhost:5000}"

IDS=$(curl -s "$SERVER_URL/users" | jq -r '.users[] | select(.name | startswith("Test")) | .id')

for id in $IDS; do
    code=$(curl -s -o /dev/null -w "%{http_code}" -X DELETE "$SERVER_URL/users/$id")
    echo "Deleted user $id (HTTP $code)"
done
