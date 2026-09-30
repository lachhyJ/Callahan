#!/bin/bash
# Seed, then dump endpoints (volatile ids and timestamps normalised) to a file,
# for a byte-for-byte before/after diff of a refactor.
# Usage: dump-api.sh <checkout> <out-file> <endpoint>...   e.g. streaks "trends?months=6"
set -u
W=${1:?checkout path}; out=${2:?out file}; shift 2
"$(dirname "$0")/devbackend.sh" "$W" >/dev/null
T=$(curl -s -X POST http://localhost:8099/api/auth/dev-login | python3 -c 'import sys,json;print(json.load(sys.stdin)["token"])')
: > "$out"
for u in "$@"; do
  echo "== $u" >> "$out"
  curl -s -H "Authorization: Bearer $T" "http://localhost:8099/api/$u" \
    | sed -E 's/"computedAt":"[^"]*"//g; s/"(id|activityId|workoutSessionId|timerId|tournamentId)":[0-9]+/"\1":N/g' >> "$out"
  echo >> "$out"
done
wc -c "$out"
