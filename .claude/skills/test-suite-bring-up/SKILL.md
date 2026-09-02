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

## Step 2 — a surviving mutant is a question, not a verdict

Five of the six were caught. The survivor was deleting a floating-point epsilon
from a greedy loop.

The instinct was to write a test for it. That instinct was wrong. A brute-force
sweep of every input at 0.01 resolution across the whole plausible range found
**zero** inputs where the epsilon changed the result — the branch is unreachable
given the data, because every value involved is exactly representable in binary.

So when a mutation survives, establish reachability *before* writing anything:

1. Can any realistic input reach the mutated branch? Sweep the input space if
   it is small enough to brute-force — it usually is for pure logic.
2. If reachable → the test gap is real. Write the test.
3. If unreachable → **do not write a test.** A test for an unreachable branch
   manufactures false coverage: it will pass forever regardless of the code.
   The correct outputs are documentation changes, and possibly deletion of the
   dead guard.

In this case the fixes were: rename the existing test, whose name claimed to
cover floating-point drift when it covered ordinary behaviour, and amend the
source comment that implied the epsilon was load-bearing.

A surviving mutant says the tests and the code disagree about what matters —
and **the code is as likely to be the party in the wrong.**

## Step 3 — check the test names describe what they assert

The renamed test above had been passing and misleading simultaneously. After
mutation-checking, re-read each test's name against what the mutations proved
it actually catches. A name that overstates coverage is worse than no test,
because it stops the next person looking.

## Pre-flight

- [ ] The suite has been watched to **fail**, not only to pass.
- [ ] Mutations covered distinct failure classes, applied one at a time, each
      reverted after.
- [ ] Every surviving mutant was resolved as *reachable → write a test* or
      *unreachable → document/delete*, never assumed to be a missing test.
- [ ] Reachability claims came from an actual input sweep, not from reasoning
      about whether the branch "should" be hit.
- [ ] Test names re-read against what they demonstrably catch.
- [ ] All mutations reverted; `npm test` green; `git diff` on source files is
      empty except for intended changes.
