import { createPersistedSlot } from './persistedSlot'
import { setSlotFields } from './utils/setSlot'

// The in-progress workout, so closing the tab or wandering off to the
// dashboard mid-session doesn't lose it. Keyed by templateId ('custom' for a
// template-less session), which is what the resume links interpolate back
// into a URL.
const slot = createPersistedSlot('callahan_active_workout', 'callahan-active-workout-changed')

export const saveActiveWorkout = slot.save
export const loadActiveWorkout = slot.load
export const clearActiveWorkout = slot.clear
export const onActiveWorkoutChange = slot.onChange

// A session's start time only ever moves earlier, never later.
//
// The page seeds its `startedAt` from here rather than from `new Date()`, and
// re-checks against the slot on every persist. Defensive: a start time that
// drifts forward silently shortens the recorded session, and nothing surfaces
// that except the header clock. No path is known to do it today — this is here
// so one cannot appear unnoticed.
//
// Tolerates a slot written by an older build (no `startedAt`) and a corrupted
// value, both of which would otherwise yield an Invalid Date that throws out of
// the persist effect on `.toISOString()`. Note `new Date(null)` is the epoch,
// which is *valid* — so emptiness needs its own check, not just a NaN guard.
export function restoreStartedAt(sessionKey, fallback = new Date()) {
  const saved = slot.load()
  if (!saved || saved.templateId !== sessionKey || !saved.startedAt) return fallback
  const restored = new Date(saved.startedAt)
  return Number.isNaN(restored.getTime()) ? fallback : restored
}

// A logged set on a time-based exercise: held for `durationSeconds`, never
// counted in reps. A non-empty `durationSeconds` is the marker every reader
// keys off, mirroring the backend's `DurationSeconds != null`.
export function isTimeSet(s) {
  return s.durationSeconds !== '' && s.durationSeconds != null
}

// How a set's load reads to the watch and Live Activity. A timed hold has no
// weight or reps — only a duration — so it is carried as a pre-formatted
// `holdLabel` ("30s" / "30s/side"), which the consumers show in the slot
// weight x reps would otherwise fill. Falls back to the exercise's target
// duration when the set itself has none typed yet.
function holdLabelFor(ex, set) {
  if (!ex.isTimeBased || !set) return ''
  const seconds = Number(set.durationSeconds) || Number(ex.targetDurationSeconds) || 0
  if (seconds <= 0) return ''
  return `${seconds}s${ex.isPerSide ? '/side' : ''}`
}

// The earlier of the candidate and whatever is already banked for this session.
export function earliestStartedAt(sessionKey, candidate) {
  const banked = restoreStartedAt(sessionKey, candidate)
  return banked.getTime() < candidate.getTime() ? banked : candidate
}

// How many of an exercise's rows are warmups. They sit contiguously at the top
// of the table (see setType in ActiveWorkoutPage), so the overall set number
// alone says whether a set is a warmup: n <= warmupSets. Sent alongside
// nextSetNumber/totalSets so every surface can show "W1/2" then "Set 1/4"
// without the native card's number-advancing logic needing to know.
// The lowest-ordered working set from last session that no current row is
// showing — what a re-added set should pick up after one was deleted. A middle
// deletion renumbers the rows but each keeps its own `previous`, so matching by
// "next setOrder" would hand the new row a duplicate of the last one's. Null
// when every working set is already on screen (a genuinely extra set).
export function unusedPreviousSet(previousSets, sets) {
  const used = new Set(sets.map((s) => s.previous?.setOrder).filter((o) => o != null))
  return (
    (previousSets ?? [])
      .filter((p) => p.setType !== 'Warmup' && !used.has(p.setOrder))
      .sort((a, b) => a.setOrder - b.setOrder)[0] ?? null
  )
}

export function warmupCount(ex) {
  return ex.sets.filter((s) => s.type === 'Warmup').length
}

