# Working in this repo

## Deployment
Deploy is automatic: `.github/workflows/deploy.yml` runs on every push to `main`,
joins the NAS's Tailscale network (`tailscale/github-action@v4`, authenticated via
an OAuth client — `TS_OAUTH_CLIENT_ID`/`TS_OAUTH_SECRET` secrets, tagged `tag:ci` —
which mints a short-lived key per run instead of relying on one long-lived reusable
key that can silently expire, as happened 2026-08-16), then SSHes into the NAS as
`truenas_admin` using a
dedicated key (`NAS_DEPLOY_SSH_KEY` secret) that's restricted via a forced
`command=` in `authorized_keys` — it can only ever run `deploy-wrapper.sh`, nothing
else, even if the key leaked. That wrapper runs `deploy.sh`, which does
`git fetch && git checkout main && git reset --hard <ref>` against
`/mnt/tank/callahan`, rebuilds with `docker compose -f docker-compose.prod.yml up -d
--build`, and records the deployed commit SHA to `.deployed_sha` on the NAS. Note:
`docker-compose.yml` (no `.prod` suffix) also exists — that's the **local-dev**
compose file (see README's "Running locally with Docker"), not used by the NAS at
all; don't confuse the two or "clean up" the plain one thinking it's dead.

Because merging to `main` now means "this goes live within moments," the merge
itself is the real gate — see the concurrent-thread notes below for what that
implies with more than one thread active.

**Rollback:** run the `Deploy to NAS` workflow manually from the Actions tab
(`workflow_dispatch`) with a specific commit SHA in the `sha` input; leaving it
blank deploys `origin/main`. `cat .deployed_sha` on the NAS (or `ssh truenas`) shows
what's currently live.

Live at `callahan.ljlab.online` and `REDACTED-LAN-IP:30070`.

## Multiple threads / concurrent work
If more than one Claude Code thread (or worktree) is working on this repo at once,
each one must work on its own branch — never commit straight to a shared local `main`
that another thread might also be using as a base. Merge to `main` and push
**immediately** when a thread's work is done; don't leave finished commits sitting
local-only, since the next thread to branch off `main` will silently inherit them.

Full PR review isn't required for routine changes — this is a solo project. Reach for
a branch + PR specifically when you want a second pair of eyes before something goes
live, not as default ceremony.

**Uncommitted files you didn't touch are normal — don't report them.** With more than
one thread active, `git status` will routinely show modified and untracked files
belonging to another session. That is the expected steady state, not a problem, and it
does not need flagging in every status update. Before committing, check only whether
the other thread's files *overlap with the ones you edited*; if they don't, stage your
own paths explicitly (`git add <path>`, never `git add -A`) and carry on without
mentioning theirs. Only raise it when there's real overlap — that's the case the next
paragraph covers.

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

## Vault documentation
Project history/context for Callahan lives in `~/moxie-vault/30-projects/callahan/`
(source of truth `/mnt/tank/vault` on the NAS — both are safe to write to directly,
no relay through Moxie needed), split into four files as of 2026-08-16. Write to the
right one — don't let everything collapse back into one doc:
- `overview.md` — short always-loaded summary (current state, stack, build-status
  checklist). Keep this trimmed; it links to the other three.
- `session-log.md` — dated narrative of what happened, newest entry first. Add one
  after any session with real work, whether or not anything shipped.
- `backlog.md` — open/deferred items and the phase roadmap. Anything flagged as
  "not now," "later," or "out of scope for this session" goes here as its own
  bullet — not as a sentence buried inside a session-log entry.
- `decisions.md` — architecture/process decisions with reasoning (why X over Y),
  matching the homelab vault's `60-reference/decisions.md` style.

Do **not** write Callahan project memory into this Claude Code session's own memory
files — the vault is the durable, cross-session home for this, not
`~/.claude/projects/.../memory/`.

**Ordering:** push finished code to `main` (and confirm the deploy landed, per
Deployment above) *before* writing the vault entry for that session, not after —
otherwise the doc ends up describing work as "shipped" while it's still sitting
uncommitted or unpushed locally. This happened once (2026-08-26: security-hardening
work was written up in the vault as shipped while three files were still
uncommitted on the working tree).

**Build now vs. park it:** when a session is shaping a batch of loosely-related small
items (a list of papercuts, "a couple of things to plan out"), ask explicitly at
plan-finalisation time whether the goal is to build them now or just to have them
recorded for later, rather than assuming build and finding out otherwise after a full
implementation plan is already written. If the answer is "park it," write each item to
`backlog.md` as its own bullet (per the split above) — not Claude's own session memory,
which isn't durable across sessions and isn't where Callahan context belongs. This
happened once (2026-08-25: four shaped UI papercuts were rejected for building after a
full plan was presented, and the plan had to be rewritten from "how to build" into "how
to record" — a one-line question up front would have avoided the rework).

## Verifying UI in the browser preview
The preview's screenshot can be shorter than the page's layout viewport — at the
default desktop preset the page laid out at 720px tall while the capture was only
450px. Anything pinned to the bottom (`position: fixed; bottom: 0` — the tab bar, the
rest bar) is then correctly positioned and simply cropped out of the picture. This
cost several minutes of suspecting the CSS on 2026-08-25 when nothing was wrong.

So: for fixed or sticky bottom-anchored elements, ask the page where things are
(`getBoundingClientRect` / `getComputedStyle` via `javascript_tool`) and treat that as
the source of truth; use a screenshot to confirm afterwards, not to diagnose. **If the
screenshot and the computed geometry disagree, suspect the capture before the CSS.**

After `resize_window` (e.g. to the mobile preset), pixel-coordinate clicks became
unreliable — re-run `read_page`/`find` and click by `ref` instead of raw coordinates.
