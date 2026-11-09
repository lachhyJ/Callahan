---
name: test-suite-bring-up
description: Validate a new or freshly-written test suite before trusting it, by mutation-checking — introduce known breakages and confirm each is caught. Use when standing up a test harness for the first time, adding tests to code written in the same session, when a suite passes green on the first run, or when asked whether tests actually cover something. Trigger on "add tests", "test harness", "all tests pass", "is this covered", "mutation", "why did this bug ship with tests passing".
---

# Test Suite Bring-Up

Internal skill for the Callahan repo. Not for publication.

Runner: `npm test` in `frontend/` (vitest, pinned to `TZ=Australia/Melbourne`
— keep the TZ, several date tests depend on it).

## Why this exists

63 new tests passed on the first run, against code written earlier in the same
session. That is not evidence the code works. **A green suite on the first
attempt is equally consistent with the tests asserting nothing** — and tests
written by the same author, in the same sitting, against the same mental model
as the code, are exactly the case where a whole assertion can be vacuous
without looking wrong.

## Step 1 — mutation-check

Introduce known breakages **one at a time**, re-run the suite against each, and
confirm each is caught. Six is a reasonable number for a new suite. Pick
mutations that target different failure classes:

- flip a constant
- revert a function to its known-buggy predecessor (git history is the source)
- change a boundary comparison (`<` → `<=`)
- delete a guard or early return
- negate a condition
- drop a rounding / clamping step

Revert each mutation before introducing the next. Record which mutation each
test caught — a test that catches none of them is the finding.

**Revert at edit level, not to HEAD, when the code under test is uncommitted.**
`git checkout <file>` / `git restore <file>` resets to HEAD — which, mid-feature,
silently deletes the very code you were testing (seen: two new exported helpers
vanished, and the "restored" run showed 6 failures from tests importing them).
Reverse the specific edit, or `git stash` a snapshot just before mutating and
pop it after. Then re-run and confirm the suite returns to the **exact**
pre-mutation pass count, not just "mostly green".

## Step 2 — a surviving mutant is a question, not a verdict

Five of the six were caught. The survivor was deleting a floating-point epsilon
from a greedy loop.

The instinct was to write a test for it. That instinct was wrong. A brute-force
sweep of every input at 0.01 resolution across the whole plausible range found
**zero** inputs where the epsilon changed the result — the branch is unreachable
given the data, because every value involved is exactly representable in binary.

So when a mutation survives, derive the input set on which the mutated and
original code actually diverge *before* writing anything:

1. What inputs make the mutant and the original produce different results?
   Sweep the input space if it is small enough to brute-force — it usually is
   for pure logic.
2. If that set is empty → the branch is unreachable given the data. **Do not
   write a test.** A test for an unreachable branch manufactures false
   coverage: it will pass forever regardless of the code. The correct outputs
   are documentation changes, and possibly deletion of the dead guard.
3. If that set is non-empty → those exact inputs are the missing test — and
   they are disproportionately likely to be a **real bug**, because by
   construction they are the cases nobody thought about. Run them through the
   *original* code and check the output is actually what it should be before
   assuming the mutant is the only thing wrong.

**The near-duplicate-guard trap.** When a mutant deletes one of two guards
that look redundant (an emptiness check next to a validity check), they
usually diverge on exactly one sentinel: `null` vs `NaN`, zero vs absent,
`""` vs undefined. Seen here: deleting a `!saved.startedAt` emptiness guard
survived because a sibling Invalid-Date guard caught the same test inputs —
but `new Date(null)` is the epoch, a *valid* date the NaN guard passes, so a
null value would have silently pinned the record to 1970 and a monotonic
"earliest wins" rule would have kept it there permanently. The surviving
mutant was pointing at an untested input class, not at dead code.

In the epsilon case the fixes were: rename the existing test, whose name
claimed to cover floating-point drift when it covered ordinary behaviour, and
amend the source comment that implied the epsilon was load-bearing.

A surviving mutant marks the boundary where two pieces of logic disagree with
nobody watching — and **the code is as likely to be the party in the wrong.**

**The fixture can also be what's wrong: a guard is only tested where removing
it changes the outcome.** A program-section finder used a heading regex that
required a dash, to skip a stub heading. Loosening the regex left every test
green: the fixture put the real section before the stub and the finder takes
the first match, so the guard never decided anything. A second test with the
stub *first* killed the mutant. When a guard exists to reject a decoy, put
the decoy where it would win without the guard (first in order, winning the
tie, matching the default). If such a mutant survives, fix the fixture before
deriving a divergence set.

## Step 3 — check the test names describe what they assert

The renamed test above had been passing and misleading simultaneously. After
mutation-checking, re-read each test's name against what the mutations proved
it actually catches. A name that overstates coverage is worse than no test,
because it stops the next person looking.

## Step 4 — when you make a suite deterministic, re-prove the gate

Removing a noise source (pinning the backend clock, seed, browser clock and
timezone) expires every tolerance that was set to absorb it. Seen here: the
visual suite passed 22/22 after a change that added a Back button to one
screen, because a 100 px absolute tolerance (set months earlier for a drifting
suite) hid the ~80 px difference. A zero-tolerance run also found a stale
baseline whose streak digit differed by 4 px: `--update-snapshots` only
rewrites snapshots that *fail*, and that one was passing on tolerance.

1. Mutation-check the gate itself: make a small deliberate visual change and
   confirm the suite fails.
2. Re-measure the noise floor at zero tolerance over 2+ runs and set the
   tolerance from that measurement, not from history (here it was exactly 0 px).
3. Regenerate baselines with `--update-snapshots=all` so no pre-fix baseline
   survives on tolerance.

## Pre-flight

- [ ] The suite has been watched to **fail**, not only to pass.
- [ ] Mutations covered distinct failure classes, applied one at a time, each
      reverted after.
- [ ] Every surviving mutant was resolved by deriving its divergence input
      set: *empty → document/delete*, *non-empty → those inputs are the test,
      and were checked against the original code for a real bug first*.
- [ ] The divergence set came from an actual input sweep, not from reasoning
      about whether the branch "should" be hit. Near-duplicate guards were
      checked for the one sentinel they split on (null/NaN, zero/absent).
- [ ] Every decoy-rejecting guard has a fixture where the decoy would win
      without it (order, ties, defaults).
- [ ] Test names re-read against what they demonstrably catch.
- [ ] If a noise source was removed, the gate was shown to fail on a small
      deliberate change, and tolerance was re-measured at zero.
- [ ] All mutations reverted; `npm test` green; `git diff` on source files is
      empty except for intended changes.
