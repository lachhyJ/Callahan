#!/bin/bash
# Every gate, one line each. Usage: baseline.sh <checkout> [e2e] [watch] [ios]
set -u
W=$(cd "${1:?checkout path}" && pwd); shift
OUT=${AUDIT_TMP:-${TMPDIR:-/tmp}/callahan-audit}; mkdir -p "$OUT"
cd "$W"
echo "backend build: $(dotnet build backend 2>&1 | grep -cE ' error ') errors, $(dotnet build backend 2>&1 | grep -E 'warning CS' | sort -u | wc -l | tr -d ' ') warnings"
echo "backend tests: $(dotnet test tests/Callahan.Api.Tests/Callahan.Api.Tests.csproj --nologo 2>&1 | tail -1 | grep -oE '(Passed|Failed)!.*Total: +[0-9]+')"
cd frontend
echo "frontend build: $(npm run build 2>&1 | grep -oE 'built in [0-9.]+m?s|error.*' | head -1)"
echo "frontend tests: $(npm test 2>&1 | grep -E '^ +Tests' | tr -s ' ')"
echo "oxlint: $(npx oxlint 2>&1 | grep -cE 'warning|error') findings"
for a in "$@"; do
  case $a in
    e2e)
      pkill -f "$W/backend" 2>/dev/null   # a stale server gets reused otherwise
      echo "e2e: $(npx playwright test 2>&1 | grep -E '^\s+[0-9]+ (passed|failed)' | tr -s ' ' | tr '\n' ' ')";;
    watch)
      (cd "$W/watch"; SR="$HOME/Library/Application Support/Garmin/ConnectIQ"; SD=$(cat "$SR/current-sdk.cfg")
       [ -f source/GeneratedAuthToken.mc ] || cp source/GeneratedAuthToken.mc.example source/GeneratedAuthToken.mc
       echo "watch: $("$SD/bin/monkeyc" -d fr965 -f monkey.jungle -o "$OUT/c.prg" -y "$SR/keys/developer_key.der" -l 3 2>&1 | grep -vE 'launcher icon' | tail -2 | tr '\n' ' ')");;
    ios)
      (cd "$W/frontend" && npx cap copy ios >/dev/null 2>&1; cd ios/App && echo "ios: $(xcodebuild -project App.xcodeproj -scheme App -destination 'generic/platform=iOS Simulator' -derivedDataPath "$OUT/iosdd" CODE_SIGNING_ALLOWED=NO build 2>&1 | grep -E 'warning:|error:|BUILD (SUCCEEDED|FAILED)' | sort -u | tail -5 | tr '\n' ' ')");;
  esac
done
