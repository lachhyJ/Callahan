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
    warmupSets: detail.warmupSets ?? 0,
  }
}

// Where a set sits in its exercise, warmups counted apart from working sets:
// "W1/2" for a warmup, "2/4" for the second working set (the watch's wording;
// `verbose` gives the sentence form the notification uses). nextSetNumber and
// totalSets count every row, warmups first, so a working set's position is its
// number minus the warmups. Null past the end of the exercise ("Last set done").
export function setPosition({ nextSetNumber, totalSets, warmupSets = 0 }, verbose = false) {
  if (!nextSetNumber || !totalSets || nextSetNumber > totalSets) return null
  if (nextSetNumber <= warmupSets) {
    return verbose ? `warmup ${nextSetNumber} of ${warmupSets}` : `W${nextSetNumber}/${warmupSets}`
  }
  const n = nextSetNumber - warmupSets
  const total = totalSets - warmupSets
  return verbose ? `set ${n} of ${total}` : `${n}/${total}`
}