// What the Live Activity should describe when no rest is running: the first
// exercise that still has an unticked set. Keeps the card meaningful for the
// whole session rather than only in the gap after a set. `fromIdx` starts the
// scan partway down the list.
//
// There's no "just ticked this" anchor available here (this is an ambient
// snapshot — synced on load, or after Skip), so a superset group resolves via
// groupMemberDueNext rather than nextIncompleteInGroup: whichever member has
// completed the fewest sets so far is the one whose turn is next in the
// round-robin, which only holds because sets are always ticked in order.
export function nextSetDescriptor(exercises, fromIdx = 0, withFollowing = true) {
  if (!exercises) return null
  const due = ambientTarget(exercises, Math.max(0, fromIdx))
  if (due) {
    const { i: ti, j } = due
    const [groupStart, groupEnd] = supersetGroupBounds(exercises, ti)
    const ex = exercises[ti]
    return {
      exerciseName: ex.exerciseName,
      targetReps: ex.targetReps,
      targetWeightKg: ex.sets[j].weightKg,
      isBodyweight: ex.isBodyweight ?? false,
      enteredReps: ex.sets[j].reps,
      holdLabel: holdLabelFor(ex, ex.sets[j]),
      nextSetNumber: j + 1,
      totalSets: ex.sets.length,
      warmupSets: warmupCount(ex),
      restSeconds: groupRestSeconds(exercises, groupStart, groupEnd, ti),
      // Whether ticking *this* set should fire a rest at all — the lock-screen
      // card needs this to make the same call the checkbox does in
      // armRestAfterSet, since it cannot call back into JS to ask.
      isLastInSuperset: !suppressesRest(exercises, ti),
      following: withFollowing ? followingDescriptor(exercises, ti, j) : null,
    }
  }
  return null
}

// What the lock-screen card should read once its own last set of an exercise
// is ticked. The card can only count set numbers up; it cannot look ahead into
// the workout, so without this it says "Last set done" straight through the
// rest that follows. Null unless (ti, j) is that exercise's last remaining set;
// `{ workoutDone: true }` when nothing at all is left after it. Built by ticking a copy and asking what the
// ambient card would then describe — the same answer the app reaches on its
// next sync, so the card doesn't jump when the webview wakes.
function followingDescriptor(exercises, ti, j) {
  const ex = exercises[ti]
  if (ex.sets.some((s, k) => k !== j && !s.completed)) return null
  const ticked = exercises.map((e, idx) =>
    idx !== ti ? e : { ...e, sets: e.sets.map((s, k) => (k === j ? { ...s, completed: true } : s)) }
  )
  // Nothing left after it: say so, and let the caller supply the label (only the
  // page knows whether a finisher has been added — "Finisher?" vs "Finished").
  return nextSetDescriptor(ticked, 0, false) ?? { workoutDone: true }
}

// The rest duration for a set inside superset group [groupStart, groupEnd]:
// the group's own configured rest (supersetRestSeconds, stored on its first
// member) while more than one member still has work — one shared config for
// the whole rotation, not any individual exercise's own field. Once the
// rotation is down to a single member with work left, that exercise's own
// restSeconds takes over instead, since the group timer no longer means
// anything once there's nobody left to rotate to.
//
// Two earlier versions of this got it wrong: always using the first
// member's own restSeconds (breaks as soon as the group's real config
// differs from that field's purpose) and "ownership shifts to whichever
// member is furthest behind" (conflated the group's rest with an
// individual exercise's, so a group of >1 active members still ended up
// borrowing one specific member's own field). A dedicated group-level field
// removes the ambiguity entirely. Outside a group (groupStart === groupEnd)
// the exercise's own restSeconds applies, as for any standalone exercise.
function groupRestSeconds(exercises, groupStart, groupEnd, exIdx) {
  if (groupEnd === groupStart) return exercises[exIdx].restSeconds || 90
  const solo = soleActiveGroupMember(exercises, groupStart, groupEnd)
  if (solo != null) return exercises[solo].restSeconds || 90
  return exercises[groupStart].supersetRestSeconds || 90
}

// The single group member still with incomplete sets, or null if more than
// one (or none) still have work. "More than one active" is exactly when the
// group's shared rest config applies instead of any one member's own.
function soleActiveGroupMember(exercises, groupStart, groupEnd) {
  let solo = null
  for (let i = groupStart; i <= groupEnd; i++) {
    if (!exercises[i].sets.some((s) => !s.completed)) continue
    if (solo != null) return null // a second active member — not solo
    solo = i
  }
  return solo
}

// Whether exIdx's own restSeconds field is the one actually read right now —
// true for a lone exercise, or for a superset member once it's the sole
// remaining active member of its group (see groupRestSeconds); false
// whenever the group's shared supersetRestSeconds governs instead. This
// shifts as a round-robin progresses through uneven set counts, unlike
// suppressesRest, which answers a different, per-tick question ("would
// completing my next set fire a rest at all"). Used to decide which
// rest-seconds fields the UI should show as editable.
export function isSupersetRestOwner(exercises, exIdx) {
  const [groupStart, groupEnd] = supersetGroupBounds(exercises, exIdx)
  if (groupEnd === groupStart) return true
  return soleActiveGroupMember(exercises, groupStart, groupEnd) === exIdx
}

