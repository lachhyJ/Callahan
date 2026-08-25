// Pre-select-only guess at a run's session type, from aggregate pace/duration
// alone — we don't pull lap/split data from Garmin, so there's no way to see
// the actual work/rest structure of an interval session, just its overall
// average. Wrong guesses cost nothing (the picker still requires a tap to
// confirm), so this stays a coarse heuristic rather than something that
// needs to be precise.
//
// Easy Aerobic Run is continuous, so its average pace is close to Lachlan's
// real easy pace (~5-6 min/km). High Speed Intervals and Speed & Acceleration
// both involve real standing rest between reps, which drags the whole-session
// average pace down well past that regardless of how fast the actual work
// bursts are — so a materially slower average pace means one of those two,
// distinguished from each other by duration (Intervals ~30-40min, Speed &
// Acceleration ~45-55min per the program).
const EASY_PACE_CUTOFF_MIN_PER_KM = 7
const INTERVALS_MAX_DURATION_MIN = 42

export function suggestRunSessionType(activity, sessionTypes) {
  if (activity.activitySessionTypeId) return null
  if (!activity.distanceKm || !activity.durationSeconds) return null

  const paceMinPerKm = activity.durationSeconds / 60 / activity.distanceKm
  const durationMinutes = activity.durationSeconds / 60

  const suggestedName = paceMinPerKm <= EASY_PACE_CUTOFF_MIN_PER_KM
    ? 'Easy Aerobic Run'
    : durationMinutes <= INTERVALS_MAX_DURATION_MIN
      ? 'High Speed Intervals'
      : 'Speed & Acceleration'

  return sessionTypes.find((t) => t.name === suggestedName) ?? null
}
