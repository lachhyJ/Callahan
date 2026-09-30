---
name: code-audit
description: Run a structured, report-first code-health audit of the Callahan repo — dead code, redundancy, refactor candidates, inefficiencies, drift and hygiene — then apply the triaged findings in a worktree. Use when asked to "run the code audit", "audit the repo", "health check", "find dead code across the project", or to repeat/extend a previous audit. Scopes to what changed since the last audit unless told to do the whole repo.
---

# Code audit

A repeatable version of the 2026-09 whole-repo audit (about 150 findings, 50
commits shipped). The phases, evidence rules and verification methods held up;
the lessons at the bottom are what cost time and are now built in. Improve this
file at the end of every audit — that is part of the job, not an extra.

## Scope first

- **Default: incremental.** Audit `git diff <last-audit-end>..HEAD` (see Audit
  history below) plus every finding the last audit left open. Only go
  whole-repo when asked, or when more than ~6 months or ~300 commits have
  passed.
- State the scope and the areas it touches before Phase 1, and skip areas the
  diff doesn't touch.

## Phase 0 — Orient

- Read CLAUDE.md, docs/architecture.md, docs/decisions.md, and the vault's
  Callahan backlog and decisions. Much odd-looking code is deliberate (the two
  compose files, FieldGeometry tuning, the native wrap, the watch). Check
  decisions before calling anything redundant.
- `git worktree list`; set up `../Callahan-code-audit` before any edit. Merging
  to main deploys; nothing merges without an explicit go-ahead.
- Uncommitted files you didn't create are other sessions' work: don't touch or
  report them.

## Phase 1 — Baseline

`scripts/baseline.sh <worktree> [e2e] [watch] [ios]` runs every gate and prints
one line each: backend build and tests, frontend build and tests, oxlint, and
optionally the Playwright suite, the watch build and an iOS simulator build.
Record the output; it is the bar every later batch has to meet. List
pre-existing failures, don't fix them here.

- Kill any backend still running from the worktree before e2e, or Playwright
  reuses a stale server.
- **CI parity:** also run the backend tests with the dev machine's config out of
  the way (or push the branch and let CI run before merging). A test that
  passes only because a local config file supplies a value will fail in CI —
  that happened in 2026-09 (`Auth:Username`).

## Audit categories (every area, every category)

- **Dead code:** unreachable files, unused exports/methods/classes/DTO
  fields/endpoints/CSS, flags never set, unused config keys and dependencies,
  commented-out code, stale TODOs, debug logging.
- **Redundancy:** logic duplicated within or across layers.
- **Refactor candidates:** oversized components/services, prop-drilling,
  hand-rolled versions of what a dependency already provides, inconsistent
  patterns for the same job (errors, fetching, dates, units).
- **Efficiency:** N+1 or over-fetching queries, missing `AsNoTracking`,
  client-side filtering, unbounded results, sync-over-async, redundant polling
  or API calls, needless re-renders, bundle size, image size.
- **Drift:** backlog items already done, public decisions since superseded,
  README or comments that contradict the code, docs pointing at deleted files.
- **Public-repo hygiene:** secrets, hostnames beyond the deliberate ones,
  personal details, vault paths, precise locations in fixtures.
- **Dependencies:** `npm audit`, `dotnet list package --vulnerable`, pinned
  Python packages. Read what a suggested fix version actually contains first.
- **Bugs noticed in passing:** logged separately, never fixed under this task.

## Evidence rules

- Never call something unused from grep alone. Code here is referenced by DI,
  EF config and reflection, route discovery, JSON property names on the wire,
  Swift↔JS bridge names, Monkey C↔backend field names, cron/CI/deploy scripts
  and dynamic imports. Use the js-dead-code-sweep skill for the frontend and
  check the implicit routes above elsewhere. Say what was checked.
- Use claim-check before presenting any root-cause or "this is slow" claim.
  Measure, or show the query or render path.
- Confidence on every finding: CONFIRMED (proved or measured), LIKELY, or
  NEEDS-YOUR-CALL (depends on intent).
- Where a claim depends on production state (is this column populated, does
  anything call this route, is the cron image stale), check it read-only
  against prod rather than guessing. Commands and access notes are in the
  vault's audit folder.