// Whether exIdx is the group member whose supersetRestSeconds field is the
// group's shared config — always the group's first member, a fixed fact
// about position (never shifts, unlike isSupersetRestOwner). Used to decide
// which single card shows the group-level rest control.
export function isSupersetGroupConfigOwner(exercises, exIdx) {
  const [groupStart, groupEnd] = supersetGroupBounds(exercises, exIdx)
  return groupEnd > groupStart && groupStart === exIdx
}

// Whether the group's shared supersetRestSeconds currently governs (more
// than one member still has work) rather than some individual member's own
// restSeconds having taken over. Used to dim the group-level rest control
// once the rotation is down to its last active member.
export function isSupersetGroupRestActive(exercises, exIdx) {
  const [groupStart, groupEnd] = supersetGroupBounds(exercises, exIdx)
  if (groupEnd === groupStart) return false
  return soleActiveGroupMember(exercises, groupStart, groupEnd) == null
}

// Within [groupStart, groupEnd], the member due next in a round-robin
// rotation when there's no specific "just ticked" exercise to cycle forward
// from: whichever member still has work left with the fewest sets completed
// so far, ties broken by group order. See nextIncompleteInGroup for the
// anchored version, used right after a tick.
function groupMemberDueNext(exercises, groupStart, groupEnd) {
  let best = null
  for (let i = groupStart; i <= groupEnd; i++) {
    const j = exercises[i].sets.findIndex((s) => !s.completed)
    if (j === -1) continue
    if (!best || j < best.j) best = { i, j }
  }
  return best
}

// The group member whose turn is next, cycling forward from `fromIdx` and
// wrapping within [groupStart, groupEnd] — never rescanning from the group's
// top, which would land back on an earlier member for as long as it has any
// later round left (every round but its last). Used right after ticking a
// set, where `fromIdx` is the exercise just ticked.
export function nextIncompleteInGroup(exercises, groupStart, groupEnd, fromIdx) {
  const span = groupEnd - groupStart + 1
  for (let step = 1; step <= span; step++) {
    const i = groupStart + ((fromIdx - groupStart + step) % span)
    const j = exercises[i].sets.findIndex((s) => !s.completed)
    if (j !== -1) return { i, j }
  }
  return null
}

// Which set is due next with nothing just ticked to anchor from: the first
// exercise with work left, resolved through its superset's rotation. This is
// nextSetDescriptor's scan, shared so a card press completes exactly the set
// the card was showing.
function ambientTarget(exercises, fromIdx) {
  for (let i = fromIdx; i < exercises.length; i++) {
    if (exercises[i].sets.every((s) => s.completed)) continue
    const [groupStart, groupEnd] = supersetGroupBounds(exercises, i)
    const target = groupEnd > groupStart ? groupMemberDueNext(exercises, groupStart, groupEnd) : null
    const j = target ? target.j : exercises[i].sets.findIndex((s) => !s.completed)
    return { i: target ? target.i : i, j }
  }
  return null
}

// Which set is due right after ticking one on exIdx — restDescriptorAfterSet's
// targeting: a second (or third...) native completion in the same batch should
// land on whoever's turn is next in rotation, not just the next row of
// whichever exercise happens to sit first in the array.
function targetAfterTick(exercises, exIdx) {
  const [groupStart, groupEnd] = supersetGroupBounds(exercises, exIdx)
  if (groupEnd > groupStart) {
    const next = nextIncompleteInGroup(exercises, groupStart, groupEnd, exIdx)
    if (next) return next
  }
  if (exercises[exIdx].sets.some((s) => !s.completed)) {
    return { i: exIdx, j: exercises[exIdx].sets.findIndex((s) => !s.completed) }
  }
  return ambientTarget(exercises, groupEnd + 1)
}

