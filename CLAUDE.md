# Working in this repo

## Deployment
Deploy is manual, not CI/CD: the NAS at `/mnt/tank/callahan` is a git clone that gets
`git pull`'d and rebuilt (Docker Compose) by hand. Live at `callahan.ljlab.online` and
`REDACTED-LAN-IP:30070`. Nothing redeploys automatically on push — a change only reaches
the phone once it's on GitHub `main` *and* someone has manually redeployed the NAS.

## Multiple threads / concurrent work
If more than one Claude Code thread (or worktree) is working on this repo at once,
each one must work on its own branch — never commit straight to a shared local `main`
that another thread might also be using as a base. Merge to `main` and push
**immediately** when a thread's work is done; don't leave finished commits sitting
local-only, since the next thread to branch off `main` will silently inherit them.

Full PR review isn't required for routine changes — this is a solo project. Reach for
a branch + PR specifically when you want a second pair of eyes before something goes
live, not as default ceremony.

## Commit conventions
Do **not** add a `Co-Authored-By: Claude` trailer to commits in this repo — this is a
deliberate deviation from the default, confirmed 2026-08-12.