## Do not flag or touch

`backend/Migrations` (append-only; an actively wrong migration is a bug
report), generated files, lockfiles, iOS scaffolding, committed screenshots and
fixtures, the local-dev `docker-compose.yml`, the wording of `docs/`, and
style-only nitpicks.

## Finding format — one table row each

| ID | Conf | Where | Problem (one line) | Fix (one line) | Risk / how verified | Effort | Default |

`Default` is the recommended triage answer (take / skip / ask). Only findings
that need a decision get a paragraph underneath. Keep each area file under
~400 lines; the 2026-09 summary was too large to read in one call.

## Phase 2 — Audit by area

Areas: backend; frontend/src and its config; frontend/ios and the JS↔native
bridge; watch and its backend contract; scripts, deploy, workflows, compose;
tests and e2e. Skip areas outside the scope.

- One background subagent per area, each prompt self-contained (categories,
  evidence rules, confidence levels, do-not-touch list, finding format, the
  Phase 0 facts and Phase 1 result for that area). A smaller model is enough
  for the sweep. Agents read whole files, stay read-only, write to the vault's
  audit folder, and return a ~10-line summary.
- Expect rate limits: launch them, then resume any that stop, rather than
  restarting.
- Then do the cross-boundary pass in the main thread: contract drift between
  backend DTOs and the frontend, watch and script consumers; duplication across
  layers; "unused" claims another area contradicts. Spot-check two CONFIRMED
  "unused" claims per area and downgrade any that fail.

## Phase 3 — Deliverable, then stop

Reports go in the vault, not the repo: `30-projects/callahan/audits/<YYYY-MM>-code-audit/`
(`code-audit.md` summary plus one file per area). They name prod details and
don't belong in the public repo.

The summary holds: counts by category, lines removable, the findings table
ranked by value ÷ risk, quick wins (CONFIRMED safe deletions), bugs noticed,
deliberately left alone and why, and open questions. Then a short chat summary
that asks for triage as a checklist ("all defaults except …"). Wait.

## Phase 4 — Apply

- In the worktree, sequentially, batched by theme, one logical change per
  commit. Re-run the baseline after each batch; revert a batch that regresses.
- Refactors are behaviour-preserving, and proven so:
  - API or report output: `scripts/devbackend.sh` + `scripts/dump-api.sh` before
    and after, then `diff` (ids normalised).
  - Pure components: render to static markup before and after and diff.
  - Migrations: apply to a prod copy and to a fresh database.
  - New tests: mutation-check them (test-suite-bring-up). Delete a test that
    can't fail.
  - UI: load the screen at 440x956 with real data; e2e runs at zero tolerance.
- Commits: short subject, body only when the why isn't in the diff, via
  `git commit -F`.
- Merge only on an explicit go-ahead. Push the branch and let CI go green
  first. After the merge, watch the deploy to green, fast-forward the primary
  checkout's `main`, and remove the worktree and branch.
- Remaining bugs go to a fresh thread via a written prompt that points at the
  vault reports.
- Offer the docs pass: session-log entry, backlog (close and re-audit related
  items), vault decisions, and candidates for docs/decisions.md.
- Finish by updating Audit history and Lessons below.

## Lessons from past audits

- **2026-09:** a 100px e2e tolerance was hiding a missing Back button and a
  stale baseline — tolerance sized for date noise hides small real diffs. Pin
  the clock instead (now done).
- **2026-09:** prose triage took two long messages; hence the `Default` column.
- **2026-09:** the helper scripts lived in a scratchpad and were nearly lost;
  they are in `scripts/` now. Improve them there.
- **2026-09:** auto mode may refuse a large merge to main as a production
  deploy; the push allow rule in `.claude/settings.local.json` covers it. If the
  permission check itself is erroring, do read-only work and retry later.
- **2026-09:** shipping from a worktree leaves the primary checkout's `main`
  behind; fast-forward it after the merge.

## Audit history

| Audit | Scope | End commit | Reports |
|---|---|---|---|
| 2026-09 | whole repo | `bcde2ae` | vault `audits/2026-09-code-audit/` |
