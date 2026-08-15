#!/bin/bash
# Forced-command target for the GitHub Actions deploy key (see authorized_keys).
# SSH_ORIGINAL_COMMAND carries the ref to deploy: empty/unset for a normal
# push-to-main deploy, or a full commit SHA for a manual rollback dispatch.
set -euo pipefail

REF="${SSH_ORIGINAL_COMMAND:-}"

if [[ -z "$REF" ]]; then
    REF="origin/main"
elif ! [[ "$REF" =~ ^[a-fA-F0-9]{7,40}$ ]]; then
    echo "Rejected ref: $REF" >&2
    exit 1
fi

exec /mnt/tank/callahan/deploy.sh "$REF"
