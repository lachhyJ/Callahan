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

# Credentials go to the HTTP request via environment variables read inside
# Python, not as command-line arguments (to python3, curl, or anything
# else) — argv is visible to any other local process via `ps` for as long
# as the process runs. The whole login call happens inside Python (urllib)
# rather than shelling out to curl with the JSON body on its argv, for the
# same reason.
export CALLAHAN_ACCOUNT="$account"
export CALLAHAN_BASE_URL="$BASE_URL"
export CALLAHAN_PASSWORD
CALLAHAN_PASSWORD=$(security find-generic-password -s "$SERVICE" -w)

token=$(python3 <<'PYEOF'
import json
import os
import sys
import urllib.error
import urllib.request

base_url = os.environ["CALLAHAN_BASE_URL"]
body = json.dumps({
    "username": os.environ["CALLAHAN_ACCOUNT"],
    "password": os.environ["CALLAHAN_PASSWORD"],
}).encode()

req = urllib.request.Request(
    f"{base_url}/api/auth/login",
    data=body,
    headers={"Content-Type": "application/json"},
    method="POST",
)
try:
    with urllib.request.urlopen(req) as resp:
        print(json.load(resp)["token"])
except urllib.error.HTTPError as e:
    print(f"Login failed: {e.code} {e.reason}", file=sys.stderr)
    sys.exit(1)
PYEOF
)

unset CALLAHAN_ACCOUNT CALLAHAN_PASSWORD CALLAHAN_BASE_URL

if command -v pbcopy >/dev/null; then
  echo -n "$token" | pbcopy
  echo "Token copied to clipboard (valid 30 days from now)."
else
  echo "$token"
fi
