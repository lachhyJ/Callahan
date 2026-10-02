import { nearestDumbbells, roundToStep } from '../plateCalc'

// Warm-up ramp as fractions of the first working set's weight, by how many
// warm-up rows the slot has. Matches the program doc's shape: only a heavy lift
// gets a real ramp, reps fall as the weight climbs. One global curve for now;
// per-lift overrides from the doc's own tables are a later step.
const RAMPS = {
  1: [{ pct: 0.6, reps: 5 }],
  2: [{ pct: 0.5, reps: 5 }, { pct: 0.75, reps: 3 }],
  3: [{ pct: 0.5, reps: 5 }, { pct: 0.75, reps: 3 }, { pct: 0.9, reps: 1 }],
}

function rampFor(count) {
  if (RAMPS[count]) return RAMPS[count]
  // Past three rows, spread evenly from 50% to 90%; reps bottom out at 1.
  return Array.from({ length: count }, (_, i) => ({
    pct: 0.5 + (0.4 * i) / (count - 1),
    reps: Math.max(1, 5 - 2 * i),
  }))
}

const BAR_KG = 20
const STEP_KG = 2.5

function snap(raw, equipmentType, availableDumbbells) {
  if (equipmentType === 'dumbbell') {
    const perDb = raw / 2
    const m = nearestDumbbells(perDb, availableDumbbells)
    const chosen = m.exact
      ?? (m.below === undefined ? m.above
        : m.above === undefined ? m.below
        : Math.abs(m.below - perDb) <= Math.abs(m.above - perDb) ? m.below : m.above)
    return chosen === undefined ? roundToStep(raw, STEP_KG) : chosen * 2
  }
  const rounded = roundToStep(raw, STEP_KG)
  // An empty bar is the lightest barbell warm-up there is.
  return equipmentType === 'barbell' ? Math.max(BAR_KG, rounded) : rounded
}

// Suggested { weightKg, reps } per warm-up row, or [] when there is nothing to
// scale from (no working weight, no warm-up rows). Never suggests a warm-up at
// or above the working weight itself.
export function warmupRamp(workingKg, warmupCount, equipmentType = 'barbell', availableDumbbells = []) {
  const working = Number(workingKg)
  if (!warmupCount || !(working > 0)) return []
  return rampFor(warmupCount).map(({ pct, reps }) => {
    const weight = Math.min(snap(working * pct, equipmentType, availableDumbbells), working)
    return { weightKg: weight, reps }
  })
}
