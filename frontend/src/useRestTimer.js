import { useEffect, useRef, useState } from 'react'
import { cancelRestTimer, clearRestProgress, postRestProgress, scheduleRestTimer } from './api/client'
import { applyNativeCompletions, nextSetDescriptor, refreshedRestSlot, upcomingDescriptors, watchSlotStale } from './activeWorkout'
import { cancelScheduledBeep, isNativeAudio, logDiary, playBeepNow, scheduleBeep, unlockAudio } from './audio'
import { isRestOver } from './restExpiry'
import { nextSetLabel, reconcileRest } from './restReconcile'
import { ackNativeCompletions, noteServerTimer, readNativeRestState, syncWorkoutActivity } from './restActivity'
import { clearRestTimer as clearRestTimerStore, loadRestTimer, saveRestTimer } from './restTimer'

// How long typing in a set row must pause before the Live Activity is updated.
const ACTIVITY_SYNC_DEBOUNCE_MS = 300

// The active workout's rest timer and everything that keeps it in step with the
// outside world: the shared store (global rest bar), the native beep, the Live
// Activity, the server timer the Garmin field polls, and native's own copy of
// the rest, which the card's buttons change while the webview is suspended.
//
// Effect order inside this hook matters and is deliberate: the schedule effect
// sets lastRestRef before the activity sync reads it.
export function useRestTimer({ sessionKey, now, exercises, setExercises, startedAt, templateName, templateSubtitle, finishers, workoutDoneLabel }) {
  // Restored from the shared store on mount so a rest timer started here keeps
  // counting (and stays visible via the global mini bar) if the athlete
  // navigates away to check the dashboard/program and comes back.
  const [restTimer, setRestTimer] = useState(() => {
    const saved = loadRestTimer()
    return saved && saved.templateId === sessionKey ? saved : null
  })
  // Last rest period's exercise/set, so the card still says what you just did
  // once the countdown has finished.
  const lastRestRef = useRef(null)
  // The current restTimer and exercises for the reconcile effect, which is
  // registered once and so cannot close over live values.
  const restTimerRef = useRef(null)
  restTimerRef.current = restTimer
  const exercisesRef = useRef(null)
  // The set the Garmin field was last told about. Null until a rest is
  // scheduled or progress posted: before that the watch is idle and should stay so.
  const watchSlotRef = useRef(null)

  useEffect(() => {
    exercisesRef.current = exercises
  }, [exercises])

  useEffect(() => {
    if (!restTimer) return
    // Derived from an absolute end timestamp, not decremented per tick — so
    // whenever `now` catches up (even after a long throttled gap), the
    // remaining time is always correct rather than having drifted.
    // Exact, not rounded: rounding cleared the rest up to 0.5s early, which
    // cancelled a native beep that hadn't started sounding yet (restExpiry.js).
    if (isRestOver(restTimer.endAt, now.getTime())) {
      // Web only: this effect advances just while the tab is visible, so a
      // backgrounded phone relies on the push. Natively the beep was armed on
      // the audio clock when the rest started and has already sounded — playing
      // again here would double it.
      if (!isNativeAudio) playBeepNow()
      setRestTimer(null)
    }
  }, [now, restTimer])

  // Mirrors restTimer into the shared store as its own effect (not inlined
  // into a setState updater) so the global mini bar can pick it up on other
  // pages — a stray call here previously fired setState on GlobalRestBar
  // synchronously mid-render of the page.
  // Deliberately does NOT depend on `exercises`. The beep is armed on the audio
  // hardware clock (the only clock that survives suspension), and re-arming
  // re-anchors on a baseline that moves whenever that hardware idles — so a
  // re-arm mid-rest can land the beep seconds early, or twice. `exercises`
  // changes on every keystroke in a weight field, which was doing exactly that
  // during real workouts. The activity sync below still tracks it; the beep
  // tracks only the timer that owns it.
  useEffect(() => {
    if (restTimer) {
      saveRestTimer({ ...restTimer, templateId: sessionKey })
      lastRestRef.current = restTimer
      logDiary(`schedule effect: arming endAt=${restTimer.endAt} (in ${Math.round((restTimer.endAt - Date.now()) / 1000)}s)`)
      scheduleBeep(restTimer.endAt, {
        title: 'Rest over',
        body: nextSetLabel(restTimer),
      })
    } else {
      // lastRestRef still holds whichever rest was last armed, so its endAt is
      // useful context even though it may already be stale by the time this runs.
      logDiary(`schedule effect: restTimer cleared, cancelling (was armed for endAt=${lastRestRef.current?.endAt ?? 'none'})`)
      clearRestTimerStore()
      // Say which rest this cancel is for. A webview resuming from suspension
      // still holds the previous, long-expired rest, and clears it on its first
      // tick; without the endAt that cancel also killed a rest the Live Activity
      // had just started natively. With none armed locally (first mount) pass 0,
      // so native ignores it if it holds a rest and reconcile adopts that one.
      cancelScheduledBeep({ endAt: lastRestRef.current?.endAt ?? 0, caller: 'schedule effect' })
    }
  }, [restTimer, sessionKey])

  // The activity belongs to the workout, so this updates it rather than
  // creating and destroying it: between sets it stays up with the countdown
  // zeroed, which is why Skip no longer makes the card vanish. lastRestRef
  // keeps the exercise/set line populated once the rest has ended — the effect
  // above sets it, and effects run in declaration order.
  //
  // `exercises` changes on every keystroke in a set row, and each sync is a
  // bridge call plus a Live Activity update. So a change to the rows alone is
  // sent once typing pauses; anything else - above all a rest starting,
  // moving or ending - goes straight through and cancels a pending one.
  const activitySyncTimerRef = useRef(null)
  // undefined, never a real restTimer value, so the first sync after mount is immediate.
  const lastSyncedRestRef = useRef(undefined)
  useEffect(() => {
    const send = () => {
      activitySyncTimerRef.current = null
      const upcoming = nextSetDescriptor(exercises)
      const allDone = !upcoming && exercises?.some((ex) => ex.sets.length > 0)
      syncWorkoutActivity({
        rest: restTimer,
        lastSet: upcoming ?? (allDone ? { ...lastRestRef.current, doneLabel: workoutDoneLabel() } : lastRestRef.current),
        upcoming: upcomingDescriptors(exercises),
        sessionStartedAt: startedAt.getTime(),
        templateName,
        templateSubtitle,
        workoutDoneLabel: workoutDoneLabel(),
      })
    }
    clearTimeout(activitySyncTimerRef.current)
    const restChanged = lastSyncedRestRef.current !== restTimer
    lastSyncedRestRef.current = restTimer
    if (restChanged) send()
    else activitySyncTimerRef.current = setTimeout(send, ACTIVITY_SYNC_DEBOUNCE_MS)
  // workoutDoneLabel is a per-render function that reads only finishers and exercises,
  // both listed here, so it is safe to leave out.
  // oxlint-disable-next-line react-hooks/exhaustive-deps
  }, [restTimer, startedAt, exercises, finishers, templateName, templateSubtitle])
  useEffect(() => () => clearTimeout(activitySyncTimerRef.current), [])

  // Editing the set you are resting for: its weight or reps, or a type change
  // that moves it (a row turned into a Warmup shifts which set is due and how
  // many warm-ups there are). The Live Activity shows the copy scheduled when
  // the rest began, and so does the server timer (what the Garmin field polls),
  // and the watch ignores a poll whose timerId it already has. So refresh the
  // set fields and reschedule the same end time under a new id once typing
  // pauses; the watch picks the new values up as a fresh countdown that lands
  // on the same second. The beep is unaffected: RestAudio.schedule treats an
  // unchanged endAt as already armed (the notification body keeps the old text).
  useEffect(() => {
    if (!restTimer?.timerId || restTimer.endAt - Date.now() < 3000) return
    const updated = refreshedRestSlot(restTimer, nextSetDescriptor(exercises))
    if (!updated) return
    const timeout = setTimeout(() => {
      watchSlotRef.current = updated
      setRestTimer({ ...updated, timerId: null })
      resyncServerTimer(updated, restTimer.endAt)
    }, 1000)
    return () => clearTimeout(timeout)
  // resyncServerTimer only reads its arguments and stable setters.
  // oxlint-disable-next-line react-hooks/exhaustive-deps
  }, [exercises, restTimer])

  // The same drift with no rest running: weight or reps edited after a rest has
  // ended, a tick un-done, a set added or deleted. The rest effect above only
  // exists while a timer does, and the watch holds its last slot until told
  // otherwise, so post the set the workout now points at as progress. A pending
  // rest outranks progress server-side and is covered above, so skip while one runs.
  useEffect(() => {
    if (restTimer) return
    const upcoming = nextSetDescriptor(exercises)
    if (!watchSlotStale(watchSlotRef.current, upcoming)) return
    const timeout = setTimeout(() => {
      watchSlotRef.current = upcoming
      postRestProgress(upcoming).catch(() => {})
    }, 1000)
    return () => clearTimeout(timeout)
  }, [exercises, restTimer])

  // The Live Activity's buttons mutate the timer natively while this webview is
  // suspended, so on every return to the foreground take native's word for it
  // (see reconcileRest).
  useEffect(() => {
    let cancelled = false
    async function reconcile() {
      const native = await readNativeRestState()
      if (cancelled || !native) return

      // Sets ticked from the card come back as a count. Apply them before the
      // timer, so the rest native started is described by the set the workout
      // has actually advanced to.
      let current = exercisesRef.current
      const pending = native.pendingCompletions || 0
      if (pending > 0) {
        const result = applyNativeCompletions(current, pending, native.pendingCompletionTimes || [])
        if (result) {
          current = result.exercises
          exercisesRef.current = current
          setExercises(current)
          ackNativeCompletions(result.applied)
        } else {
          // Nothing could be applied (no sets left, or no rep target to stand
          // in). Drop them rather than replaying the same press every resume.
          ackNativeCompletions(pending)
        }
      }

      // Each outcome also brings the server's timer (what the Garmin field
      // polls) into line: the card's buttons only ever touched native state, so
      // the server still holds the pre-press timer. Side effects live out here
      // rather than in a setRestTimer updater, which StrictMode runs twice.
      const prev = restTimerRef.current
      const nowMs = Date.now()
      if (native.active && native.endAt && native.endAt <= nowMs) {
        logDiary(`reconcile: native.active but endAt=${native.endAt} already past, treating inactive`)
      }
      const action = reconcileRest(prev, native, current, nowMs)
      if (action.kind === 'clear') {
        logDiary('reconcile: native inactive, clearing local restTimer')
        if (action.cancelTimerId) cancelRestTimer(action.cancelTimerId).catch(() => {})
        setRestTimer(null)
      } else if (action.kind === 'set') {
        logDiary(prev
          ? `reconcile: native endAt=${native.endAt} diverges from local endAt=${prev.endAt}, adopting native`
          : `reconcile: no local restTimer, adopting native endAt=${native.endAt}`)
        setRestTimer(action.rest)
        // Cancels prev's server timer when moving; an adopted rest has none.
        if (action.resyncEndAt != null) resyncServerTimer(prev ?? action.rest, action.resyncEndAt)
      }
    }
    reconcile()
    document.addEventListener('visibilitychange', reconcile)
    return () => {
      cancelled = true
      document.removeEventListener('visibilitychange', reconcile)
    }
  // Registered once; reads live values through refs.
  // oxlint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // Points the server's pending timer (what the Garmin field polls) at a new
  // end: cancel the old one, schedule a replacement for whatever is left. At
  // least 1s, the server's floor, so a rest adjusted to "now" still schedules.
  // Called from event handlers and reconcile, never from a state updater - an
  // updater runs twice under StrictMode and would schedule twice.
  function resyncServerTimer(rest, endAt) {
    if (rest.timerId) cancelRestTimer(rest.timerId).catch(() => {})
    const remaining = Math.max(1, Math.round((endAt - Date.now()) / 1000))
    scheduleRestTimer(remaining, rest, isNativeAudio)
      .then(({ timerId }) => {
        noteServerTimer(timerId)
        setRestTimer((cur) => (cur ? { ...cur, timerId } : cur))
      })
      .catch(() => {})
  }

  // Takes a full rest descriptor ({ exerciseName, targetReps, targetWeightKg,
  // enteredReps, nextSetNumber, totalSets, restSeconds }) rather than an
  // exercise + set number, so the caller can describe the next set of a
  // *different* exercise once one exercise is finished (see restDescriptorAfterSet).
  async function startRestTimer(descriptor) {
    if (restTimer?.timerId) {
      cancelRestTimer(restTimer.timerId).catch(() => {})
    }
    const duration = descriptor.restSeconds || 90
    const doneLabel = descriptor.workoutDone ? workoutDoneLabel() : ''
    // Web: unlock the beep element inside this tap so the countdown effect can
    // sound it later, and a backgrounded phone falls back to the server push.
    // Native: no unlock needed, and the beep and notification are both armed
    // from the schedule effect.
    unlockAudio()
    watchSlotRef.current = descriptor
    setRestTimer({
      endAt: Date.now() + duration * 1000,
      totalSeconds: duration,
      timerId: null,
      exerciseName: descriptor.exerciseName,
      exerciseShortName: descriptor.exerciseShortName ?? null,
      targetReps: descriptor.targetReps,
      // Weight already carried into the set you are about to do, so the Live
      // Activity can read "115 kg x 6" rather than just the rep target.
      targetWeightKg: descriptor.targetWeightKg,
      enteredReps: descriptor.enteredReps,
      holdLabel: descriptor.holdLabel ?? '',
      following: descriptor.following ?? null,
      nextSetNumber: descriptor.nextSetNumber,
      totalSets: descriptor.totalSets,
      warmupSets: descriptor.warmupSets ?? 0,
      restSeconds: duration,
      cardRestSeconds: descriptor.cardRestSeconds ?? duration,
      isLastInSuperset: descriptor.isLastInSuperset ?? true,
      isBodyweight: descriptor.isBodyweight ?? false,
      doneLabel,
    })
    // Native schedules its own local notification in scheduleBeep, which fires
    // on the device clock instead of arriving over APNs a few seconds late —
    // booking the server push too would double the alert. Still schedule
    // server-side either way (suppressing the push when native) so
    // PendingTimers has an entry for the Garmin watch data field to poll;
    // that's the one thing native itself has no server-side record of.
    try {
      const { timerId } = await scheduleRestTimer(duration, { ...descriptor, doneLabel }, isNativeAudio)
      noteServerTimer(timerId)
      setRestTimer((prev) => (prev ? { ...prev, timerId } : prev))
    } catch {
      // Local countdown still works even if the backend push couldn't be scheduled.
    }
  }

  // No rest after this set (a superset member that isn't the round's last), so
  // tell the Garmin field where the round has got to instead.
  function postProgress(descriptor) {
    watchSlotRef.current = descriptor
    postRestProgress(descriptor).catch(() => {})
  }

  function adjustRest(deltaSeconds) {
    if (!restTimer) return
    const newEndAt = Math.max(Date.now(), restTimer.endAt + deltaSeconds * 1000)
    resyncServerTimer(restTimer, newEndAt)
    setRestTimer({ ...restTimer, endAt: newEndAt, timerId: null })
  }

  function skipRest() {
    if (restTimer?.timerId) {
      cancelRestTimer(restTimer.timerId).catch(() => {})
    }
    setRestTimer(null)
  }

  // Saving or discarding the session. Explicit, not via the schedule effect's
  // teardown: neither ending path sets restTimer to null before navigating
  // away, so that effect's else branch never runs and the natively-armed beep
  // stayed live. It then went off minutes later, after the session was saved.
  function endRestForSession() {
    if (restTimer?.timerId) cancelRestTimer(restTimer.timerId).catch(() => {})
    clearRestProgress().catch(() => {})
    clearRestTimerStore()
    cancelScheduledBeep({ caller: 'saveSession/discardSession' })
  }

  return { restTimer, startRestTimer, adjustRest, skipRest, postProgress, endRestForSession }
}