// Fold sets ticked from the Live Activity into the workout's own state.
//
// The card can only bank a count — it has no access to the set rows — so this
// walks forward from wherever the workout is now, ticking whichever set is
// actually due next in rotation order (not just array order — a mid-superset
// press otherwise lands on the wrong exercise's set, completing member A's
// row ahead of B's turn even when B was due). A set confirmed from a locked
// phone has no typed reps, so the programmed target stands in; that is what
// the athlete was being asked to do and what the card was showing them when
// they pressed it. Sets with neither a typed nor a target rep count are left
// alone rather than saved as a blank.
export function applyNativeCompletions(exercises, count) {
  if (!exercises || count <= 0) return null
  const next = exercises.map((ex) => ({ ...ex, sets: ex.sets.map((set) => ({ ...set })) }))
  let applied = 0
  let target = ambientTarget(next, 0)
  while (target && applied < count) {
    const ex = next[target.i]
    const set = ex.sets[target.j]
    const reps = set.reps !== '' ? set.reps : (ex.targetReps ?? '')
    if (reps === '' || reps == null) break
    set.reps = String(reps)
    set.completed = true
    applied += 1
    target = targetAfterTick(next, target.i)
  }
  return applied > 0 ? { exercises: next, applied } : null
}

// Advance a hold countdown at a 1s tick. Returns null while time remains on the
// current phase; at zero, one of:
//   { type: 'finish', seconds }            — record the set and start the rest
//   { type: 'advance', beep, holdTimer }   — beep, then keep counting the returned timer
//
// A per-side exercise runs: hold side 1 → (beep) → `gap` phase of
// `delaySeconds` → (beep) → hold side 2 → (beep) → finish. `delaySeconds === 0`
// skips the gap, so side 1 ending goes straight to side 2 with a single beep.
// `ex` is the exercise the timer sits on (read for `isPerSide`); `nowMs` is the
// tick time, passed in so this stays pure.
export function advanceHold(holdTimer, ex, nowMs) {
  if (!holdTimer) return null
  if (Math.round((holdTimer.endsAt - nowMs) / 1000) > 0) return null

  if (holdTimer.phase === 'gap') {
    return {
      type: 'advance',
      beep: true,
      holdTimer: { ...holdTimer, phase: 'hold', side: 2, endsAt: nowMs + holdTimer.targetSeconds * 1000 },
    }
  }

  if (ex?.isPerSide && holdTimer.side === 1) {
    const patch = holdTimer.delaySeconds > 0
      ? { phase: 'gap', endsAt: nowMs + holdTimer.delaySeconds * 1000 }
      : { side: 2, endsAt: nowMs + holdTimer.targetSeconds * 1000 }
    return { type: 'advance', beep: true, holdTimer: { ...holdTimer, ...patch } }
  }

  return { type: 'finish', beep: true, seconds: holdTimer.targetSeconds }
}

// [startIdx, endIdx] (inclusive) of the maximal superset run containing exIdx —
// a contiguous span where every member but the last carries `supersetWithNext`.
// A lone exercise returns [exIdx, exIdx]. Adjacency is array order, which is
// session order, so this is all it takes to describe a superset.
export function supersetGroupBounds(exercises, exIdx) {
  let start = exIdx
  while (start > 0 && exercises[start - 1]?.supersetWithNext) start--
  let end = exIdx
  while (exercises[end]?.supersetWithNext) end++
  return [start, end]
}

// Completing set `setIdx` on exIdx should not fire a rest as long as some
// *later* group member still owes that same round — i.e. still has a set at
// that index and hasn't done it yet. Only whoever closes out the round rests,
// standing in for the whole group. Defaults `setIdx` to exIdx's own next
// incomplete set, for callers asking "if this exercise's next set were
// ticked" rather than about one that already was (the per-card UI dimming,
// and nextSetDescriptor's ambient isLastInSuperset both want that shape).
//
// A static "am I flagged as the group's last exercise" check gets this wrong
// once set counts are uneven (e.g. 5+3+3): once the two 3-set members are
// exhausted, the 5-set exercise's own remaining sets (4th, 5th) are the only
// work left in the group, and no later member has an equivalent round left
// to owe — so they need to rest between themselves like a normal exercise,
// rather than suppressing forever just because they aren't the exercise
// flagged as "last". Assumes each exercise's own sets are ticked in order,
// same as the rest of this module (e.g. groupMemberDueNext).
export function suppressesRest(exercises, exIdx, setIdx = exercises[exIdx]?.sets.findIndex((s) => !s.completed)) {
  const [, groupEnd] = supersetGroupBounds(exercises, exIdx)
  for (let i = exIdx + 1; i <= groupEnd; i++) {
    const s = exercises[i].sets[setIdx]
    if (s && !s.completed) return true
  }
  return false
}

