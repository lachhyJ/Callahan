// Pre-select-only guess at a run's session type. Wrong guesses cost nothing
// (the picker still requires a tap to confirm), so this stays a heuristic
// rather than something that needs to be precise.
//
// Easy Aerobic Run is continuous, so its average pace is close to Lachlan's
// real easy pace (~5-6 min/km). High Speed Intervals and Speed & Acceleration
// both involve real standing rest between reps, which drags the whole-session
// average pace down well past that regardless of how fast the actual work
// bursts are — so a materially slower average pace means one of those two.
const EASY_PACE_CUTOFF_MIN_PER_KM = 7
const INTERVALS_MAX_DURATION_MIN = 42

// Once lap data exists (see garmin_sync.py's lap sync, 2026-08-25), distance
// per work rep distinguishes the two far better than duration does — HS
// Intervals reps run ~70-80m (confirmed via --dump-laps against a real
// session: 1168m / 16 active laps ≈ 73m), Speed & Acceleration's short accel
// sprints are ~40-60m. Below this cutoff means short sprints.
const ACCEL_REP_DISTANCE_CUTOFF_M = 65

export function suggestRunSessionType(activity, sessionTypes) {
  if (activity.activitySessionTypeId) return null

  const suggestedName = suggestByLaps(activity) ?? suggestByPaceAndDuration(activity)
  if (!suggestedName) return null

  return sessionTypes.find((t) => t.name === suggestedName) ?? null
}

function suggestByLaps(activity) {
  if (!activity.activeLapCount || activity.highSpeedDistanceKm == null) return null
  // Real interval structure confirmed (multiple ACTIVE laps) — this alone
  // rules out Easy Aerobic Run, which has none.
  const avgActiveLapDistanceM = (activity.highSpeedDistanceKm * 1000) / activity.activeLapCount
  return avgActiveLapDistanceM < ACCEL_REP_DISTANCE_CUTOFF_M ? 'Speed & Acceleration' : 'High Speed Intervals'
}

// Fallback for activities synced before lap data existed, or where Garmin
// didn't record real per-lap structure (e.g. a plain "Run" profile with no
// lap presses) — coarser, but still better than no suggestion.
function suggestByPaceAndDuration(activity) {
  if (!activity.distanceKm || !activity.durationSeconds) return null

  const paceMinPerKm = activity.durationSeconds / 60 / activity.distanceKm
  const durationMinutes = activity.durationSeconds / 60

  return paceMinPerKm <= EASY_PACE_CUTOFF_MIN_PER_KM
    ? 'Easy Aerobic Run'
    : durationMinutes <= INTERVALS_MAX_DURATION_MIN
      ? 'High Speed Intervals'
      : 'Speed & Acceleration'
}
