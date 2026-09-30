#!/bin/bash
# Start a checkout's backend on :8099 with the e2e anchor clock, wait, seed.
# Usage: devbackend.sh <checkout>
set -u
W=$(cd "${1:?checkout path}" && pwd)
OUT=${AUDIT_TMP:-${TMPDIR:-/tmp}/callahan-audit}; mkdir -p "$OUT"
ANCHOR=$(grep -oE "ANCHOR = '[^']+'" "$W/frontend/e2e/anchor.js" | cut -d"'" -f2)
pkill -f "$W/backend" 2>/dev/null; sleep 1
cd "$W"
ASPNETCORE_ENVIRONMENT=Development Auth__AllowDevLogin=true Auth__Username=audit \
  ASPNETCORE_URLS=http://localhost:8099 Cors__AllowedOrigins__0=http://localhost:5183 \
  Dev__FixedNow="$ANCHOR" ProgramDoc__MarkdownPath="$W/frontend/e2e/fixtures/program.md" \
  nohup dotnet run --project backend/Callahan.Api.csproj --no-launch-profile > "$OUT/backend.log" 2>&1 &
for _ in $(seq 1 60); do
  curl -s -o /dev/null -w '%{http_code}' -X POST http://localhost:8099/api/auth/dev-login | grep -q 200 && break; sleep 2
done
curl -s -X POST http://localhost:8099/api/dev/seed; echo
