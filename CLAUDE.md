# Working in this repo

## Deployment
Deploy is automatic: `.github/workflows/deploy.yml` runs on every push to `main`,
joins the NAS's Tailscale network (`tailscale/github-action@v4`, authenticated via
an OAuth client — `TS_OAUTH_CLIENT_ID`/`TS_OAUTH_SECRET` secrets, tagged `tag:ci` —
which mints a short-lived key per run instead of relying on one long-lived reusable
key that can silently expire, as happened 2026-08-16), then SSHes into the NAS as
`truenas_admin` using a dedicated key (`NAS_DEPLOY_SSH_KEY` secret) that's restricted
via a forced `command=` in `authorized_keys` — it can only ever run
`deploy-wrapper.sh`, nothing else, even if the key leaked. That wrapper runs
`deploy.sh`, which does `git fetch && git checkout main && git reset --hard <ref>`
against `/mnt/tank/callahan`, rebuilds with
`docker compose -f docker-compose.prod.yml up -d --build`, and records the deployed
commit SHA to `.deployed_sha` on the NAS. Note: `docker-compose.yml` (no `.prod`
suffix) also exists — that's the **local-dev** compose file (see README's "Running
locally with Docker"), not used by the NAS at all; don't confuse the two or "clean
up" the plain one thinking it's dead.

**Rollback:** run the `Deploy to NAS` workflow manually from the Actions tab
(`workflow_dispatch`) with a specific commit SHA in the `sha` input; leaving it
blank deploys `origin/main`. `cat .deployed_sha` on the NAS (or `ssh truenas`) shows
what's currently live.

Live at `callahan.ljlab.online` and `REDACTED-LAN-IP:30070`.

## Concurrent work — Callahan-specific
`~/.claude/rules/concurrent-work.md` carries the worktree-per-thread workflow and the
"ask before non-trivial work in the primary checkout" rule. Callahan-specific:

- **Merging to `main` *is* the deploy decision** — deploy is automatic on push. Don't
  merge a thread's work until it's actually meant to go live, even if it's finished and
  reviewed. To ship one thread's work while another's is mid-flight on `main`, use the
  rollback `workflow_dispatch` to pin the deploy to a specific commit rather than letting
  the automatic push-deploy carry both.
- Name worktrees `../Callahan-<short-name>`; `git worktree remove` after the branch ships.

## Commit conventions
Do **not** add a `Co-Authored-By: Claude` trailer to commits in this repo — a
deliberate deviation from the default elsewhere, confirmed 2026-08-12.

## Project docs
Follows the standard 4-file split (`~/.claude/rules/project-workflow.md`). For Callahan
the files live in `~/moxie-vault/30-projects/callahan/` (source of truth
`/mnt/tank/vault/30-projects/callahan/` on the NAS — both safe to write to directly, no
relay through Moxie). When told "update the docs" in a Callahan session with no other
target named, default there, not to repo files (README / CLAUDE.md / code comments).

Do **not** write Callahan project memory into this Claude Code session's own memory
files — the vault is the durable, cross-session home for it. (Recurring lesson in Aug
2026; see the vault's `decisions.md`.)

## Running locally
See README's "Running locally with Docker". `.claude/launch.json` has the dev-server
entry for the browser-preview tools.
