#!/usr/bin/env bash
# Mints a fresh 30-day Callahan auth token and copies it to the clipboard,
# for pasting into the Garmin watch app's "Auth token" setting (Garmin
# Connect Mobile > Watch > Connect IQ Store > My Data Fields > Rest Timer).
#
# Reads the Callahan username/password from a macOS Keychain item so
# nothing is typed into a terminal, script, or chat — see README.md for
# the one-time setup.
set -euo pipefail

SERVICE="callahan-app"
BASE_URL="https://callahan.ljlab.online"

if ! command -v security >/dev/null; then
  echo "This script needs macOS Keychain (the 'security' CLI)." >&2
  exit 1
fi

account=$(security find-generic-password -s "$SERVICE" 2>&1 >/dev/null | grep '"acct"' | sed -E 's/.*="(.*)"/\1/')
if [ -z "$account" ]; then
  echo "No Keychain item found for service '$SERVICE'. See README.md to set one up." >&2
  exit 1
fi

password=$(security find-generic-password -s "$SERVICE" -w)

response=$(curl -sf -X POST "$BASE_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "$(python3 -c 'import json,sys; print(json.dumps({"username": sys.argv[1], "password": sys.argv[2]}))' "$account" "$password")")

token=$(echo "$response" | python3 -c 'import json,sys; print(json.load(sys.stdin)["token"])')

if command -v pbcopy >/dev/null; then
  echo -n "$token" | pbcopy
  echo "Token copied to clipboard (valid 30 days from now)."
else
  echo "$token"
fi
