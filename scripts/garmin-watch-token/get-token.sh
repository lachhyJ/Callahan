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

account=$(security find-generic-password -s "$SERVICE" 2>/dev/null | grep '"acct"' | sed -E 's/.*="(.*)"/\1/')
if [ -z "$account" ]; then
  echo "No Keychain item found for service '$SERVICE'. See README.md to set one up." >&2
  exit 1
fi

# Credentials go to the HTTP request via environment variables read inside
# Python, not as command-line arguments (to python3, curl, or anything
# else) — argv is visible to any other local process via `ps` for as long
# as the process runs.
#
# The actual HTTP call goes through curl (via subprocess, body fed over
# stdin — never curl's argv either), not Python's urllib. Cloudflare's WAF
# fingerprints urllib's TLS/HTTP client signature and blocks it outright
# (a 403 with Cloudflare's own "error code: 1010" — nothing to do with the
# credentials), where curl passes through fine.
export CALLAHAN_ACCOUNT="$account"
export CALLAHAN_BASE_URL="$BASE_URL"
export CALLAHAN_PASSWORD
CALLAHAN_PASSWORD=$(security find-generic-password -s "$SERVICE" -w)

token=$(python3 <<'PYEOF'
import json
import os
import subprocess
import sys

base_url = os.environ["CALLAHAN_BASE_URL"]
body = json.dumps({
    "username": os.environ["CALLAHAN_ACCOUNT"],
    "password": os.environ["CALLAHAN_PASSWORD"],
})

result = subprocess.run(
    [
        "curl", "-s", "-X", "POST", f"{base_url}/api/auth/login",
        "-H", "Content-Type: application/json",
        "--data-binary", "@-",
        "-w", "\n%{http_code}",
    ],
    input=body,
    capture_output=True,
    text=True,
)

*body_lines, status_code = result.stdout.rsplit("\n", 1)
response_body = "\n".join(body_lines)

if result.returncode != 0 or status_code.strip() != "200":
    print(f"Login failed: HTTP {status_code.strip()}", file=sys.stderr)
    print(response_body, file=sys.stderr)
    sys.exit(1)

print(json.loads(response_body)["token"])
PYEOF
)

unset CALLAHAN_ACCOUNT CALLAHAN_PASSWORD CALLAHAN_BASE_URL

if command -v pbcopy >/dev/null; then
  echo -n "$token" | pbcopy
  echo "Token copied to clipboard (valid 30 days from now)."
else
  echo "$token"
fi
