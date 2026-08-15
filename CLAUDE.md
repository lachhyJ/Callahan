# Working in this repo

## Deployment
Deploy is automatic: `.github/workflows/deploy.yml` runs on every push to `main`,
joins the NAS's Tailscale network (`tailscale/github-action`, ephemeral auth key in
the `TS_AUTH_KEY` secret), then SSHes into the NAS as `truenas_admin` using a
dedicated key (`NAS_DEPLOY_SSH_KEY` secret) that's restricted via a forced
`command=` in `authorized_keys` — it can only ever run `deploy-wrapper.sh`, nothing
else, even if the key leaked. That wrapper runs `deploy.sh`, which does
`git fetch && git checkout main && git reset --hard <ref>` against
`/mnt/tank/callahan`, rebuilds with `docker compose -f docker-compose.prod.yml up -d
--build` (`docker-compose.yml` — the dev-era file — was removed; `.prod.yml` is the
only compose file that exists now), and records the deployed commit SHA to
`.deployed_sha` on the NAS.

Because merging to `main` now means "this goes live within moments," the merge
itself is the real gate — see the concurrent-thread notes below for what that
implies with more than one thread active.

**Rollback:** run the `Deploy to NAS` workflow manually from the Actions tab
(`workflow_dispatch`) with a specific commit SHA in the `sha` input; leaving it
blank deploys `origin/main`. `cat .deployed_sha` on the NAS (or `ssh truenas`) shows
what's currently live.

Live at `callahan.ljlab.online` and `192.168.1.150:30070`.

## Multiple threads / concurrent work
If more than one Claude Code thread (or worktree) is working on this repo at once,
each one must work on its own branch — never commit straight to a shared local `main`
that another thread might also be using as a base. Merge to `main` and push
**immediately** when a thread's work is done; don't leave finished commits sitting
local-only, since the next thread to branch off `main` will silently inherit them.

Full PR review isn't required for routine changes — this is a solo project. Reach for
a branch + PR specifically when you want a second pair of eyes before something goes
live, not as default ceremony.

If your changes end up interleaved with another thread's in the same shared working
directory (uncommitted edits to the same files from two sessions at once), coordinate
with that thread for real — `SendMessage` to it directly and wait for its actual reply
— before committing or pushing anything on its behalf. Do not spawn a subagent to
"simulate" what the other thread would say and act on that; it has no real connection
to the other session and its guesses aren't consent. This happened once (2026-08-14)
and required a real cross-session message afterward to untangle.

Since deploy is now automatic on push (see Deployment above), merging to `main` *is*
the deploy decision — don't merge a thread's work until it's actually meant to go
live, even if it's otherwise finished and reviewed. If you need to ship one thread's
work while another's is mid-flight on `main`, use the rollback workflow_dispatch to
pin the deploy to a specific commit rather than letting the automatic push-deploy
carry both.

## Commit conventions
Do **not** add a `Co-Authored-By: Claude` trailer to commits in this repo — this is a
deliberate deviation from the default, confirmed 2026-08-12.
