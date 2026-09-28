import { Capacitor, registerPlugin } from '@capacitor/core'
import { setSlotFields } from './utils/setSlot'

// Lock-screen / Dynamic Island rest timer. Native only: the web build has no
// equivalent and the PWA keeps relying on the push notification alone.
//
// sync() is start-or-update, so callers can just declare the current timer state
// and let the native side work out whether that means starting a new activity or
// moving an existing one's end time.
const RestActivity = registerPlugin('RestActivity')

const available = Capacitor.isNativePlatform()

// The card-facing fields for one set descriptor: the shared slot strings
// (utils/setSlot.js — the watch gets the same ones) plus the card-only flag.
function setFields(detail) {
  return {
    ...setSlotFields(detail),
    // Whether ticking the set the card is pointed at should fire a rest at all —
    // false for a non-last superset member, which "Set done" runs straight past.
    // Defaults true so a lone exercise behaves as it always has.
    isLastInSuperset: detail.isLastInSuperset ?? true,
  }
}

// `following` is either the next exercise's set, or `{ workoutDone: true }` when
// the card's set is the session's last — which becomes the done label
// ("Finisher?" / "Finished"), so ticking it reads that instead of "Last set done".
function followingFields(following, workoutDoneLabel) {
  if (!following) return undefined
  if (following.workoutDone) return workoutDoneLabel ? { doneLabel: workoutDoneLabel } : undefined
  return { ...setFields(following), restSeconds: following.restSeconds ?? 0 }
}

// The activity belongs to the workout, not to a rest period: it goes up when a
// session starts and comes down when it is finished or discarded, so Skip zeroes
// the countdown instead of tearing the card down. `rest` is null between sets.
export function syncWorkoutActivity({ rest, sessionStartedAt, lastSet, templateName, templateSubtitle, workoutDoneLabel } = {}) {
  if (!available) return
  const detail = rest ?? lastSet ?? {}
  RestActivity.sync({
    // Fixed for the whole session — the card's header reads these instead of a
    // generic "Workout". Blank for an ad-hoc session, which falls back natively.
    templateName: templateName ?? '',
    templateSubtitle: templateSubtitle ?? '',
    endAt: rest ? rest.endAt : undefined,
    totalSeconds: rest ? rest.totalSeconds : 0,
    ...setFields(detail),
    // Lets the card's "Set done" button start the next rest itself, without
    // waking this webview to ask how long it should be. That is the rest for the
    // set the card points at, which after an exercise rollover differs from the
    // countdown running now (`restSeconds`) — hence cardRestSeconds.
    restSeconds: detail.cardRestSeconds ?? detail.restSeconds ?? rest?.totalSeconds ?? 0,
    // What the card should switch to when its last set of this exercise is
    // ticked (see followingDescriptor in activeWorkout.js). Absent when nothing
    // follows. Carries its own restSeconds: the rest for *its* set, not this one.
    following: followingFields(detail.following, workoutDoneLabel),
    // Non-empty once the session has nothing left: "Finisher?" / "Finished".
    doneLabel: detail.doneLabel ?? '',
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

// Tell native that `count` sets ticked from the card have been folded into the
// app's own state. Acknowledged by count rather than cleared outright, so a
// press that lands while the app is waking survives.
export function ackNativeCompletions(count) {
  if (!available || !count) return
  RestActivity.ackCompletions({ count }).catch(() => {})
}
