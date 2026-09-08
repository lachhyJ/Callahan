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
// whole session rather than only in the gap after a set.
export function nextSetDescriptor(exercises) {
  if (!exercises) return null
  for (const ex of exercises) {
    const idx = ex.sets.findIndex((s) => !s.completed)
    if (idx === -1) continue
    return {
      exerciseName: ex.exerciseName,
      targetReps: ex.targetReps,
      targetWeightKg: ex.sets[idx].weightKg,
      enteredReps: ex.sets[idx].reps,
      nextSetNumber: idx + 1,
      totalSets: ex.sets.length,
      restSeconds: ex.restSeconds || 90,
    }
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
  const sameExercise = {
    exerciseName: ex.exerciseName,
    targetReps: ex.targetReps,
    targetWeightKg: ex.sets[setIdx + 1]?.weightKg,
    enteredReps: ex.sets[setIdx + 1]?.reps,
    nextSetNumber: setIdx + 2,
    totalSets: ex.sets.length,
    restSeconds: ex.restSeconds || 90,
  }
  if (ex.sets.some((s) => !s.completed)) return sameExercise
  return nextSetDescriptor(exercises) ?? sameExercise
}
