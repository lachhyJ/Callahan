import { Capacitor, registerPlugin } from '@capacitor/core'

// Lock-screen / Dynamic Island rest timer. Native only: the web build has no
// equivalent and the PWA keeps relying on the push notification alone.
//
// sync() is start-or-update, so callers can just declare the current timer state
// and let the native side work out whether that means starting a new activity or
// moving an existing one's end time.
const RestActivity = registerPlugin('RestActivity')

const available = Capacitor.isNativePlatform()

function formatWeight(weightKg) {
  const n = Number(weightKg)
  if (!weightKg || Number.isNaN(n) || n === 0) return ''
  return `${Number.isInteger(n) ? n : Math.round(n * 10) / 10} kg`
}

// The activity belongs to the workout, not to a rest period: it goes up when a
// session starts and comes down when it is finished or discarded, so Skip zeroes
// the countdown instead of tearing the card down. `rest` is null between sets.
export function syncWorkoutActivity({ rest, sessionStartedAt, lastSet } = {}) {
  if (!available) return
  const detail = rest ?? lastSet ?? {}
  RestActivity.sync({
    endAt: rest ? rest.endAt : undefined,
    totalSeconds: rest ? rest.totalSeconds : 0,
    exerciseName: detail.exerciseName ?? 'Workout',
    targetReps: detail.targetReps == null ? '' : String(detail.targetReps),
    targetWeight: formatWeight(detail.targetWeightKg),
    nextSetNumber: detail.nextSetNumber ?? 1,
    totalSets: detail.totalSets ?? 1,
    sessionStartedAt: sessionStartedAt ?? Date.now(),
  }).catch(() => {
    // A Live Activity is a nicety on top of the push notification — if the user
    // has them switched off, or iOS declines, the timer itself is unaffected.
  })
}

export function endWorkoutActivity() {
  if (!available) return
  RestActivity.end().catch(() => {})
}

// While the app is backgrounded the Live Activity's -15s/+15s/Skip buttons are
// the only way to change the timer, and they cannot reach this webview's
// localStorage — so native is authoritative for endAt until we come back. Ask it
// what happened and adopt the answer.
export async function readNativeRestState() {
  if (!available) return null
  try {
    return await RestActivity.getState()
  } catch {
    return null
  }
}
