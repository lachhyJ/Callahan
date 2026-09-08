import { beforeEach, describe, expect, it } from 'vitest'
import {
  earliestStartedAt,
  restoreStartedAt,
  saveActiveWorkout,
  clearActiveWorkout,
  isTimeSet,
  nextSetDescriptor,
  restDescriptorAfterSet,
} from './activeWorkout'

// The suite runs on plain node by deliberate choice (see vite.config.js), and
// the only browser global this module touches is localStorage — so stub that
// rather than pulling in jsdom for one key/value store. `window` is needed too:
// persistedSlot dispatches a change event on every save.
const store = new Map()
globalThis.localStorage = {
  getItem: (k) => (store.has(k) ? store.get(k) : null),
  setItem: (k, v) => store.set(k, String(v)),
  removeItem: (k) => store.delete(k),
  clear: () => store.clear(),
}
globalThis.window = { dispatchEvent: () => {}, addEventListener: () => {}, removeEventListener: () => {} }
globalThis.Event = class { constructor(type) { this.type = type } }

const KEY = 'callahan_active_workout'
const LATER = new Date('2026-09-05T01:07:40Z')
const EARLIER = new Date('2026-09-04T23:43:00Z')

function bank(state) {
  localStorage.setItem(KEY, JSON.stringify(state))
}

beforeEach(() => localStorage.clear())

describe('restoreStartedAt', () => {
  it('returns the banked start for a matching session', () => {
    bank({ templateId: 3, startedAt: EARLIER.toISOString() })
    expect(restoreStartedAt(3, LATER)).toEqual(EARLIER)
  })

  it('falls back when nothing is banked', () => {
    expect(restoreStartedAt(3, LATER)).toEqual(LATER)
  })

  it('falls back when the banked session is a different template', () => {
    bank({ templateId: 9, startedAt: EARLIER.toISOString() })
    expect(restoreStartedAt(3, LATER)).toEqual(LATER)
  })

  it('handles the custom (template-less) session key', () => {
    bank({ templateId: 'custom', startedAt: EARLIER.toISOString() })
    expect(restoreStartedAt('custom', LATER)).toEqual(EARLIER)
    expect(restoreStartedAt(3, LATER)).toEqual(LATER)
  })

  // Both of these used to produce an Invalid Date that threw out of the
  // persist effect on .toISOString().
  it('falls back on a slot written before startedAt existed', () => {
    bank({ templateId: 3 })
    expect(restoreStartedAt(3, LATER)).toEqual(LATER)
  })

  // Not covered by the Invalid Date guard: `new Date(null)` is the epoch, which
  // is a perfectly valid date. Without the explicit emptiness check that would
  // pin the session's start to 1970 and, via earliestStartedAt, keep it there.
  it('falls back on a null start time rather than returning the epoch', () => {
    bank({ templateId: 3, startedAt: null })
    expect(restoreStartedAt(3, LATER)).toEqual(LATER)
    expect(earliestStartedAt(3, LATER)).toEqual(LATER)
  })

  it('falls back on a corrupted start time', () => {
    bank({ templateId: 3, startedAt: 'not-a-date' })
    expect(restoreStartedAt(3, LATER)).toEqual(LATER)
  })

  it('survives an unparseable slot', () => {
    localStorage.setItem(KEY, '{oh no')
    expect(restoreStartedAt(3, LATER)).toEqual(LATER)
  })
})

describe('earliestStartedAt', () => {
  // The case the guard exists for: a remount mid-session offering `now`.
  it('keeps the banked start when the candidate is later', () => {
    bank({ templateId: 3, startedAt: EARLIER.toISOString() })
    expect(earliestStartedAt(3, LATER)).toEqual(EARLIER)
  })

  it('accepts a candidate that is earlier than what is banked', () => {
    bank({ templateId: 3, startedAt: LATER.toISOString() })
    expect(earliestStartedAt(3, EARLIER)).toEqual(EARLIER)
  })

  it('uses the candidate when nothing is banked', () => {
    expect(earliestStartedAt(3, LATER)).toEqual(LATER)
  })

  it('does not borrow a start time from a different session', () => {
    bank({ templateId: 9, startedAt: EARLIER.toISOString() })
    expect(earliestStartedAt(3, LATER)).toEqual(LATER)
  })

  it('is stable across repeated persists', () => {
    bank({ templateId: 3, startedAt: EARLIER.toISOString() })
    let start = earliestStartedAt(3, LATER)
    for (let i = 0; i < 5; i++) {
      saveActiveWorkout({ templateId: 3, startedAt: start.toISOString(), exercises: [] })
      start = earliestStartedAt(3, new Date(LATER.getTime() + i * 60000))
    }
    expect(start).toEqual(EARLIER)
  })
})

