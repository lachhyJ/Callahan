---
name: claim-check
description: Check the claims a piece of work rests on, and the claims it produces, before acting on either. Use when a plan or backlog item asserts something about this codebase that heavier work depends on ("this needs a migration", "extend the existing script"); when running a parameter sweep or diagnostic over the Ultimate stream data; when tuning anything against the committed fixtures; and always before presenting a measured number or root cause as a finding. Trigger on "why is X wrong", "diagnose", "sweep", "tune", "root cause", or any conclusion about the app derived from a measurement rather than read directly.
---

# Claim Check

Internal skill for the Callahan repo. Not for publication.

It exists because five separate sessions in Aug 2026 hit the same failure:
a confident, plausible result that rested on an input nobody checked. The
worked examples below are the real incidents, kept concrete on purpose —
generic versions of these rules were already written down and got violated
anyway.

## The shape of the failure

An investigation produces a number. It's striking, internally consistent,
and matches what Lachlan already suspected. It gets reported as the
mechanism. It's wrong, because one of its inputs was never checked.

Nothing about the output looks wrong. A broken sweep and a genuine null
result print the same thing. A metric measuring the wrong component and a
metric confirming the hypothesis read the same. There's no downstream check
that catches this, which is why it has to happen at the inputs.

And it's most likely exactly when the result is most satisfying — a finding
that contradicts everyone gets scrutinised by reflex; one that confirms the
stated hunch gets reported.

## Phase 1 — claims arriving from a plan or backlog item

Before starting, list every claim about the current state of the code that
heavier work depends on, and open the actual file to confirm each one. A
plan written in an earlier session, or a `backlog.md` bullet, is a
hypothesis about the repo — not a fact about it, however carefully it was
shaped.

**Ceremony inflation.** A backlog item read "Persist rest duration per
exercise (schema change)" and invoked the backup-the-NAS-db-first
convention. `WorkoutTemplateExercise.RestSeconds` already existed and was
already migrated — the real gap was a missing write-back endpoint, a copy
of the existing `UpdateCue` pattern. Reading the model first would have
dropped the task a whole risk tier. So: any item claiming a migration,
schema change, or db backup gets checked against `backend/Models/` and
`backend/Migrations/` **before** the heavy path is invoked, never after.

**Name-based role inference.** An approved plan said to extend
`scripts/ultimate-stream-explore/explore.py` as "the existing offline
sandbox." That file is the *abandoned* speed-thresholding attempt, kept
deliberately as a record of why it failed; the live sandbox is
`segment.py`. The plan was read and approved with the wrong file named.
`scripts/ultimate-stream-explore/` is exactly the kind of directory that
retains dead attempts alongside live code — and the dead one often has the
more obvious name. Read the directory README, or the head of the file,
before naming any file as the thing to modify.

## Phase 2 — while measuring

**Pass swept parameters explicitly.** A sweep helper was written as
`def report(label, **kwargs)` and called as `report("WIN=80")` — the value
lived only in the label string and the function ran on its defaults. Pass
sweep values as positional or dict arguments to the function under test,
not as prose in a label.

**Identical rows are a bug until proven otherwise.** Four byte-identical
sweep rows are what exposed that one. That signal only appears because the
fixture data is clean; on noisier data the same broken harness produces
rows that differ slightly, which reads as a real small effect.

**"No effect" needs its plumbing checked hardest**, because it's the result
that requires no follow-up work to accept. A finding that demands more
investigation gets stress-tested by the investigation. A null result ends
the inquiry.

## Phase 3 — before reporting anything measured

Write this out. Don't do it in your head — the whole point is that the
mental version doesn't survive the moment of having an interesting result.

```
CLAIM CHECK
Claim:             [the finding, one sentence]
Inputs:            [every component, dataset, measurement it depends on]
Weakest input:     [which one — and is it already flagged as unreliable
                   in backlog.md or the session log?]
Can it separate?   [does this distinguish the component being blamed from
                   its weakest input's failure mode?]
Evidence held out: [what was this checked against that it wasn't tuned on?]
Prior match:       [does it match what Lachlan already said? if so, what
                   extra check did that trigger?]
Verdict:           [finding | hypothesis | needs more work]
```

**Can it separate?** — the sharpest line. A metric built to show the
on/off-field labeller was over-counting measured "labeller output during
windows where the point detector found no events." The point detector was
*already documented in `backlog.md` as the unreliable part*. So the metric
couldn't tell "labeller over-counts" from "detector under-detects" — it
just blamed whichever one was already under suspicion. It produced a clean
67%→48% number that landed exactly on the stated hunch, and Lachlan
refuted it in one line from domain knowledge ("halftime is ~5 minutes,
that 27-minute gap is multiple points"). When investigating component A, a
metric depending on component B measures the pair. If the answer here is
no, the verdict cannot be "finding".

**Evidence held out** — parameters were swept against the same six
committed fixtures in `tests/Callahan.Api.Tests/Fixtures`, with "these
numbers look more plausible now" as the acceptance bar. Eleven comparable
unseen games existed in production data and were only pulled in after
Lachlan pushed back; that's what `holdout_check.py` now exists for. Before
sweeping anything: inventory what data exists, reserve a split, and say
what the split is. Plausibility improves with tuning effort regardless of
correctness — a fit is not a validation.

**Prior match** — if the result agrees with what he already said, and the
measure was built after hearing it, that's not corroboration; the measure
was built by someone who knew the target. Name the extra check the match
earned. Agreement is a reason to look harder.

**Verdict** — report as hypothesis unless every line is clean. "One
candidate explanation, here's what would confirm it" costs a sentence.

## Decide what counts as evidence first

At the start of an empirical task, write one short paragraph: what data
exists, what's being held out, what result would falsify the hypothesis.
Deciding this after the result is questioned is too late — by then every
case that could have been held out has been seen.

## Before delivering

Re-read this file and confirm:

1. Every claim that pulled in the heavier path (migration, db backup) was
   checked against the live model, not the plan's description of it.
2. Every file named as the thing to modify was opened, or its README read.
3. Sweeps passed their parameter explicitly; identical rows were treated
   as a bug first.
4. The Phase 3 block was written out, not performed mentally.
5. Anything matching Lachlan's prior names the extra check it got.
6. Anything with an unclean gate is framed as a hypothesis.

If one is unmet, fix it before delivering, and say what was missed.
