#!/bin/bash
# Deploy Callahan on the NAS: pull latest main, rebuild, and record what's live.
# Invoked over SSH (forced-command, see .github/workflows/deploy.yml + README note),
# or run manually on the NAS for a rollback: ./deploy.sh <commit-ish>
set -euo pipefail

cd /mnt/tank/callahan

REF="${1:-origin/main}"

git fetch origin
git checkout main
git reset --hard "$REF"

sudo docker compose -f docker-compose.prod.yml up -d --build

git rev-parse HEAD > .deployed_sha
echo "Deployed $(cat .deployed_sha) ($(git log -1 --format=%s))"