// The rest descriptor to arm after ticking the set at (exIdx, setIdx), given
// the post-tick `exercises` array. Normally it points at the next set of the
// same exercise. But when the ticked set was that exercise's last remaining
// one, it rolls over to the first still-unticked set anywhere in the session so
// the card shows what's genuinely next rather than a dead "set 4 of 3" line for
// the whole rest. When nothing is left it falls back to the same-exercise
// over-the-end descriptor (nextSetNumber > totalSets), which the native card
// renders as "Last set done".
export function restDescriptorAfterSet(exercises, exIdx, setIdx) {
  const ex = exercises[exIdx]
  // Inside a superset, cycle forward from the exercise just ticked for
  // whoever's turn is next — including wrapping back to this exercise's own
  // next set when it's the only member with work left. An uneven-count group
  // (e.g. 5+3+3) can leave *any* member as the odd one out once the others
  // are exhausted, not only the one flagged as the group's last exercise, so
  // this can't just scan from the group's top the way it used to.
  const [groupStart, groupEnd] = supersetGroupBounds(exercises, exIdx)
  const restSeconds = groupRestSeconds(exercises, groupStart, groupEnd, exIdx)

  const sameExercise = {
    exerciseName: ex.exerciseName,
    targetReps: ex.targetReps,
    targetWeightKg: ex.sets[setIdx + 1]?.weightKg,
    isBodyweight: ex.isBodyweight ?? false,
    enteredReps: ex.sets[setIdx + 1]?.reps,
    holdLabel: holdLabelFor(ex, ex.sets[setIdx + 1]),
    nextSetNumber: setIdx + 2,
    totalSets: ex.sets.length,
    warmupSets: warmupCount(ex),
    restSeconds,
    // Reached only when nothing else in this exercise's group (if any) has
    // work left, so this exercise's own next set is never mid-rotation.
    isLastInSuperset: true,
  }

  if (groupEnd > groupStart) {
    const next = nextIncompleteInGroup(exercises, groupStart, groupEnd, exIdx)
    if (next) {
      if (next.i === exIdx) return sameExercise
      const nx = exercises[next.i]
      return {
        exerciseName: nx.exerciseName,
        targetReps: nx.targetReps,
        targetWeightKg: nx.sets[next.j].weightKg,
        isBodyweight: nx.isBodyweight ?? false,
        enteredReps: nx.sets[next.j].reps,
        holdLabel: holdLabelFor(nx, nx.sets[next.j]),
        nextSetNumber: next.j + 1,
        totalSets: nx.sets.length,
        warmupSets: warmupCount(nx),
        restSeconds,
        isLastInSuperset: !suppressesRest(exercises, next.i),
      }
    }
  }

  if (ex.sets.some((s) => !s.completed)) return sameExercise
  // Whole exercise (and, if it was in one, its whole group) is done — move on
  // to whatever's next in session order. Scans from just past the group
  // rather than from the top of the session: an earlier, unrelated exercise
  // with leftover incomplete work (skipped past on the way into this one)
  // shouldn't be resurrected just because this group finished.
  //
  // Nothing left anywhere ahead: sameExercise then describes a set past the
  // end, which native reads as "Last set done". workoutDone says so outright,
  // for surfaces (the Garmin field) that would otherwise show that phantom
  // set's load.
  //
  // The rest still belongs to the exercise just finished: it describes the
  // rest *after* the set that was ticked, so it keeps that exercise's own
  // restSeconds rather than the next exercise's (decided 2026-09-22).
  const upcoming = nextSetDescriptor(exercises, groupEnd + 1)
  // `cardRestSeconds` keeps the upcoming set's own rest for the lock-screen
  // card, which reads it as "the rest that ticking the set I'm pointed at
  // starts" — that is the next exercise's, not the countdown running now.
  return upcoming
    ? { ...upcoming, restSeconds, cardRestSeconds: upcoming.restSeconds }
    : { ...sameExercise, workoutDone: true }
}

// True when the weight or reps typed into the set a rest is waiting on no
// longer match what the rest was scheduled with (the Live Activity card and the
// watch both read the scheduled copy). Only the same set counts: a different
// exercise or set number is a different rest, not an edit to this one.
export function restSlotDrifted(rest, upcoming) {
  if (!rest || !upcoming) return false
  if (upcoming.exerciseName !== rest.exerciseName || upcoming.nextSetNumber !== rest.nextSetNumber) return false
  const scheduled = setSlotFields(rest)
  const current = setSlotFields(upcoming)
  return scheduled.targetWeight !== current.targetWeight || scheduled.enteredReps !== current.enteredReps
}
