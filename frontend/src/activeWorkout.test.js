import { beforeEach, describe, expect, it } from 'vitest'
import {
  earliestStartedAt,
  restoreStartedAt,
  saveActiveWorkout,
  clearActiveWorkout,
  isTimeSet,
  nextSetDescriptor,
  restDescriptorAfterSet,
  supersetGroupBounds,
  suppressesRest,
  isSupersetRestOwner,
  advanceHold,
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

describe('superset grouping', () => {
  // E/F/G run as one superset: E and F carry the link, G ends it. A/D are lone.
  function gymOne() {
    return [
      { ...exercise('Trap Bar', 150, '4', [set(120, 4, true), set(120, 4, false)]), supersetWithNext: false },
      { ...exercise('Pull-Ups', 60, '3', [set(0, 3, false), set(0, 3, false)]), supersetWithNext: true },
      { ...exercise('Calf Raise', 60, '12', [set(20, 12, false), set(20, 12, false)]), supersetWithNext: true },
      { ...exercise('Copenhagen', 60, '20', [set(0, 20, false), set(0, 20, false)]), supersetWithNext: false },
      { ...exercise('Hip Thrust', 90, '8', [set(60, 8, false)]), supersetWithNext: false },
    ]
  }

  it('supersetGroupBounds spans the whole E/F/G run from any member', () => {
    const ex = gymOne()
    expect(supersetGroupBounds(ex, 1)).toEqual([1, 3])
    expect(supersetGroupBounds(ex, 2)).toEqual([1, 3])
    expect(supersetGroupBounds(ex, 3)).toEqual([1, 3])
  })

  it('supersetGroupBounds returns a lone exercise as its own span', () => {
    const ex = gymOne()
    expect(supersetGroupBounds(ex, 0)).toEqual([0, 0])
    expect(supersetGroupBounds(ex, 4)).toEqual([4, 4])
  })

  it('suppressesRest is true for every member but the last', () => {
    const ex = gymOne()
    expect(ex.map((_, i) => suppressesRest(ex, i))).toEqual([false, true, true, false, false])
  })

  it('rest after a non-last member still resolves a descriptor (caller suppresses the timer)', () => {
    // The page checks suppressesRest before arming a timer; restDescriptorAfterSet
    // itself stays total so the Live Activity sync can always describe "next".
    const ex = gymOne()
    ex[1].sets[0].completed = true // ticked Pull-Ups set 1
    const d = restDescriptorAfterSet(ex, 1, 0)
    // Rotates forward to the next member in the group (Calf Raise) rather
    // than rescanning from the group's top back onto Pull-Ups itself.
    expect(d.exerciseName).toBe('Calf Raise')
    expect(d.nextSetNumber).toBe(1)
    // Calf Raise isn't the group's last member (Copenhagen still has work),
    // so the lock-screen "Set done" button must not fire a rest off this
    // descriptor either.
    expect(d.isLastInSuperset).toBe(false)
  })

  it('a descriptor pointing at the group\'s last member is flagged for a rest', () => {
    const ex = gymOne()
    // Pull-Ups and Calf Raise fully done, leaving Copenhagen (the group's last
    // member — it's the one without supersetWithNext) as the next unticked set.
    ex[1].sets = ex[1].sets.map((s) => ({ ...s, completed: true }))
    ex[2].sets = ex[2].sets.map((s) => ({ ...s, completed: true }))
    const d = nextSetDescriptor(ex, 1)
    expect(d.exerciseName).toBe('Copenhagen')
    expect(d.isLastInSuperset).toBe(true)
  })

  it('a descriptor pointing at a non-last member is not flagged for a rest', () => {
    const ex = gymOne()
    const d = nextSetDescriptor(ex, 1) // lands on Pull-Ups, which isn't the group's last member
    expect(d.exerciseName).toBe('Pull-Ups')
    expect(d.isLastInSuperset).toBe(false)
  })

  it('rest off the last member points back at the top of the group for the next round', () => {
    const ex = gymOne()
    // Round 1 done for E/F/G; ticking G's set 1.
    ex[1].sets[0].completed = true
    ex[2].sets[0].completed = true
    ex[3].sets[0].completed = true
    const d = restDescriptorAfterSet(ex, 3, 0)
    expect(d.exerciseName).toBe('Pull-Ups') // group's first member, round 2
    expect(d.nextSetNumber).toBe(2)
    expect(d.restSeconds).toBe(60)
  })

  it('once the whole group is done the rest rolls on to the next exercise', () => {
    const ex = gymOne()
    for (const i of [1, 2, 3]) ex[i].sets = ex[i].sets.map((s) => ({ ...s, completed: true }))
    const d = restDescriptorAfterSet(ex, 3, 1)
    expect(d.exerciseName).toBe('Hip Thrust')
    expect(d.nextSetNumber).toBe(1)
  })

  // Uneven set counts: Pull-Ups gets 5 sets, Calf Raise and Copenhagen only 3.
  // Once the two 3-set members are exhausted, Pull-Ups' own remaining sets
  // are the only work left in the group. All sets start incomplete; each
  // test ticks whatever it needs.
  // Distinct restSeconds per member (45/60/90) so tests can actually tell
  // whose duration ends up used, rather than every member coincidentally
  // sharing one value.
  function unevenGym() {
    return [
      { ...exercise('Pull-Ups', 45, '3', [0, 1, 2, 3, 4].map(() => set(0, 3, false))), supersetWithNext: true },
      { ...exercise('Calf Raise', 60, '12', [0, 1, 2].map(() => set(20, 12, false))), supersetWithNext: true },
      { ...exercise('Copenhagen', 90, '20', [0, 1, 2].map(() => set(0, 20, false))), supersetWithNext: false },
    ]
  }

  it('a member earlier in the group still suppresses while a later one has work left', () => {
    const ex = unevenGym()
    // Pull-Ups' 1st set: Calf Raise/Copenhagen both still owe this round.
    expect(suppressesRest(ex, 0, 0)).toBe(true)
  })

  it('the last member standing rests between its own sets once everyone else is exhausted', () => {
    const ex = unevenGym()
    ex[1].sets = ex[1].sets.map((s) => ({ ...s, completed: true })) // Calf Raise done
    ex[2].sets = ex[2].sets.map((s) => ({ ...s, completed: true })) // Copenhagen done
    ex[0].sets[0].completed = true
    ex[0].sets[1].completed = true
    ex[0].sets[2].completed = true
    // Pull-Ups' 4th set (index 3): Calf Raise and Copenhagen have no set at
    // that index at all, so nobody is left to rotate to.
    expect(suppressesRest(ex, 0, 3)).toBe(false)
    // Pull-Ups still owns the group's rest here since it's the one with work
    // left (it just happens to also be the group's first member in this
    // fixture — see the reversed-order fixture below for the case that
    // actually distinguishes "current owner" from "always first member").
    const d = restDescriptorAfterSet(ex, 0, 3)
    expect(d.restSeconds).toBe(45)
  })

  it('the rest-timer/Live Activity descriptor rotates the same way the on-screen scroll does', () => {
    const ex = unevenGym()
    ex[0].sets[0].completed = true // ticked Pull-Ups set 1
    const d = restDescriptorAfterSet(ex, 0, 0)
    expect(d.exerciseName).toBe('Calf Raise')
  })

  it('the round-closing rest uses whichever member currently owns the group, not the closer\'s own', () => {
    const ex = unevenGym()
    // Round 1: Pull-Ups, Calf Raise done; Copenhagen (90s) closes the round.
    ex[0].sets[0].completed = true
    ex[1].sets[0].completed = true
    const d = restDescriptorAfterSet(ex, 2, 0)
    expect(d.exerciseName).toBe('Pull-Ups') // next round, back to the top
    // Not Copenhagen's own 90s — Pull-Ups (still the earliest member with
    // work left) owns the duration for the whole superset at this point.
    expect(d.restSeconds).toBe(45)
  })

  // The case that actually distinguishes "current owner" from a permanent
  // "always the first member": here the FIRST member is the short one, and
  // it's exhausted first while the second, longer member still has sets left
  // — exactly the scenario Lachlan hit gym-testing (3 sets/45s into 5
  // sets/150s). Ownership must shift to the second member once the first is
  // done, not keep using the first member's now-irrelevant duration.
  function firstMemberExhaustedFirstGym() {
    return [
      { ...exercise('Exercise 1', 45, '8', [0, 1, 2].map(() => set(20, 8, false))), supersetWithNext: true },
      { ...exercise('Exercise 2', 150, '5', [0, 1, 2, 3, 4].map(() => set(40, 5, false))), supersetWithNext: false },
    ]
  }

  it('rest ownership shifts to the next member once an earlier one is exhausted', () => {
    const ex = firstMemberExhaustedFirstGym()
    ex[0].sets = ex[0].sets.map((s) => ({ ...s, completed: true })) // Exercise 1 fully done
    ex[1].sets[0].completed = true
    ex[1].sets[1].completed = true
    ex[1].sets[2].completed = true
    expect(isSupersetRestOwner(ex, 0)).toBe(false)
    expect(isSupersetRestOwner(ex, 1)).toBe(true)
    // Exercise 2's own remaining sets (4th here) rest on its own 150s, not
    // Exercise 1's leftover 45s.
    expect(suppressesRest(ex, 1, 3)).toBe(false)
    const d = restDescriptorAfterSet(ex, 1, 2)
    expect(d.restSeconds).toBe(150)
  })

  it('isSupersetRestOwner is true only for the group\'s first member while it still has work (and any lone exercise)', () => {
    const ex = unevenGym() // Pull-Ups, Calf Raise, Copenhagen
    expect(ex.map((_, i) => isSupersetRestOwner(ex, i))).toEqual([true, false, false])
    const lone = gymOne()
    expect(isSupersetRestOwner(lone, 0)).toBe(true) // Trap Bar, not in a group
    expect(isSupersetRestOwner(lone, 4)).toBe(true) // Hip Thrust, not in a group
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

describe('advanceHold', () => {
  const T0 = 1_000_000
  const perSide = { isPerSide: true }
  const single = { isPerSide: false }
  // A side-1 hold, 30s target, 8s configured gap, due now.
  const side1 = { exIdx: 0, setIdx: 0, phase: 'hold', side: 1, targetSeconds: 30, delaySeconds: 8, endsAt: T0 }

  it('returns null while time remains on the current phase', () => {
    expect(advanceHold({ ...side1, endsAt: T0 + 5000 }, perSide, T0)).toBeNull()
  })

  it('side 1 ending on a per-side exercise beeps and opens the gap', () => {
    const step = advanceHold(side1, perSide, T0)
    expect(step).toMatchObject({ type: 'advance', beep: true })
    expect(step.holdTimer).toMatchObject({ phase: 'gap', side: 1, endsAt: T0 + 8000 })
  })

  it('the gap ending beeps again and starts side 2 for the full target', () => {
    const gap = { ...side1, phase: 'gap', endsAt: T0 }
    const step = advanceHold(gap, perSide, T0)
    expect(step).toMatchObject({ type: 'advance', beep: true })
    expect(step.holdTimer).toMatchObject({ phase: 'hold', side: 2, endsAt: T0 + 30_000 })
  })

  it('side 2 ending finishes the set with the target seconds', () => {
    const step = advanceHold({ ...side1, side: 2, endsAt: T0 }, perSide, T0)
    expect(step).toEqual({ type: 'finish', beep: true, seconds: 30 })
  })

  it('a zero delay skips the gap: side 1 goes straight to side 2 with one beep', () => {
    const step = advanceHold({ ...side1, delaySeconds: 0 }, perSide, T0)
    expect(step.holdTimer).toMatchObject({ phase: 'hold', side: 2, endsAt: T0 + 30_000 })
  })

  it('a non-per-side hold just finishes when it hits zero', () => {
    const step = advanceHold({ ...side1, delaySeconds: 0 }, single, T0)
    expect(step).toEqual({ type: 'finish', beep: true, seconds: 30 })
  })
})
