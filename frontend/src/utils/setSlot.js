import { formatLoadWeight } from './format'

// The strings one set shows on every surface outside the app: the Live
// Activity card and the Garmin watch field. Built in one place because they
// must read identically — weight goes over pre-formatted, and a timed hold has
// no reps or weight, so its duration ("30s" / "30s/side") rides in the weight
// slot with reps left blank.
export function setSlotFields(detail) {
  return {
    exerciseName: detail.exerciseName ?? 'Workout',
    targetReps: detail.holdLabel || detail.targetReps == null ? '' : String(detail.targetReps),
    targetWeight: detail.holdLabel || (detail.isBodyweight ? '' : formatLoadWeight(detail.targetWeightKg)),
    // What is actually typed into the next set's reps box, as opposed to the
    // programmed target, which is often a range.
    enteredReps: detail.holdLabel || detail.enteredReps == null ? '' : String(detail.enteredReps),
    nextSetNumber: detail.nextSetNumber ?? 1,
    totalSets: detail.totalSets ?? 1,
  }
}
