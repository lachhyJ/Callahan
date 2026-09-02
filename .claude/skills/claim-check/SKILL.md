---
name: claim-check
description: Check the claims a piece of work rests on, and the claims it produces, before acting on either. Use when a plan or backlog item asserts something about this codebase that heavier work depends on ("this needs a migration", "extend the existing script"); when running a parameter sweep or diagnostic over the Ultimate stream data; when tuning anything against the committed fixtures; when asked whether a displayed metric is meaningful or indicates a problem; when verifying that a migration, backup, or data repair preserved state; and always before presenting a measured number or root cause as a finding — including a negative one ("not worth it") or a retraction. Trigger on "why is X wrong", "diagnose", "sweep", "tune", "root cause", "is this stat useful", "did this change anything", or any conclusion about the app derived from a measurement rather than read directly.
---

# Claim Check

Internal skill for the Callahan repo. Not for publication.

It exists because many sessions across Aug–Sep 2026 hit the same failure: a
confident, plausible result that rested on an input nobody checked. The
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

**Claims in a plan Claude just wrote.** A rev-2 plan, written and approved
in the same session, asserted FrisScore "has no project-docs setup." It had
a deliberate 3-file convention with a `session-template.md` saying "Do not
create new files." Executing as written would have overridden a standing
instruction; it was caught only because the executor listed the target
directory first. Authorship does not upgrade a claim to a fact. Before any
step that creates, overwrites, deletes, or restructures a target, re-open
that exact target and confirm the plan's stated precondition ("doesn't
exist", "is empty", "is stale") still holds. The plan→execute boundary is
the checkpoint; "I just wrote this" is not verification.

**Symptoms from a stored note.** A rest-timer session opened with a
detailed push-subscription hypothesis built off a backlog item written
weeks earlier. By then the push notification arrived fine — the real
problem was only the in-app beep, and the whole first analysis aimed at the
wrong symptom. Restate the *current* observed behaviour with Lachlan before
diagnosing: "the notes say X is the problem — is that still exactly what
you're seeing?" A stored problem description is a hypothesis about the past.

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

**Prove the target — which copy am I talking to?** One session had a dev
server, a docker stack, and a prod deploy alive at once. Three near-misses,
one root cause: a dying dev server still held a port a container had just
claimed; the dev-server launcher resolved against the primary checkout
after the cwd moved to a worktree, so three pages were "verified" against
the previous commit; a screenshot caught a loading state the network panel
showed was fine. Before trusting any behavioural check, establish the
artefact under test is the one running — grep the served bundle for a
string literal only the new code has, `pgrep -lf` the process path, `lsof`
the port, compare a build version id. Once per environment, not once per
assertion. Pair this with "suspect the capture" for screenshots.

**Record the status code next to the number.** A timing-oracle check sent
12 login requests per variant; both medians came back at 2 ms — a clean
pass. The endpoint is rate-limited to 5/min, so requests 6–12 were 429s and
the median landed on those. It only unravelled because 2 ms was checked
against an independent expectation: bcrypt at the configured cost is
~143 ms, and a "pass" an order of magnitude faster than its own dominant
operation cannot be real. When a measurement is the evidence, log the
status / success indicator beside the metric and state the expected
magnitude *before* measuring. Security middleware — rate limiters, caches,
circuit breakers — returns fast, uniform, meaningless numbers.

**Compare in the native representation.** A TEXT→REAL migration was gated on
proving no stored value changed. Four successive checks each said values
*had* changed; every one was an artefact of moving data through text to
compare it (CLI rendering at 15 s.f., printf capping precision, printf on
NULL returning "0"). The failure count moved 188 → 107 → 39 as each
artefact was removed — which was itself the tell. Settled by comparing
inside the engine: attach the pre-migration copy, join on primary key,
NULL-safe compare. Zero differences across 2095 values. Never verify a data
transformation by exporting both sides to text and diffing; every
serialisation boundary is a false positive waiting to happen. A shrinking
discrepancy count means the instrument is the variable.

**Backups are a claim too.** Before seeding test rows, a backup was taken
with `cp db.sqlite backup`. WAL mode plus a live server meant `-wal` and
`-shm` weren't copied; restoring the main file under the newer WAL produced
`database disk image is malformed` — more destructive than the mutation it
was meant to undo. Recovery was luck (the damaged B-tree was a cache
table). For any datastore with sidecar files or an active writer, use the
engine's own backup (`sqlite3 db ".backup 'file'"` / `VACUUM INTO`), and
open the backup and integrity-check it before relying on it. An unverified
backup turns a reversible action irreversible while looking like the
opposite.

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
Instantiated?      [ran real values through the claim and watched the
                   output move the way the claim says?]
Converse checked?  [looked for records that fit the precondition but not
                   the predicted symptom, and explained each?]
