import { beforeEach, describe, expect, it } from 'vitest'
import { earliestStartedAt, restoreStartedAt, saveActiveWorkout, clearActiveWorkout } from './activeWorkout'

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
