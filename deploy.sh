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

# Docker's build context for the frontend is just frontend/, with no .git in
# it — so the version tag has to be computed here, where the real checkout
# is, and handed in via .env (which docker compose reads automatically,
# surviving sudo without needing -E). Only touches .env if it already exists,
# so a fresh host missing it still fails loudly at PROGRAM_DOCS_HOST_PATH
# below rather than this quietly creating one.
if [ -f .env ]; then
  sed -i '/^CALLAHAN_GIT_COMMIT=/d;/^CALLAHAN_GIT_BRANCH=/d' .env
  {
    echo "CALLAHAN_GIT_COMMIT=$(git rev-parse --short HEAD)"
    echo "CALLAHAN_GIT_BRANCH=$(git rev-parse --abbrev-ref HEAD)"
  } >> .env
fi

sudo docker compose -f docker-compose.prod.yml up -d --build

git rev-parse HEAD > .deployed_sha
echo "Deployed $(cat .deployed_sha) ($(git log -1 --format=%s))"
