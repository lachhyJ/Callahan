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
// re-checks against the slot on every persist. Both exist because a start time
// banked as "whenever the webview last came up" silently truncates the session:
// a workout logged 23:43 → 01:10 was saved as starting 01:07, three minutes
// long. Remounts, webview reloads and native reconciles all happen mid-workout;
// none of them may push the start forward.
//
// Tolerates a slot written by an older build (no `startedAt`) and a corrupted
// value, both of which used to yield an Invalid Date that then threw out of the
// persist effect on `.toISOString()`.
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
