#!/bin/bash

set -e

SERVER_URL="${MYMUSIC_SERVER_URL:-http://localhost:8080}"

if [ -z "$1" ] || [ -z "$2" ]; then
    echo "Usage: $0 <username> <display_name>"
    echo "Example: $0 johndoe 'John Doe'"
    exit 1
fi

USERNAME="$1"
DISPLAY_NAME="$2"

RESPONSE=$(curl -s -X POST "$SERVER_URL/users" \
    -H "Content-Type: application/json" \
    -d "{\"user\":{\"username\":\"$USERNAME\",\"name\":\"$DISPLAY_NAME\"}}" \
    -w "\n%{http_code}")

HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
BODY=$(echo "$RESPONSE" | sed '$d')

if [ "$HTTP_CODE" = "200" ] || [ "$HTTP_CODE" = "201" ]; then
    echo "User created successfully: $USERNAME"
    echo "$BODY"
else
    echo "Failed to create user (HTTP $HTTP_CODE)"
    echo "$BODY"
    exit 1
fi