Verdict:           [finding | hypothesis | needs more work]
```

**Can it separate?** — the sharpest line. A metric built to show the
on/off-field labeller was over-counting measured "labeller output during
windows where the point detector found no events." The point detector was
*already documented in `backlog.md` as the unreliable part*. So the metric
couldn't tell "labeller over-counts" from "detector under-detects" — it
just blamed whichever one was already under suspicion. It produced a clean
67%→48% number that landed exactly on the stated hunch, and Lachlan refuted
it in one line from domain knowledge ("halftime is ~5 minutes, that
27-minute gap is multiple points"). When investigating component A, a
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

**Instantiate it.** A claim that a fixed narrow rep range makes an
estimated-1RM metric redundant was presented as evidence-backed — the rep
ranges had been queried and confirmed. The premise was right and the
inference was backwards: a fixed rep *range* is the signature of double
progression, where reps climb between load increments, so the raw value is
flat while the normalised one moves. One worked example — same load, reps
3→8 — would have shown it, and the data to build one was already loaded.
Verifying the inputs made the reasoning feel checked when it wasn't. Before
stating any "metric X won't capture Y here", run two or three values the
system would really produce through the formula and watch which way the
output goes. Data verification and reasoning verification are separate
checks, and passing the first makes the second feel unnecessary.

**The converse check.** A date bug matched 31 of 83 production rows. Rather
than stopping there, the converse was run — rows matching the precondition
(early-morning start) but *not* the symptom. Eleven existed, already stored
with the "wrong" date, because the UTC bug happened to produce exactly what
the newly-requested training-day rule would. A forward-only "shift every
early-morning session" fix would have corrupted all eleven. After N records
match a defect signature, query for records that fit the precondition but
not the prediction and explain every one before acting — that's where a
wrong model shows itself, and in a repair it's the rows a confident fix
damages.

**Verdict** — report as hypothesis unless every line is clean. "One
candidate explanation, here's what would confirm it" costs a sentence.

## The bar doesn't drop for "no" or "never mind"

**Negative conclusions gate work too.** A visibly-wrong per-game field
frame was parked with a one-line "not worth it — version bump plus
reclassify, nothing surfaces it." Lachlan said try again. A real pass — ten
estimators, a quantified downstream-impact table — produced a defensible
close *and* showed that the obvious "clamp" fix would have broken two other
games. "Stop, don't fix this" redirects effort exactly as much as "here's
the fix"; it gets the same Inputs / Can-it-separate / Evidence-held-out
treatment. A parked item justified only by "not worth it" or "looks
invisible" is a hypothesis, not a verdict.

**A blocked verification route is not a downgrade.** A change touched
backend EF queries and UI; the planned "run it and look" was closed by auth
with only a bcrypt hash stored. The risk that mattered was query
translation, not pixels — an in-memory-SQLite harness against the real
DbContext retired exactly that risk with no auth, and the UI half was
reported as build-checked only. When the planned route is blocked, name the
specific risk it would have retired, find the lowest layer that still
exercises it, and state plainly what stays unverified. Don't downgrade to
"it compiles" and don't quietly widen what "verified" covers.

**A retraction is a claim.** A safe-area layout bug was reported from a
screenshot, then withdrawn after comparing viewport meta tags — which
examined a different element than the broken one. The bug was real; the
retraction was asserted with more confidence than the original and never
verified. Withdrawing a finding needs evidence of the same standard as the
assertion, aimed at the same target, and prefers a direct measurement
(computed style / geometry) over an indirect proxy (source inspection,
config comparison). "Looks fine now" is not evidence a defect was
imaginary.

## When Lachlan asks whether a metric means something

**Read the computation, not the label.** Asked whether a "push vs pull"
stat flagged a real program problem, answering from the label would have
engaged that reading. The stat counted raw set counts over a fixed template
program — it measured the program's designed shape, not execution, and
would read identically every month forever. Same review, three more: a
"rebalance" prompt comparing non-substitutable categories, a distance total
summing GPS for shuttle sessions where GPS under-measures, a recovery line
restating a tautology. Every label was plausible; the implementation was
the tell. Separate "the metric is mis-specified" from "the underlying thing
is wrong" — only the first is visible from the code, and answering from the
label silently endorses the framing.

**Grep the anti-patterns the codebase already warns about.** A date util
opened with a comment: deliberately avoids `toISOString()`, which converts
through UTC and rolls local midnight back a day in a positive-offset zone.
Grepping `toISOString()` turned up three call sites doing exactly that, two
feeding a user-visible date — a real off-by-one for any session logged
before ~10am local. A documented "why we don't do X" is a spec for a bug
class; check every other call site honours it. Each hit is a candidate
until the offending expression is *executed* under the stated conditions
(`TZ=Australia/Melbourne node -e ...`), not reasoned about.

## Decide what counts as evidence first

At the start of an empirical task, write one short paragraph: what data
exists, what's being held out, what result would falsify the hypothesis.
Deciding this after the result is questioned is too late — by then every
case that could have been held out has been seen.

## Before delivering

Re-read this file and confirm:

1. Every claim that pulled in the heavier path (migration, db backup) was
   checked against the live model, not the plan's description of it —
   including a plan written this session.
2. Every file named as the thing to modify was opened, or its README read.
3. Sweeps passed their parameter explicitly; identical rows were treated
   as a bug first.
4. The Phase 3 block was written out, not performed mentally.
5. Anything matching Lachlan's prior names the extra check it got.
6. Anything with an unclean gate is framed as a hypothesis — and so is any
   "not worth it" call or retraction.
7. The artefact measured was confirmed to be the one that changed; every
   measurement carries its status code and an expected magnitude.
8. A metric question was answered from its implementation; a data
   transformation was verified inside the engine, not through text.

If one is unmet, fix it before delivering, and say what was missed.
