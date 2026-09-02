import { Capacitor, registerPlugin } from '@capacitor/core'

// Lock-screen / Dynamic Island rest timer. Native only: the web build has no
// equivalent and the PWA keeps relying on the push notification alone.
//
// The plugin's sync() is start-or-update, so callers can just declare the
// current timer state and let the native side work out whether that means
// starting a new activity or moving an existing one's end time.
const RestActivity = registerPlugin('RestActivity')

const available = Capacitor.isNativePlatform()

export function syncRestActivity(restTimer) {
  if (!available) return
  RestActivity.sync({
    endAt: restTimer.endAt,
    totalSeconds: restTimer.totalSeconds,
    exerciseName: restTimer.exerciseName ?? 'Rest',
    targetReps: restTimer.targetReps == null ? '' : String(restTimer.targetReps),
    nextSetNumber: restTimer.nextSetNumber ?? 1,
    totalSets: restTimer.totalSets ?? 1,
  }).catch(() => {
    // A Live Activity is a nicety on top of the push notification — if the user
    // has them switched off, or iOS declines, the timer itself is unaffected.
  })
}

export function endRestActivity() {
  if (!available) return
  RestActivity.end().catch(() => {})
}
