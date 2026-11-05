import { nextSetDescriptor } from './activeWorkout'
import { setPosition } from './utils/setSlot'

// The notification body for a rest: what you are resting for.
export function nextSetLabel(rest) {
  const parts = [rest.exerciseName]
  const position = setPosition(rest, true)
  if (position) parts.push(position)
  return parts.filter(Boolean).join(' · ') || 'Next set.'
}

// A rest the card started while this webview was suspended: native owns endAt,
// and the descriptor is rebuilt from wherever the workout has now got to.
export function restTimerFromNative(exercises, native) {
  const detail = nextSetDescriptor(exercises)
  if (!detail) return null
  // Spread the whole descriptor so nothing the sync/schedule paths read
  // (bodyweight, hold label, superset flag) is silently dropped.
  return {
    ...detail,
    endAt: native.endAt,
    totalSeconds: native.totalSeconds || detail.restSeconds,
    timerId: null,
    doneLabel: '',
  }
}

// What to do with the local rest timer once native's state has been read back
// on resume. The Live Activity's buttons mutate the timer natively while the
// webview is suspended, so native's word wins. Pure: the caller applies the
// result (state, server timer) so side effects stay out of state updaters,
// which StrictMode runs twice.
//
// `exercises` must already include any sets ticked from the card, so an adopted
// rest is described by the set the workout has actually advanced to.
//
// Returns one of:
//   { kind: 'none' }
//   { kind: 'clear', cancelTimerId }                 native has no rest; drop ours
//   { kind: 'set', rest, resyncEndAt }               adopt or move to native's rest;
//                                                    resyncEndAt non-null means the
//                                                    server timer must be rescheduled
export function reconcileRest(prev, native, exercises, nowMs) {
  // Native only expires `RestTimerStore` on its own didBecomeActive, which fires
  // after visibilitychange, so a rest that finished in the background can read
  // back as active with a past endAt. Reviving it would hand an elapsed endAt
  // to the native beep ("too close to arm, just sound it").
  const active = native.active && !(native.endAt && native.endAt <= nowMs)

  if (!active) {
    if (!prev) return { kind: 'none' }
    return { kind: 'clear', cancelTimerId: prev.timerId ?? null }
  }

  // A rest started from the card with no local timer: "Set done" on a locked phone.
  if (!prev) {
    const adopted = restTimerFromNative(exercises, native)
    if (!adopted) return { kind: 'set', rest: null, resyncEndAt: null }
    // The card's button already put this rest on the server when it could; only
    // schedule one if it could not (no network, logged out).
    if (native.serverTimerId) return { kind: 'set', rest: { ...adopted, timerId: native.serverTimerId }, resyncEndAt: null }
    return { kind: 'set', rest: adopted, resyncEndAt: native.endAt }
  }

  // Native moved the rest (-15/+15 on the card, or a newer rest started there).
  if (native.endAt && Math.abs(native.endAt - prev.endAt) > 1000) {
    if (native.serverTimerId && native.serverTimerId !== prev.timerId) {
      // A card button already moved the server's timer (and cancelled the old).
      return { kind: 'set', rest: { ...prev, endAt: native.endAt, timerId: native.serverTimerId }, resyncEndAt: null }
    }
    return { kind: 'set', rest: { ...prev, endAt: native.endAt, timerId: null }, resyncEndAt: native.endAt }
  }

  return { kind: 'none' }
}
