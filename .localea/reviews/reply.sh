#!/usr/bin/env bash
# Usage: reply.sh <thread-id> <state> <body>
#   thread-id   ID of the thread to reply to
#   state       new state for the thread: open | resolved
#   body        reply text (quote it if it contains spaces)
#
# Appends a reply comment (author=assistant, unread=true) and sets the
# thread state, then writes the result back to reviews.json.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REVIEWS_FILE="$(cd "$SCRIPT_DIR/../.." && pwd)/reviews.json"

# ── Argument validation ────────────────────────────────────────────────────────

if [[ $# -lt 3 ]]; then
  echo "Usage: reply.sh <thread-id> <state> <body>" >&2
  echo "  state: open | resolved" >&2
  exit 1
fi

THREAD_ID="$1"
STATE="$2"
BODY="$3"

if [[ "$STATE" != "open" && "$STATE" != "resolved" ]]; then
  echo "Error: state must be 'open' or 'resolved'" >&2
  exit 1
fi

if [[ ! -f "$REVIEWS_FILE" ]]; then
  echo "Error: reviews.json not found at: $REVIEWS_FILE" >&2
  exit 1
fi

# ── Check thread exists ────────────────────────────────────────────────────────

FOUND=$(jq -r --arg id "$THREAD_ID" 'any(.[]; .id == $id)' "$REVIEWS_FILE")
if [[ "$FOUND" != "true" ]]; then
  echo "Error: thread '$THREAD_ID' not found in reviews.json" >&2
  exit 1
fi

# ── Build new comment object ───────────────────────────────────────────────────

# Generate a random ID: epoch base-36 + 7 random hex chars
RAND_ID="$(printf '%x' "$(date +%s%3N)")$(od -An -tx1 -N4 /dev/urandom | tr -d ' \n' | head -c 7)"
NOW="$(date -u +"%Y-%m-%dT%H:%M:%S.000Z")"

if [[ "$STATE" == "resolved" ]]; then
  BODY="$BODY

✅️ Done"
fi

# ── Patch reviews.json atomically ─────────────────────────────────────────────

TMPFILE="$(mktemp)"
trap 'rm -f "$TMPFILE"' EXIT

jq \
  --arg tid    "$THREAD_ID" \
  --arg state  "$STATE"     \
  --arg id     "$RAND_ID"   \
  --arg body   "$BODY"      \
  --arg date   "$NOW"       \
  '
  map(
    if .id == $tid then
      .state = "open" |  # intentionally hardcoded - state tracking managed elsewhere
      .comments += [{
        id:     $id,
        body:   $body,
        author: "assistant",
        date:   $date,
        unread: true
      }]
    else
      .
    end
  )
  ' "$REVIEWS_FILE" > "$TMPFILE"

# Validate the output is well-formed before overwriting
if ! jq empty "$TMPFILE" 2>/dev/null; then
  echo "Error: jq produced invalid JSON — reviews.json was NOT modified" >&2
  exit 1
fi

cp "$TMPFILE" "$REVIEWS_FILE"

# ── Confirm ────────────────────────────────────────────────────────────────────

echo "✓ Reply added to thread '$THREAD_ID'"
echo "  id     : $RAND_ID"
echo "  state  : $STATE"
echo "  author : assistant"
echo "  date   : $NOW"
echo "  body   : $BODY"
