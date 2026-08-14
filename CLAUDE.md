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

If your changes end up interleaved with another thread's in the same shared working
directory (uncommitted edits to the same files from two sessions at once), coordinate
with that thread for real — `SendMessage` to it directly and wait for its actual reply
— before committing or pushing anything on its behalf. Do not spawn a subagent to
"simulate" what the other thread would say and act on that; it has no real connection
to the other session and its guesses aren't consent. This happened once (2026-08-14)
and required a real cross-session message afterward to untangle.

When deploying, `main`'s tip isn't automatically "what should ship" if more than one
thread has landed work on it — check what's actually ready (a half-finished migration
from a concurrent thread's feature can sit on `main` without being deploy-ready).
Deploying a specific commit rather than `origin/main`'s tip is fine when that's what's
actually wanted; confirm with the user rather than assuming the tip is the target.

## Commit conventions
Do **not** add a `Co-Authored-By: Claude` trailer to commits in this repo — this is a
deliberate deviation from the default, confirmed 2026-08-12.