describe('clearActiveWorkout', () => {
  it('lets the next session start fresh', () => {
    bank({ templateId: 3, startedAt: EARLIER.toISOString() })
    clearActiveWorkout()
    expect(earliestStartedAt(3, LATER)).toEqual(LATER)
  })
})

// A set row as the page holds it; only the fields the descriptors read.
function set(weightKg, reps, completed = false) {
  return { weightKg: String(weightKg), reps: String(reps), completed }
}

function exercise(name, restSeconds, targetReps, sets) {
  return { exerciseName: name, restSeconds, targetReps, sets }
}

describe('restDescriptorAfterSet', () => {
  // Two exercises, three working sets each. Bench is done bar its last set.
  function session() {
    return [
      exercise('Bench Press', 120, '6', [
        set(80, 6, true),
        set(80, 6, true),
        set(80, 6, false),
      ]),
      exercise('Barbell Row', 90, '8', [
        set(60, 8, false),
        set(60, 8, false),
        set(60, 8, false),
      ]),
    ]
  }

  it('points at the next set of the same exercise when one is left', () => {
    const ex = session()
    ex[0].sets[1].completed = false // ticking set 2, set 3 still to go
    const d = restDescriptorAfterSet(ex, 0, 1)
    expect(d.exerciseName).toBe('Bench Press')
    expect(d.nextSetNumber).toBe(3)
    expect(d.totalSets).toBe(3)
    expect(d.restSeconds).toBe(120)
  })

  it('rolls over to the first set of the next exercise after the last set', () => {
    const ex = session()
    ex[0].sets[2].completed = true // ticking Bench's last set
    const d = restDescriptorAfterSet(ex, 0, 2)
    expect(d.exerciseName).toBe('Barbell Row')
    expect(d.nextSetNumber).toBe(1)
    expect(d.totalSets).toBe(3)
    expect(d.targetReps).toBe('8')
    expect(d.targetWeightKg).toBe('60')
    // rest length comes from the exercise about to be worked, not the one just finished
    expect(d.restSeconds).toBe(90)
  })

  it('returns an over-the-end descriptor after the last set of the last exercise', () => {
    const ex = session()
    ex[0].sets = ex[0].sets.map((s) => ({ ...s, completed: true }))
    ex[1].sets = ex[1].sets.map((s) => ({ ...s, completed: true }))
    const d = restDescriptorAfterSet(ex, 1, 2)
    expect(d.exerciseName).toBe('Barbell Row')
    expect(d.nextSetNumber).toBe(4)
    expect(d.totalSets).toBe(3)
    expect(d.nextSetNumber).toBeGreaterThan(d.totalSets) // native renders "Last set done"
  })

  it('skips a fully-completed exercise to reach the next one with work left', () => {
    const ex = [
      exercise('A', 100, '5', [set(50, 5, true)]),
      exercise('B', 110, '5', [set(55, 5, true)]),
      exercise('C', 120, '5', [set(60, 5, false), set(60, 5, false)]),
    ]
    const d = restDescriptorAfterSet(ex, 0, 0)
    expect(d.exerciseName).toBe('C')
    expect(d.nextSetNumber).toBe(1)
    expect(d.restSeconds).toBe(120)
  })
})

describe('isTimeSet', () => {
  it('is true only when durationSeconds carries a value', () => {
    expect(isTimeSet({ durationSeconds: 20 })).toBe(true)
    expect(isTimeSet({ durationSeconds: '20' })).toBe(true)
    expect(isTimeSet({ durationSeconds: 0 })).toBe(true) // an explicit zero is still a time set
    expect(isTimeSet({ durationSeconds: '' })).toBe(false)
    expect(isTimeSet({ durationSeconds: null })).toBe(false)
    expect(isTimeSet({ reps: '8' })).toBe(false)
  })
})

describe('nextSetDescriptor', () => {
  it('returns the first exercise with an unticked set', () => {
    const ex = [
      exercise('A', 100, '5', [set(50, 5, true), set(50, 5, false)]),
      exercise('B', 110, '5', [set(55, 5, false)]),
    ]
    const d = nextSetDescriptor(ex)
    expect(d.exerciseName).toBe('A')
    expect(d.nextSetNumber).toBe(2)
  })

  it('returns null when every set is done', () => {
    const ex = [exercise('A', 100, '5', [set(50, 5, true)])]
    expect(nextSetDescriptor(ex)).toBeNull()
  })
})
