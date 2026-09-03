# Working in this repo

## Deployment
Deploy is automatic: `.github/workflows/deploy.yml` runs on every push to `main`,
joins the NAS's Tailscale network (`tailscale/github-action@v4`, authenticated via
an OAuth client — `TS_OAUTH_CLIENT_ID`/`TS_OAUTH_SECRET` secrets, tagged `tag:ci` —
which mints a short-lived key per run instead of relying on one long-lived reusable
key that can silently expire, as happened 2026-08-16), then SSHes into the NAS as the
deploy user using a dedicated key (`NAS_DEPLOY_SSH_KEY` secret) that's restricted
via a forced `command=` in `authorized_keys` — it can only ever run
`deploy-wrapper.sh`, nothing else, even if the key leaked. That wrapper runs
`deploy.sh`, which does `git fetch && git checkout main && git reset --hard <ref>`
against the NAS checkout (path in `deploy.sh`), rebuilds with
`docker compose -f docker-compose.prod.yml up -d --build`, and records the deployed
commit SHA to `.deployed_sha` on the NAS. Note: `docker-compose.yml` (no `.prod`
suffix) also exists — that's the **local-dev** compose file (see README's "Running
locally with Docker"), not used by the NAS at all; don't confuse the two or "clean
up" the plain one thinking it's dead.

**Rollback:** run the `Deploy to NAS` workflow manually from the Actions tab
(`workflow_dispatch`) with a specific commit SHA in the `sha` input; leaving it
blank deploys `origin/main`. `cat .deployed_sha` in the NAS checkout shows what's
currently live.

Live at `callahan.ljlab.online`.

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
the files live in the personal notes vault, not in this repo — the exact location is in
the machine-level `~/.claude/CLAUDE.md`. When told "update the docs" in a Callahan
session with no other target named, default there, not to repo files (README /
CLAUDE.md / code comments).

### Public docs in the repo (`docs/`)
`docs/decisions.md` and `docs/architecture.md` are a **curated public mirror**, not a
second source of truth. The vault stays authoritative; the repo carries the subset a
stranger reading the code would benefit from.

- **Write to the vault first, always.** A decision is recorded in the vault's own
  `decisions.md` as it happens, in the existing session-batched format. Nothing changes
  about that cadence.
- **Promotion to `docs/decisions.md` is a separate, deliberate pass** — offer it at the
  same checkpoints as the vault update, naming the specific entry ("the FieldGeometry
  retune is worth promoting too — want that in the same pass?"). Never promote silently:
  it's public, and the wording should be seen before it ships.
- **The bar is quality, not a quota.** Promote an entry only if all three hold: it turns
  on a **real tradeoff** with a non-obvious answer; it's **legible without the session**
  around it; and it involves no NAS paths, LAN IPs, host accounts, secret names, token
  file locations, or personal-vault references. Most vault entries fail at least one — a
  shipped-a-thing entry ("calendar shape-coding shipped for Ultimate only") is a vault
  entry, not a public one. Some months that's four promotions, some months zero; don't
  manufacture one to keep the file moving, and don't withhold a good one to keep it
  small. A reviewer forms an impression from the *median* entry, so dilution costs more
  than absence.
- **Progress is shown by git history and the dates already on the entries, not by the
  file growing.** But a file whose newest entry is a year old reads as abandoned — if a
  genuinely interesting decision goes unpromoted for months, that's a miss, not
  restraint.
- **The repo file is topic-grouped, the vault file is date-grouped.** A promoted entry
  goes under the matching `##` topic heading (Stack and architecture, Data modelling and
  storage, Measuring a sport from GPS, Metric design, Security and auth, Testing and
  verification, Deployment and operations, The native iOS wrap) — never appended as a new
  dated section. Add a new topic heading only if an entry genuinely fits none.
- **Rewrite, don't copy.** Public entries are second person / first person singular ("I"),
  never "Lachlan"; carry no `[[wikilinks]]`; drop the `**Decision:** / **Reason:** /
  **How to apply:**` label scaffolding in favour of prose with a bolded date opener; and
  add the one sentence of context the vault version assumes. A straight paste is the
  failure mode to avoid.
- **When a promoted decision is later reversed or superseded, update the repo entry too** —
  a stale public rationale is worse than an absent one. Check `docs/decisions.md` for a
  matching entry whenever you write a superseding one to the vault.
- **`docs/architecture.md` changes only on structural change** — a new container, a new
  backend service worth naming, a data-model shape change. Not on feature work.
- Same confirm-before-writing rule as the vault files applies.

Do **not** write Callahan project memory into this Claude Code session's own memory
files — the vault is the durable, cross-session home for it. (Recurring lesson in Aug
2026; see the vault's `decisions.md`.)

## Running locally
See README's "Running locally with Docker". `.claude/launch.json` has the dev-server
entry for the browser-preview tools.
