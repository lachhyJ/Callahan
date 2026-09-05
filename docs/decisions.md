# Design decisions

Callahan is a single-user training tracker I built for myself, so most of the
interesting engineering isn't in the feature list — it's in the calls I had to make
about data modelling, metric design and verification, where the obvious answer turned
out to be wrong.

This file records those calls and the reasoning behind them. Each entry is what was
decided, why, and what I'd tell a future maintainer. Entries are grouped by topic; the
date is when the decision was made, not when it was written down.

- [Stack and architecture](#stack-and-architecture)
- [Data modelling and storage](#data-modelling-and-storage)
- [Measuring a sport from GPS](#measuring-a-sport-from-gps)
- [Metric design](#metric-design)
- [Security and auth](#security-and-auth)
- [Testing and verification](#testing-and-verification)
- [Deployment and operations](#deployment-and-operations)
- [The native iOS wrap](#the-native-ios-wrap)

---

## Stack and architecture

### C#/.NET over other stacks
**2026-08-04.** ASP.NET Core Web API (.NET 10), controllers, EF Core over SQLite, React
(Vite) frontend.

Chosen for depth rather than breadth: I already had velocity in this stack, and since
the repo doubles as a portfolio piece, going deep in one stack is worth more than
sampling several.

**Tradeoff accepted:** C#'s ecosystem for unofficial Garmin Connect integration is much
thinner than Python's. I took that hit knowingly and, when Garmin work actually landed,
built it as a separate Python container rather than forcing it into the C# stack.

### Controllers over Minimal APIs
**2026-08-04.** Callahan has five-plus growing resource groups (exercises, sessions,
sets, activities, auth). Minimal APIs suit a handful of routes in one file; past that
they get manually grouped into per-resource extension methods anyway, which is
reinventing controllers without the built-in model binding and `[Authorize]` ergonomics.

### Simple custom auth, not ASP.NET Identity
**2026-08-04.** A single credential in config plus JWT issuance.

Identity exists for multi-role self-service user management — registration, password
reset, lockout policies. Callahan is one person behind a login gate. Adopting Identity
would have meant carrying a user table, a role system and a migration surface for
requirements that don't exist.

### Don't justify one project's architecture with another project's architecture
**2026-08-04.** While scaffolding, I caught myself justifying several decisions
(controllers, wholesale reuse of another project's Identity + JWT setup) as "matches my
other app" rather than evaluating them for Callahan. Both were re-decided on independent
grounds and one of them came out differently.

**How to apply:** shared authorship is not a technical argument. Checking another
project for a *convention* is fine; using its stack as unexamined justification is not.

### The free-form workout flow is the template flow in a different mode
**2026-09-01.** `/workout/custom` renders `ActiveWorkoutPage` with no `:templateId` route
param rather than a dedicated component. A derived `sessionKey` (`'custom'` or the
numeric id) keys the active-workout and rest-timer stores. The old `LogWorkoutPage` was
deleted, and no shared components were extracted.

The requirement was that the two flows be the *same experience*, not merely visually
consistent — a separately-styled second page always drifts again. Extracting the
exercise-card and set-table into shared components is the tidier-sounding option, but it
means refactoring the ~1000-line page that already ships the core loop for no
user-visible gain. Reusing the page whole means manual sessions inherit set types, the
rest timer, the plate calculator, the keyboard toolbar, localStorage resume and the
finish summary for free, and there's exactly one workout-logging code path to maintain.

**How to apply:** gate template-only behaviour on `isCustom`. If a third entry mode ever
appears, *that's* the point to reconsider extraction — two modes in one page is still
comfortably readable.

---

## Data modelling and storage

### One tournament is one row, and the taper is a field on it
**2026-09-04.** A tournament weekend used to be two unrelated rows: a `Tournament`,
which had a date range and grouped the Ultimate activities played at it, and a
`TaperEvent`, which had a single date and a taper length and drove the countdown on the
taper page. Nothing linked them. Every tournament was entered twice and the two names
were free to drift apart. They are now one entity, with `TaperDays` as a nullable field
on it.

The merge started as a much smaller task — tournaments had no edit or delete UI, so a
typo needed a database edit. Looking before building turned up why that gap felt odd in
isolation: full create-and-delete already existed, just on the other entity.

`TaperDays` is nullable rather than defaulted, and that's the part worth stating. The
two directions aren't symmetric. Every taper points at a tournament I'll play in, so the
duplication was pure overhead — but the reverse doesn't hold, because most tournaments
in the database are backfilled past ones I never tapered for. A sensible-looking default
of 10 days would have made every one of those appear on the taper page as a taper that
happened. Null means "not a taper target", and adding a tournament to record its games
can't silently invent a training plan.

Both pages kept their own form rather than consolidating onto one screen. They're
different moments — adding an upcoming tournament while planning a taper, versus
recording a past one while grouping its games — and they now write the same row.

Merging also exposed three bugs that had been invisible while the entities were
separate: editing a tournament's dates never re-ran the sweep that attaches games to it,
the games list was built from the games themselves so a tournament with no games yet was
unreachable in the UI, and the season link wasn't exposed by the API so it couldn't
survive an edit.

**How to apply:** when two records keep being created in pairs, ask whether they can ever
legitimately diverge; if they can't, the duplication is the bug. Then check the
asymmetry before merging — which side implies the other, and which doesn't — because
that's what decides whether the merged field is nullable or defaulted. Check the child
tables before writing the migration: this one renamed a foreign key in place, which is
only correct because the taper tables were verified empty first.

### A session belongs to a training day, not to a UTC calendar day
**2026-09-02.** `trainingDayIso(startedAt)` formats the **local** date, then shifts back
one day if the session started before 3am. It's the only way a session date is stamped,
and it always reads the session's *start* time, never the clock at save time.

Two problems collapsed into one rule. The bug: dates were stamped with
`new Date().toISOString().slice(0, 10)`, which converts through UTC, so in AEST/AEDT any
session finished before 10/11am local was filed under the previous day — 31 of 83
production sessions. The feature: I lift at midnight, and a session started at 00:30
should count for the previous day's training. Those turn out to be the same rule, and
the bug had been accidentally satisfying the feature.

3am rather than the 1am first proposed: at a 1am cutoff, sessions starting at 00:55 and
01:05 land on different days, so a ten-minute swing decides a date.

**How to apply:** separate the *timestamp* (when it happened — `StartedAt`/`FinishedAt`,
never rewritten) from the *attribution* (which day it counts for — `Date`). Conflating
them is what made a routine repair sound alarming when described as "changing the date".
When a bug and a feature request describe the same boundary, make the rule explicit
rather than patching the bug — the rule is testable.

### The container carries the timezone; the code does not
**2026-09-02.** `TZ=Australia/Melbourne` is set on the backend container in both compose
files, and `Program.cs` logs the effective zone at startup. The ~15 `DateTime.Now` call
sites are left alone.

Those sites all derive "today" for a read-side comparison and none writes a local time
into a UTC column, so the environment is the correct lever and one line fixes all of
them. It's also what the code already assumed — a comment read
`ReminderHour = 20; // 20:00 local - see docker-compose TZ note`, and that note had never
existed, so reminders intended for 8pm fired at 6am.

**How to apply:** when behaviour depends on an ambient environment setting, make the
effective value observable at startup. Configuration that is silently absent produces
wrongness with no signal, which is strictly worse than a crash. And check the image
actually ships `tzdata` — without it `TZ` is accepted and ignored.

### Decimals are stored as REAL across the whole model
**2026-09-02.** `OnModelCreating` sets `SetProviderClrType(typeof(double))` for every
decimal property in the model, rather than configuring columns one at a time.

EF's SQLite provider maps `decimal` to TEXT by default, giving the column TEXT affinity,
so server-side comparisons are lexicographic: `WeightKg > 9` matched 21 rows where the
numeric comparison matched 1211, because `'10'` sorts before `'9'`. Nothing was visibly
broken, because every comparison in the app materialises the rows first — but that's a
rule the next query breaks silently and invisibly.

Model-wide rather than per-property so a decimal column added later inherits the fix.
Per-property would leave the next one trapped.

**How to apply:** where a latent trap is guarded only by a convention every future author
must remember, remove the trap rather than documenting it.

### Assistance is negative weight on one exercise, not a separate "(Assisted)" exercise
**2026-09-01.** A pull-up is one exercise whose load runs continuously from assisted
(negative) through bodyweight (zero) to weighted (positive). The separate
`Pull Up (Assisted)` rows were merged in and their assistance normalised to negative.

They're the same lift at different stages, so splitting them severed the progression
exactly where it's most interesting — and the split had produced contradictory data, with
the same 14 kg of assistance stored as `+14` on one row and `−14` on the other.

Storing *absolute* load (bodyweight + weight) would let e1RM work across the whole range,
but it needs per-session historical bodyweight to avoid rewriting the past wrongly, and
daily 1–2 kg fluctuation would move e1RM by 1–2% against a 3% stall threshold — eating a
third of the band that decides whether a lift has stalled.

**How to apply:** prefer one entity with a signed, continuous attribute over separate
entities per stage, when the stages are points on one progression.

### `ActivityTrack`: a separate entity holding raw lat/lon, not projected coordinates
**2026-08-27.** The per-second GPS stream for a game lives in its own table (1:1 with
`Activity`, cascade delete), storing a compact columnar JSON blob
`{"t":[…],"lat":[…],"lon":[…],"spd":[…]}` plus sample count and median spacing as real
columns.

Three sub-decisions:

- **Own entity, not a column on `Activity`.** The blob is 65–100 KB. The reclassify job
  materialises every game activity in one query, and a nav property loaded there would be
  N × 100 KB. As a separate entity with no lazy-loading proxies it's inert unless
  explicitly `.Include`d, and the list endpoints use `.Select` projection so they never
  touch it.
- **Raw lat/lon, not projected field-frame coordinates.** The field frame (rotation,
  centre line, half-dimensions) is fitted *per game* from that game's own fast samples.
  Storing projected coordinates would freeze the fit and make reclassification — whose
  entire point is retuning with zero Garmin traffic — unable to re-derive it.
- **Not compressed in the database.** ~100 KB × ~60 activities/year is ~6 MB. SQLite
  doesn't care, and gzip+base64 would kill `sqlite3` inspectability. The git test
  fixtures *are* gzipped, which is a different call — they're never read by a human.

**How to apply:** a malformed, empty or partial blob must degrade to a "no track" result,
never a 500.

### Lap data as a child table, JSON blobs only for unsettled schemas
**2026-08-25.** Laps live in their own `ActivityLap` table, not as a JSON column on
`Activity`, even though `MonthlyReport.ReportJson` and `DailyWellness.RawJson` both use
the blob approach.

The primary use of lap data is aggregation (`SUM(DistanceM) WHERE IntensityType =
'ACTIVE'`), laps are uniform tabular rows, and volume is small. That matches the existing
parent-child shape, not the "hedge for an unmodelled field" reasoning that justifies JSON
elsewhere.

**How to apply:** JSON-blob storage in this codebase is specifically for data that is
read whole, rarely queried, and whose schema is still settling. Tabular, aggregated or
filtered data gets a real child table. Don't reach for JSON just because a payload looks
nested at first read.

### Discovery dump before schema, for any undocumented upstream
**2026-08-25.** Before writing the `DailyWellness` migration I added a `--dump-wellness`
mode to the sync script and ran it against real data for two dates. The schema — typed
nullable columns per metric, plus a `RawJson` hedge — was written from that output, not
from the client library's docs, which don't document response field names at all.

Garmin's wellness field names are undocumented and watch-model dependent (training
readiness only exists on newer models), so guessing risked a migration full of columns
that are always null, or a mapping that silently reads the wrong key.

It paid off twice more in the same session. The dump showed `get_stats` already returns
resting heart rate and body battery, so the sync dropped from six Garmin requests per
date to four. And a planned relative-speed heuristic for classifying interval laps was
dropped entirely once the dump showed Garmin already labels every lap
`WARMUP`/`ACTIVE`/`RECOVERY`/`REST`/`COOLDOWN` — the exact classification the heuristic
was trying to reconstruct, cross-checked against Garmin's own pre-aggregated figure and
matching to the metre.

**How to apply:** dump real responses before designing against an undocumented API, and
don't skip the step just because the plan already has a design in hand. Pin the client
library exactly (`==`, not `>=`) once you depend on undocumented field names — a silent
minor bump on a future image build renames a field with no error, just a quietly-null
column.

### A cached snapshot goes stale when its *inputs* change, not only when its shape does
**2026-09-02.** Repairing the 31 mis-attributed session dates was paired with a report
schema-version bump, even though the report DTO was unchanged. The bump's comment records
that the underlying data moved, not the DTO.

Locked months are snapshotted as JSON and served from cache while the stored version is
current. Correcting those dates without bumping would have left nine months serving
reports computed from the old, wrong attribution — invisibly, because nothing about the
report's *shape* was wrong. The version field's original purpose was shape changes; its
actual purpose is "this snapshot no longer reflects reality", which a data repair
triggers just as surely.

**How to apply:** any backfill or repair of source rows must ask what has already been
computed *from* those rows and cached.

### Deploy the fix before repairing the data it produced
**2026-09-02.** Where a code fix and a data repair address the same defect, the code
ships first. If the repair must lead, the window is stated explicitly and the
verification re-run after the deploy lands.

The 31-row repair was applied while production still ran the buggy code, because the fix
sat on an unmerged branch and merging is what deploys. For the length of that window any
newly logged morning session would have been re-corrupted, and the "0 remaining
mis-attributed" check — run before the deploy — was measuring a state the system was
actively leaving. Nothing was lost, but the ordering was discovered rather than chosen.

**How to apply:** treat a repair and its fix as one change with an ordering constraint,
not two tasks. Merging to `main` *is* the deploy here, so "the fix is committed" is not
"the fix is live".

---

## Measuring a sport from GPS

Callahan ingests per-second GPS from Ultimate Frisbee games and tries to answer "how much
did I actually play". This section is the longest because almost every intuitive approach
was wrong, and the failures were more informative than the successes.

### On/off-field comes from position geometry, not speed
**2026-08-27.** Each moment of a game is labelled on-field or off-field from *where* the
player is on the pitch — rolling lateral spread and distance from a fitted centre line —
not from how fast they're moving. An earlier speed-threshold classifier (adaptive 2-means
over lap average speed) was built, shipped to a branch, and thrown out.

Real ultimate is mostly standing *even while on the field*: stoppages, disc check-ins,
setting up the stack. Across six real games, median in-game speed is 0.5–1.0 m/s and only
16–26% of time is above 2 m/s. On-field laps average 1.22–1.44 m/s, below the speed
classifier's 1.5 m/s "this is play" floor, so it returned "no separation" on every single
game. Speed magnitude cannot separate the classes and no retune fixes that.

Position can. Off a point, the player walks up and down one sideline — pinned laterally,
~20 m off centre, 5–10 m of lateral range. On a point they use the full width — ~6–10 m
off centre, 30–47 m of range.

**How to apply:** the Python reference implementation is
`scripts/ultimate-stream-explore/segment.py`; `backend/Services/FieldGeometry.cs` is a
line-for-line port that matches exactly, not merely within a band. `explore.py` in the
same directory is the *failed* speed approach, kept deliberately because it passes its own
synthetic self-test — which is exactly how a wrong feature hides.

### "On-field time" measures occupancy, not play — and that gap was the real complaint
**2026-08-27.** The shipped on-field percentages (67–89% across 17 games) felt far too
high against an expected ~50/50. Two mechanical explanations were tested and rejected
first. The actual finding: 69% of all "on-field" time is below 1.5 m/s and only 11% is
above 3 m/s, accruing at ~3.0 minutes per point when a live point runs 1–2 minutes. The
classifier was faithfully reporting where the athlete was *standing* — waiting on the
line, stall counts, disc check-ins — and the label promised something else.

The strongest evidence the geometry itself was sound came from an independent direction:
I recalled, unprompted, playing considerably more in the February/March games than in
April because the squad was thinner. The classifier reproduces exactly that ordering
without any knowledge of roster size. An over-counting classifier would not reproduce a
squad-size effect it cannot see.

Three candidate "live play" bases were then measured against six fixtures before one was
chosen:

| Basis | Result | Verdict |
|---|---|---|
| points × nominal duration | swings 22–46% of game time with the point count | adds nothing over the point count already shown, on a guessed 90s point |
| **in-point on-field time** | **~50% of on-field time, ~1.5 min/point** | **chosen** — matches expectation, grounded in geometry already computed |
| speed-gated on-field time | 10–19%, halves/doubles across a 1.5–2.5 m/s threshold | measures *running*, not playing; reintroduces the rejected speed threshold |

**How to apply:** a metric-definition mismatch cannot be fixed by retuning constants, and
trying risks corrupting a number that is currently correct for what it measures. Reach
for the definition before the knobs.

### When a narrower metric replaces a flawed one, remove the old one
**2026-08-28.** The occupancy percentage was initially kept visible alongside the new
live-play figure, on the theory that it was still correct-if-secondary. Once the two sat
side by side as stat cards, it was clear the occupancy figure just read as an inflated
version of the same thing. It was removed from every headline surface.

**How to apply:** a demoted-but-visible bad number still competes for attention and still
misleads. Plan to remove, not to demote. The underlying stored column is unchanged — it
still feeds the live-play computation and the timeline.

### The point counter systematically deleted short points
**2026-08-27.** Point counts were under-reported by 4–13%, concentrated in short points.
To count an endzone dwell as a point, the detector required 54 of the following 90 seconds
to be on-field. That filter exists for a good reason — it removes dwells where the player
stood on the line for instructions and returned to the sideline — but it structurally
discards the opposite case: a short defensive point where the other team scores quickly
and the player subs off. The following 90 seconds are then mostly off-field, and a genuine
point is dropped. Instrumenting one disputed 27-minute window found 13 dwell candidates
and 1 acceptance, including an 86-second dwell rejected at 58% against a 60% threshold.

The fix was a two-constant relaxation. The part worth preserving is the validation
discipline: the relaxation was tuned on six fixture games, then scored against eleven
games that had never been used — where it moved 191→199 points and 9/11→11/11 games into
the expected band — *before* any constant was committed.

**How to apply:** `scripts/ultimate-stream-explore/holdout_check.py` exists for exactly
this and runs against a read-only copy. Keep the held-out discipline for any future
retune. Note that a retune needs *both* version constants bumped — the reclassify endpoint
gates only on the stored classifier version, so bumping the geometry version alone will
not trigger a non-forced reclassify.

### The timeline recomputes on every read — no persisted segment table
**2026-08-27.** The game-detail timeline re-runs the geometry analysis over the stored
raw track on every request and returns the segments directly. Nothing is written.

Its stated purpose is to be a spot-check tool — an easy way to see whether something looks
off in the analysis — which only works if it always reflects the classifier's *current*
tuning, not whatever was in effect when a row was last written. A persisted table would
need its own invalidation on every retune, on top of the version-bump-and-reclassify dance
the aggregates already need, and could silently drift out of sync with the numbers shown
directly above it on the same page. Recompute is one pass over ~2000 samples per page
view, well within what a single-user page load absorbs.

**How to apply:** if recompute cost ever becomes real, a segment table slots in as a
*cache* in front of the same call — not as a replacement for it. Don't persist without
also solving the staleness problem this decision exists to avoid.

### A default Garmin lap is not a sub log
**2026-08-27.** Per-lap boundaries are only used when an activity has at least four laps;
below that the classifier falls back to aggregating straight from the geometry segments.

Garmin returns a single lap spanning the whole session when the watch was never
lap-pressed. That single lap has an on-field fraction of ~0.5 over the whole game, so it
was classified as one ambiguous lap and on-field seconds collapsed to zero. This hit all
17 backfilled games. A threshold of 2 wasn't enough — two of those games had stray Garmin
auto-laps and still produced garbage.

**How to apply:** the lap-boundary path remains **unexercised on real data** — no game has
ever been lap-pressed. Its behaviour is covered only by tests synthesised from real
segments. The first real lap-pressed game is the moment to check it and likely retune the
thresholds.

---

## Metric design

### Match a metric's aggregation window to its dynamics
**2026-09-01.** Training readiness was removed from the monthly report entirely. It keeps
its daily surfaces untouched.

Readiness is an acute, strongly mean-reverting daily score whose expected value largely
tracks yesterday's load. Averaging it over a month mostly averages out the thing that
makes it useful. Worse, the derived line — "readiness averaged N points below your
3-month baseline across your two highest-volume weeks" — is definitionally what *should*
happen after hard weeks. A tautology dressed as an insight. What belongs in a monthly
retrospective is the slow-moving stuff: resting HR and HRV weekly drift are real
accumulated-fatigue signals.

**How to apply:** before adding a metric to a summary, ask what a month's average of it
could tell you that its daily value doesn't.

### A metric that fires every period regardless of behaviour is measuring a constant
**2026-09-01.** Push/pull balance compares each side's *completion rate* — sets logged
against what that month's own sessions prescribed — and flags only a divergence of ≥15
percentage points. The old raw push:pull set-count ratio is gone.

On a fixed template program, a raw ratio measures the program's *designed shape*, not the
training: a bench-heavy template flags every month forever with nothing to act on. The
question that prompted this was "is this telling me I'm doing my exercises wrong, or that
my program is wrong?" — neither; the metric was mis-specified. Comparing rates rather than
counts also means a light month can't false-positive, since training less drags both sides
down together.

**How to apply:** reframe such a metric against intent (plan, target, prescription) or
delete it. And note the general form: a question about whether a stat is *valuable* is a
question about how it's *computed*, so read the implementation before answering from the
label.

### Each lift is measured on a basis its own history supports
**2026-09-01.** Per exercise, one of three bases is picked from its **full** history,
never from the display window: estimated 1RM for normal working sets; set volume when
median reps exceed 12; a load-then-reps rank whenever any non-positive load appears.
Display always shows the set that actually happened.

Three distinct failures under one blanket e1RM:

1. **Assisted work was measured backwards.** With a negative load, Epley's
   `w × (1 + reps/30)` makes more reps *more negative* — improving reads as declining — and
   at bodyweight it's identically zero forever.
2. **High-rep work is over-extrapolated.** A 20-rep set is multiplied by 1.67, and a
   prescribed range as wide as 15–20 moves the estimate ~17% on formula alone, swamping
   the signal.
3. **A bare e1RM is a number never lifted** — "new best: Leg Press 301.0 kg" when the
   actual set was 215 × 12.

12 reps deliberately stays on e1RM: those slots are pinned at 12 rather than spanning a
range, so the estimate's bias is *constant* month to month and cancels out of any
comparison. That's also the sharper reason the 15–20 slots can't do the same.

**How to apply:** e1RM earns its place because it tracks **double progression** — reps
climbing inside a fixed range before the weight moves, which top weight is blind to
(240×10 → 240×12 is progress and +0 kg). That's the test for whether a normalisation is
worth its inaccuracy. And the basis must be a property of the entity, not of the window
being viewed: deriving it per-query let the same lift read as set volume on one page and
e1RM on another.

### Prefer a user-maintained structure over an algorithmic ranking for default subsets
**2026-09-01.** The season-strength chart shows only exercises that appear in a workout
template, ordered by their position in the program. The first cut ranked all moved
exercises by magnitude of change and showed the top five.

That looked arbitrary — indistinguishable from a random selection — and surfaced
incidental accessory lifts instead of the movements the program is built around. Anchoring
to the program, a structure already maintained by hand, makes the default set legible and
trustworthy. Slot depth doubles as a free isolation filter: a 15-rep face-pull's e1RM is a
weak signal and lands hidden behind the legend's "+N more".

**How to apply:** reserve algorithmic rankings for secondary sort.

### Deterministic reports, with the LLM strictly alongside
**2026-08-30.** The monthly report — including its "Strong / Steady / Down month" headline
verdict and its recovery section — is 100% deterministic. The classifier is a pure rules
function; the wellness summariser is pure and unit-tested, reusing the existing insight
bands rather than re-deriving thresholds.

The report's job is to make a call and close a period. A rules function does that; an LLM
writing it adds a hallucination surface to a fixed-format artifact where a wrong number is
worse than no report.

The same line is drawn for the AI taper consult, which reads free-text check-in notes
alongside the numbers: it is strictly additive and explanatory, on a fully separate code
path. It never edits or gates the deterministic step-taper percentages, which are computed
and shown identically whether or not the consult is configured or working. A consult
failure returns 503 for that one feature and never touches the data already on screen.

**How to apply:** the classifier thresholds are tunable starting values, not researched
constants — the first cut wrongly forced "Down" on three stalls and mislabelled a real
3.8-sessions/week month. Adjust from real output and keep the function pure so the unit
tests stay the guard. Any future DTO field added to a snapshotted record must be nullable
and positional-last, so pre-feature snapshots still deserialize.

### Measure the behaviour before making the change that assumes it
**2026-09-02.** Before changing navigation, the app records real usage: normalised route,
the route navigated *from*, and foreground dwell. No read endpoint and no in-app UI until
there's a meaningful sample.

An audit identified a structural oddity — 23 routes behind two bottom tabs, one screen
reachable only via a calendar gutter — but whether that structure actually bites is a
question about behaviour, which neither recollection nor inspection answers. The from-path
is the load-bearing field: a visit count says a screen is used, only the from-path says
how it's *reached*, which is exactly what a nav change alters.

The readout is withheld on purpose. A screen reporting "you never open /history" changes
how /history gets opened, and the sample is meant to be unbiased.

Each row also records how long the backend process had been up when the event landed, so
post-deploy verification traffic can be excluded at read time. Tagging rather than
dropping matters because the exclusion window is a guess: a tagged dataset can be
re-filtered, a pruned one cannot.

**How to apply:** when excluding a category of data, record the discriminator and filter
at read time. A threshold applied at write time is baked permanently into the dataset by
whoever happened to be guessing that day.

---

## Security and auth

### Authorization is deny-by-default, and unmatched API routes are explicitly 404
**2026-09-02.** A `FallbackPolicy` requires an authenticated user; the auth, health and
dev-login routes carry explicit `[AllowAnonymous]`. A
`MapFallback("/api/{**rest}", () => Results.NotFound()).AllowAnonymous()` is registered
alongside it.

All 18 data controllers already had `[Authorize]`, so protection was real but
*conventional* — the 19th controller added without the attribute would have been silently
public, with nothing to catch it. The fallback inverts the failure mode.

The `MapFallback` is not decoration. ASP.NET Core applies the fallback policy to requests
matching **no endpoint at all**, so without it every unknown `/api/*` path returns 401
instead of 404 — and the shared fetch wrapper treats any 401 as an expired session,
clearing the token and logging the user out. A stale bundle calling a removed route would
have logged the user out silently. That regression shipped and was caught by a post-deploy
probe.

**How to apply:** a default-deny control is defined by what it does when *nothing matched*
— the case least likely to be tested. When changing which status code a system emits, grep
the clients for how they branch on it: a fetch wrapper that maps one status onto a
destructive action turns a status-code change into a user-visible bug in a different
codebase.

### Session token stays in localStorage, deliberately
**2026-08-26.** After a security review, the JWT stays in `localStorage` rather than
migrating to an httpOnly cookie, and no 2FA was added.

localStorage is genuinely readable by any injected script, which is a real risk in
general. But a full sweep of the frontend found zero XSS injection points — no
`dangerouslySetInnerHTML`, no `innerHTML`, React's JSX escaping relied on throughout — and
this is a single-user app with one credential hash in config and no other users' data at
stake. A cookie migration brings CSRF and SameSite handling with it, which is real cost
against a risk with no live vector.

**How to apply:** revisit if either premise changes — a genuine injection point appears (a
dependency doing raw HTML rendering, a rich-text feature) or the app stops being
single-user. Until then, the CSP and the login rate limiter are the actual
defence-in-depth here, not the storage choice.

### The dev-login bypass is gated on two independent conditions
**2026-08-16.** `POST /api/auth/dev-login` issues a real JWT with no password check. The
route is only **registered** — in `Program.cs`, not merely guarded inside a controller
action — when the environment is Development **and** an explicit `Auth:AllowDevLogin` flag
is set. The local-dev compose file sets both; neither key exists in the production env
file.

The first version gated on the environment check alone. That's one misconfigured
environment variable away from a real unauthenticated login bypass in production — a
landmine sitting in the codebase indefinitely for a one-off convenience. Fixed two ways:
registration moved into a conditional `MapPost`, so in production the route doesn't exist
in the routing table at all rather than existing and saying no; and a second independent
opt-in was added, so a single stray environment variable is no longer sufficient.

The purpose is to let automated UI verification run against an authenticated app without
anyone handing over a real password.

### nginx does not inherit `add_header` into a location that sets its own
**2026-08-26.** The four security headers are declared in one file and pulled into every
`location` block by `include`, rather than set once at server level.

A first attempt put them once at server level and they silently vanished on every path
that set its own `Cache-Control` — index, assets, service worker, manifest. Confirmed by
curling each path directly, not by reading the config and assuming.

Relatedly, the CSP's `connect-src` is a build-time substitution defaulting to `'self'`,
overridden only by the local-dev compose file to permit the dev API origin. An audit
flagged the localhost allowance as dev leftover to delete; it isn't — the local docker
stack genuinely needs it. Both things are true at once, which makes it a build-time
variable rather than a line to remove.

**How to apply:** verify with `curl -I` against each real path after any nginx change,
since the failure mode is silent — a 200 with the header simply missing. And before
deleting config that looks like a dev leftover, find which build consumes it: "only dev
needs this" is an argument for scoping it to dev, not for removing it.

### Verify framework defaults rather than assuming them
**2026-08-26.** The login rate limiter sets `RejectionStatusCode = 429` explicitly. ASP.NET
Core's built-in default is `503 Service Unavailable`, not 429 as assumed going in — caught
by actually curling the endpoint in a loop past the limit.

A second instance the same day: Docker Compose interpolates `$word` inside `env_file`
values, so a bcrypt hash containing raw `$` is silently corrupted before it reaches the
container, and an unset reference resolves to an empty string. Production had the correct
doubled-`$` escaping; local dev never had, which meant real password login had been
silently broken locally the entire time. Verified by reading the resolved variable back
out of the running container, not by reading the file.

**How to apply:** for anything whose failure mode is silent, verify against the running
system rather than the source. `docker exec <container> printenv <VAR>` beats reading the
env file.

---

## Testing and verification

### A new test suite is mutation-checked before it's trusted
**2026-09-02.** The 63 new frontend tests were validated by introducing six deliberate
breakages one at a time and confirming the suite caught each.

63 tests passing on the first run, against code written in the same session, is equally
consistent with 63 assertions that assert nothing. Five of six mutations were caught. The
survivor — deleting an epsilon from a greedy plate-calculation loop — turned out not to be
a testing gap: a sweep of every 0.01 step from 0 to 300 in both units found no input where
it changes the result, because every plate size is exactly representable in binary and the
remainder is re-rounded each pass. Writing a test for it would have manufactured false
coverage.

**How to apply:** watch a suite fail for the right reason before believing it. A surviving
mutant is a question, not a verdict — it says the tests and the code disagree about what
matters, and the code is as likely to be the party in the wrong. Unreachable branches want
documentation or deletion, not tests.

### A sweep only proves what it could reach
**2026-09-05.** Auditing the app for touch targets under 44px, I claimed three times that
every button cleared it. The sweep enumerated routes, which silently excluded every screen
needing a particular state to render — including the active workout screen, where the set
checkbox is pressed once per set and measured 32x32.

The fixes then broke the measurement. Enlarging a bordered control without changing how it
looks means a transparent overlay that deliberately leaves the element's own box alone, so
reading that box reports the old size forever. The check moved to what a thumb actually
hits: probe outward with `elementFromPoint` until the point stops resolving to the control.

**How to apply:** an audit's blind spots are set by how it enumerates, so state the
enumeration before trusting the result. And when a fix is defined by *not* changing a
measurement, that measurement has stopped being the check.

### A behaviour-preserving refactor is proven by diffing the output
**2026-09-02.** Extracting shared chart rendering was validated by rendering all seven
components to static markup with fixture data before and after the change, and diffing.
Byte-identical was the acceptance bar.

These are pure prop-driven components, so this costs one throwaway script and replaces "it
looks right" with proof. It also caught the risk that made the refactor non-trivial in the
first place — differences in tick rounding between callers — before any of it reached the
app. `react-dom/server` needs no jsdom, so the same technique works from a plain node test
runner.

**How to apply:** where a refactor is supposed to be behaviour-preserving and the unit is
pure, snapshot the real output either side. Reserve visual checking for confirming
integration afterwards, not for detecting the regression.

### Read the repeated values before extracting on the strength of repeated names
**2026-09-02.** An audit flagged that eight chart components each redefine `WIDTH`,
`HEIGHT` and padding constants, and implied shared constants were the fix. The values are
all different — height spans 40 to 168, left padding 20 to 34 — because each chart holds
different content. They share a shape, not a size, and unifying them would have broken
every chart. The genuine duplication was one level down, in the four lines of SVG and the
tick loops each chart had copied.

The extracted tick builder takes a `decimals` argument because the rounding differed
*meaningfully* between callers: one chart wants whole percentages, most want 1dp, and the
volume chart needs 2dp or a degenerate 1 kg month renders a 0.25 step as 0.3 / 0.8.

**How to apply:** this was the third time in one session that a shared *name* hid different
meanings. A duplication finding is a hypothesis about meaning, and it has to be checked
against the code before it becomes a refactor.

### Frontend tests cover pure logic only, with the timezone pinned
**2026-09-02.** vitest in the `node` environment, no jsdom and no component tests. The
timezone is set in the npm script, not in the vite config.

The lines worth covering are plate maths, date handling, formatting and session-type
inference — all pure functions. The real bugs were in date arithmetic, config and status
codes, not in rendering, so jsdom and testing-library would be cost without matching
benefit. TZ goes in the script because Node resolves the zone once per process and a
config-file assignment can land too late; under UTC every date assertion here passes
trivially and proves nothing, which is worse than having no test.

**How to apply:** when a test's whole purpose is timezone behaviour, the timezone is part
of the fixture — pin it where it's guaranteed to take effect and say why in the same place.
More generally, prefer covering the layer where the defects actually occurred over the
layer that's conventional to cover.

### A quality gate belongs on the path that introduces risk, not the one that removes it
**2026-09-02.** The deploy workflow runs backend and frontend tests before the SSH steps,
guarded so that only a `push` event runs them. A manual `workflow_dispatch` run skips them
entirely.

Until this, the workflow ran no tests at all, so 122 backend tests gated nothing — a red
suite deployed like a green one. But gating *every* path would break the rollback:
`workflow_dispatch` exists for the case where `main` is broken, and requiring `main`'s
tests to pass before deploying a known-good older commit would block the fix at exactly
the moment it's needed. The dispatch path also deploys a specific old commit, which tests
run against the checked-out `main` would not be testing.

**How to apply:** whenever adding a gate to a pipeline that also carries an emergency path,
check whether the gate would block the emergency.

---

## Deployment and operations

### Deploy over a private mesh network, with the key restricted to one command
**2026-08-15.** The GitHub Actions deploy job joins the server's Tailscale network rather
than exposing SSH through the existing Cloudflare tunnel — the goal being no new
always-on public listener. The deploy key itself is restricted in `authorized_keys` with a
forced `command=`, so it can only ever run the deploy wrapper script. Even leaked, it
can't get a general shell.

### Short-lived credentials over long-lived ones, wherever the platform offers it
**2026-08-16.** Deploy auth moved from a single long-lived reusable Tailscale auth key to
an OAuth client that mints a fresh short-lived key per run.

The reusable key silently expired and broke every deploy, with no alert until the GitHub
failure emails were noticed. A single long-lived credential with no built-in renewal is a
recurring failure mode, not a one-off. The GitHub Action's own deprecation warning already
pointed at OAuth clients as the replacement.

Verified end-to-end by dispatching the workflow with a deliberately invalid commit SHA —
it fails cleanly at the `git reset --hard` before any rebuild, while still exercising the
real network-join and SSH path.

### On-demand sync runs as a sidecar, not via a Docker socket or a reimplementation
**2026-08-29.** The "sync now" button reaches a small always-on sidecar container built
from the existing Python sync image with its entrypoint overridden to a stdlib-only HTTP
server. No host port; the backend reaches it over the compose network and proxies to it.
Overlap is prevented by a lock in the sidecar returning 409, not a client-side cooldown.
The whole thing is feature-flagged on a base URL — blank means the endpoint fails cleanly
and the button shows a clear message, so it could ship dark.

Four options were weighed: give the backend container the Docker socket (broad daemon
privilege for one button); have the backend SSH to the host (host credentials inside the
API container); reimplement ~780 lines of Python sync in C# (duplicates working, tested
code plus its auth handling); or the sidecar. Only the sidecar keeps the Python sync as
the single source of truth, adds no privilege and no new dependency, and leaves the
nightly cron path completely untouched.

The cooldown originally imagined turned out to be unnecessary: the lock already makes a
second press a no-op 409, and every sync write is idempotent, so a double-press — or an
overlap with the nightly run — is harmless. A 6-second client-side disable was added
anyway, purely to stop a rage-tap. UX nicety, not a correctness guard.

### Don't trust an audit tool's suggested version blindly
**2026-08-04.** `npm audit`'s suggested fix for a routing dependency was a downgrade that
walked into a much worse set of 14 advisories — XSS, open redirect, an RCE — several
applicable to plain client-side routing. Staying on latest left exactly one flagged issue,
and that one is specific to a rendering mode this app doesn't use.

**How to apply:** read what's actually in the suggested target version before applying the
fix.

---

## The native iOS wrap

The same React build ships as a native iOS app. It exists for exactly one class of thing
the web platform cannot do on iOS — and that constraint drove every decision here.

### Capacitor over React Native, Swift or MAUI
Capacitor wraps the *existing* Vite build: one React codebase, two delivery targets. React
Native would have meant rewriting the frontend to reach roughly where Capacitor gets
additively. A Swift app means maintaining two clients. MAUI's only argument was C#
symmetry with the backend, against a React codebase that already exists.

**How to apply:** native capability gets added as a shell around the web app, not as a
second implementation of it. If something can't be done that way, that's the trigger to
reconsider — not a reason to fork the UI.

### The webview loads the live site; it does not bundle the build
The config points at the production URL instead of shipping `dist/` inside the app. A push
to `main` therefore updates the web app *and* the native app together, and Xcode is only
needed when native code changes. The cost is no offline support — which this app never
had, since every screen reads from the API.

### The rest beep is why the wrap exists
No web audio session on iOS can be both audible through the hardware silent switch and
polite to the user's music. The only session that ignores the switch fully interrupts
other audio with no way to resume it; the sessions that mix or duck are themselves silenced
by the switch. A page also cannot *emit* a system play command, so it can't ask the
previous app to resume. And a backgrounded PWA can't play a timed sound at all — JS is
suspended, so the countdown never fires.

Several dead ends were established empirically and are recorded so they aren't retried:
the Web Audio API is muted by the silent switch regardless of any "promote the session
with a silent media element" trick; iOS `<audio>` won't load a `blob:` URL and won't decode
a raw-PCM WAV even via a data URI, so generating audio at runtime is a dead end — it needs
a real AAC or MP3 asset.

Natively, `AVAudioSession` combines `.playback` with `.mixWithOthers` and `.duckOthers`,
which is the combination the web has no equivalent of. Backgrounded audio needs more than a
session, since a suspended app cannot *start* a sound: the audio background mode plus an
`AVAudioPlayer` armed with `play(atTime:)` keeps the app alive on the audio hardware clock.
Confirmed on device with music playing and the phone locked.

**How to apply:** don't reach for timers — a suspended process's timers never fire; the
armed player is what holds the app up. Ducking is switched on ~0.35s before the beep and
dropped when the player finishes, because `.duckOthers` on an active session ducks from
the moment of activation, which held music down for the entire rest period. Session
activity and ducking are separate concerns.

### A one-shot timer armed on the audio clock still needs a wall-clock check
The rest beep is armed with `AVAudioPlayer.play(atTime:)` against `deviceCurrentTime` — the
only clock that keeps running while the app is suspended, so it has to be the one doing the
actual timing. But its zero point moves whenever the audio hardware idles, and stopping the
outgoing player on a re-arm can idle it between reading the clock and arming the next beep.
On a real workout that showed up as the beep landing a few seconds early, once by eight
seconds, and once firing twice.

The fix doesn't try to keep the audio clock from drifting — it treats drift as something
that will happen and catches the symptom instead. `schedule()` became idempotent, so
re-arming for an end time it's already armed for is a no-op. And the finish callback checks
the wall clock before accepting a firing as real: one landing more than a second early gets
discarded and re-armed for whatever time is actually left, rather than being trusted at face
value.

**How to apply:** a clock chosen because it survives suspension isn't automatically a clock
you can trust the output of — its own baseline can move for reasons unrelated to the
timer's logic. Build the fix around the symptom (a firing that arrives too soon) rather than
a specific theory of why the clock disagreed with reality, so it still holds if the actual
mechanism turns out to be something else. And keep whatever arms a one-shot native call
listening only to the state that call actually depends on — an unrelated re-render is enough
to trigger a bad re-arm if the arming code is reachable from it, which is exactly how this
one started happening on every keystroke in an unrelated field.

### Rest alerts are local notifications, not the server push
The push had to be scheduled server-side, handed to APNs and delivered over the network —
that was the few seconds of lateness, plus a connection dependency. A local notification
fires on the device's own clock. On native the server push is no longer booked at all, so
the alert doesn't double. The web build still uses the push.

### The Live Activity belongs to the workout, not to the rest period
Scoping it to the rest timer meant Skip destroyed the card. It now goes up when a session
opens and comes down when the session is finished or discarded, so everything that changes
mid-session — exercise, set numbers, target, end time — lives in the mutable content state,
and the immutable attributes hold only the session start.

**How to apply:** anything that varies during a workout goes in the content state.
Changing the attributes forces the activity to be replaced, which restarts the card.

### Native owns the rest end-time while an activity is live
The Live Activity's buttons run as intents in the app's process, and they cannot write to
the webview's `localStorage` where the rest timer lives. So the native store is
authoritative for the end time, and JS adopts it on `visibilitychange`. Intents in the app
process also mean plain `UserDefaults` suffices — no App Group entitlement, which free
provisioning cannot add anyway.

**How to apply:** this is the first place the webview architecture actually costs
something. Any future native control that mutates app state needs the same
reconcile-on-resume treatment.

### Four ActivityKit and WidgetKit behaviours worth knowing
Each of these cost real debugging time:

- **`Text(timerInterval:)` truncates in five distinct ways.** A `maxWidth` truncates to
  `1:--`. `.fixedSize()` **crashes the widget process** inside a ProgressView's
  GeometryReader, which silently stops the card rendering at all. An unprioritised
  `minWidth` loses the width negotiation to a long label, and `minWidth` alone raises only
  the resulting frame, never the width *proposed* to the Text. A range spanning hours
  reserves space for `h:mm:ss` and renders `38:--` at any width. Give every timer label a
  definite width plus a layout priority, and keep the range no longer than the value needs.
  For a counting-up elapsed time, don't use a live timer at all — compute the string at
  update time.
- **Don't end an activity early with `dismissalPolicy: .after(date)`.** Measured on iOS
  26.5 it dismisses *immediately*, not at the date — the countdown vanished the moment the
  app was backgrounded, deleting the feature exactly when it's meant to work. Use a stale
  date to mark a finished rest and end the activity when the app next becomes active.
- **"One activity at a time" means asking the system, not tracking it locally.** The
  in-memory handle is empty after every relaunch, but ActivityKit keeps the activity alive
  across relaunches — so the sync concluded nothing was running and requested a second
  card, one per restart. It now reads the live activity list and adopts the one whose
  attributes still match, so the progress bar doesn't restart.
- **Register app-target plugins in `capacitorDidLoad()`, never in the generated config's
  plugin list.** Adding a class there does work — until the next `npx cap sync`, which
  rewrites that array from the installed npm plugins and silently drops the entry, taking
  the feature with it. Treat anything in the generated config as disposable. Similarly, the
  scene delegate builds the root controller in code, so setting a custom class in the
  storyboard has no effect at all; two separate view-controller subclasses appeared to "not
  fire" before that was found.

### Landscape is unsupported, not untested
Rotating the phone to landscape made iOS rescale the page to the landscape layout width
and never restore it on the way back — returning to portrait left the dashboard calendar
stuck at roughly 2.2x with three weekday columns visible. Nothing in the app's own CSS
pins a width; the grid reflows correctly at any size. The choice was between building a
landscape layout for the calendar and declaring the orientation unsupported, and every
surface here is a one-handed phone surface — logging a set, reading the calendar, the rest
timer — that gains nothing from a wider viewport. So the iPhone target is portrait-only in
`Info.plist`, the web manifest declares `"orientation": "portrait"`, and `html` pins
`-webkit-text-size-adjust: 100%` so iOS can't rescale the layout on its own.

Only the native lock is a real lock: iOS ignores the manifest's `orientation` key for
home-screen PWAs, so the installed web app is relying on the autosizing pin alone. The
remaining lever there is `maximum-scale=1` on the viewport meta, left off deliberately
because it also disables pinch-zoom.

**How to apply:** when a bug occurs only in a mode the product has no use for, removing the
mode beats supporting it — but write that down, because "we never tested landscape" and
"landscape is unsupported" are indistinguishable from the code.
