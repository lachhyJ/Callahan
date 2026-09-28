#!/usr/bin/env bash
# Mints a fresh 30-day Callahan auth token, bakes it directly into the
# watch app's build as a compiled constant, and rebuilds the .PRG.
#
# Why baked in rather than pasted at runtime: the Garmin watch app's
# settings aren't reachable for a sideloaded data field — Garmin Connect
# Mobile's "My Data Fields" list only shows Connect IQ Store apps, data
# fields (unlike standalone apps/widgets) have no on-device settings
# screen, and Garmin Express — which would normally be the fallback
# settings editor — doesn't support Apple Silicon Macs.
#
# Why a compiled constant (GeneratedAuthToken.mc) and not a Property
# default: Connect IQ properties persist across app updates by design —
# confirmed on both the simulator and the real watch — so a new default
# value never takes effect for an app id that's already installed once.
# A plain compiled-in constant has no such persistence; every rebuild
# fully replaces it. Re-run this monthly, then copy the rebuilt .PRG onto
# the watch via OpenMTP (GARMIN/Apps), replacing the old one. See
# README.md.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")"
WATCH_DIR="../../watch"
TOKEN_MC="$WATCH_DIR/source/GeneratedAuthToken.mc"
SDK_ROOT="$HOME/Library/Application Support/Garmin/ConnectIQ"
DEV_KEY="$SDK_ROOT/keys/developer_key.der"

if [ ! -f "$SDK_ROOT/current-sdk.cfg" ]; then
  echo "No Connect IQ SDK found at $SDK_ROOT. Is the SDK Manager set up?" >&2
  exit 1
fi
SDK_DIR=$(cat "$SDK_ROOT/current-sdk.cfg")

# The one place the server URL is set: _fetch-token.sh logs in against it
# and _bake_token.py compiles it into the watch build. Override to point a
# build at a local backend.
export CALLAHAN_BASE_URL="${CALLAHAN_BASE_URL:-https://callahan.ljlab.online}"

export CALLAHAN_TOKEN
CALLAHAN_TOKEN=$(bash _fetch-token.sh)

python3 _bake_token.py "$TOKEN_MC"

unset CALLAHAN_TOKEN

echo "Token and base URL baked into GeneratedAuthToken.mc. Rebuilding..."
(
  cd "$WATCH_DIR"
  "$SDK_DIR/bin/monkeyc" -d fr965 -f monkey.jungle -o bin/CallahanDataField.prg -y "$DEV_KEY" -l 2
)

echo ""
echo "Build ready: watch/bin/CallahanDataField.prg"
echo "Next: open OpenMTP, connect the watch, and copy that file into"
echo "GARMIN/Apps on the watch, replacing the existing one."
