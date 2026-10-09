import { Capacitor } from '@capacitor/core'
import { Haptics, ImpactStyle } from '@capacitor/haptics'

const isNative = Capacitor.isNativePlatform()

// A light tap confirming a set was just marked complete. No-op on the web —
// the Vibration API's `navigator.vibrate` isn't worth wiring up for a single
// confirmation buzz that most desktop/browser testing can't feel anyway.
export function tapSetComplete() {
  if (!isNative) return
  Haptics.impact({ style: ImpactStyle.Light }).catch(() => {})
}

// A firmer tap when a long press registers, so the popup that follows feels
// like it was caused by the hold rather than appearing on its own.
export function pressHeld() {
  if (!isNative) return
  Haptics.impact({ style: ImpactStyle.Medium }).catch(() => {})
}
