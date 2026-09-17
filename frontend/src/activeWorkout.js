import { createPersistedSlot } from './persistedSlot'

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

// The earlier of the candidate and whatever is already banked for this session.
export function earliestStartedAt(sessionKey, candidate) {
  const banked = restoreStartedAt(sessionKey, candidate)
  return banked.getTime() < candidate.getTime() ? banked : candidate
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
export function nextSetDescriptor(exercises, fromIdx = 0) {
  if (!exercises) return null
  for (let i = Math.max(0, fromIdx); i < exercises.length; i++) {
    if (exercises[i].sets.every((s) => s.completed)) continue
    const [groupStart, groupEnd] = supersetGroupBounds(exercises, i)
    const target = groupEnd > groupStart ? groupMemberDueNext(exercises, groupStart, groupEnd) : null
    const j = target ? target.j : exercises[i].sets.findIndex((s) => !s.completed)
    const ex = exercises[target ? target.i : i]
    return {
      exerciseName: ex.exerciseName,
      targetReps: ex.targetReps,
      targetWeightKg: ex.sets[j].weightKg,
      isBodyweight: ex.isBodyweight ?? false,
      enteredReps: ex.sets[j].reps,
      nextSetNumber: j + 1,
      totalSets: ex.sets.length,
      restSeconds: groupRestSeconds(exercises, groupStart, groupEnd, target ? target.i : i),
      // Whether ticking *this* set should fire a rest at all — the lock-screen
      // card needs this to make the same call the checkbox does in
      // armRestAfterSet, since it cannot call back into JS to ask.
      isLastInSuperset: !suppressesRest(exercises, target ? target.i : i),
    }
  }
  return null
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
    nextSetNumber: setIdx + 2,
    totalSets: ex.sets.length,
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
        nextSetNumber: next.j + 1,
        totalSets: nx.sets.length,
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
  return nextSetDescriptor(exercises, groupEnd + 1) ?? sameExercise
}
