#!/usr/bin/env bash
# Mints a fresh 30-day Callahan auth token, bakes it directly into the
# watch app's build (properties.xml default), and rebuilds the .PRG.
#
# Why baked in rather than pasted at runtime: the Garmin watch app's
# settings aren't reachable for a sideloaded data field — Garmin Connect
# Mobile's "My Data Fields" list only shows Connect IQ Store apps, data
# fields (unlike standalone apps/widgets) have no on-device settings
# screen, and Garmin Express — which would normally be the fallback
# settings editor — doesn't support Apple Silicon Macs. Baking the token
# into the build is the practical workaround: re-run this monthly, then
# copy the rebuilt .PRG onto the watch via OpenMTP (GARMIN/Apps),
# replacing the old one. See README.md.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")"
WATCH_DIR="../../watch"
PROPERTIES_XML="$WATCH_DIR/resources/settings/properties.xml"
SDK_ROOT="$HOME/Library/Application Support/Garmin/ConnectIQ"
DEV_KEY="$SDK_ROOT/keys/developer_key.der"

if [ ! -f "$SDK_ROOT/current-sdk.cfg" ]; then
  echo "No Connect IQ SDK found at $SDK_ROOT. Is the SDK Manager set up?" >&2
  exit 1
fi
SDK_DIR=$(cat "$SDK_ROOT/current-sdk.cfg")

# properties.xml is gitignored (it holds the real token) — bootstrap it
# from the tracked placeholder template on a fresh clone/worktree.
if [ ! -f "$PROPERTIES_XML" ]; then
  cp "$WATCH_DIR/resources/settings/properties.xml.example" "$PROPERTIES_XML"
fi

export CALLAHAN_TOKEN
CALLAHAN_TOKEN=$(bash _fetch-token.sh)

python3 _bake_token.py "$PROPERTIES_XML"

unset CALLAHAN_TOKEN

echo "Token baked into properties.xml. Rebuilding..."
(
  cd "$WATCH_DIR"
  "$SDK_DIR/bin/monkeyc" -d fr965 -f monkey.jungle -o bin/CallahanDataField.prg -y "$DEV_KEY"
)

echo ""
echo "Build ready: watch/bin/CallahanDataField.prg"
echo "Next: open OpenMTP, connect the watch, and copy that file into"
echo "GARMIN/Apps on the watch, replacing the existing one."
