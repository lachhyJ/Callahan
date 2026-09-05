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

// The earlier of the candidate and whatever is already banked for this session.
export function earliestStartedAt(sessionKey, candidate) {
  const banked = restoreStartedAt(sessionKey, candidate)
  return banked.getTime() < candidate.getTime() ? banked : candidate
}
