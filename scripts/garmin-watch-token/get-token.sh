#!/usr/bin/env bash
# Mints a fresh 30-day Callahan auth token and copies it to the clipboard.
#
# NOTE: the Garmin watch app's settings aren't reachable through Garmin
# Connect Mobile for a sideloaded (unsigned) data field — its "My Data
# Fields" list doesn't surface apps that didn't come from the Connect IQ
# Store, and there's no on-device settings screen for data fields (only
# for standalone apps/widgets). Garmin Express would normally provide the
# fallback settings editor, but it doesn't support Apple Silicon Macs.
#
# So this script's clipboard output isn't actually usable to paste
# anywhere right now — use bake-and-build.sh instead, which writes the
# token directly into the watch app's build. Kept for whenever a real
# settings-editing path is found (see README.md).
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")"
token=$(bash _fetch-token.sh)

if command -v pbcopy >/dev/null; then
  echo -n "$token" | pbcopy
  echo "Token copied to clipboard (valid 30 days from now)."
else
  echo "$token"
fi
