import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { scheduleRestTimer } from './client'

// The schedule payload is what the Garmin field polls, so it is the one place
// the short name replaces the full name. The in-app bar and Live Activity read
// the full name off the rest-timer state, not this payload.
describe('scheduleRestTimer', () => {
  let sent

  beforeEach(() => {
    sent = null
    vi.stubGlobal('localStorage', { getItem: () => null, removeItem: () => {} })
    vi.stubGlobal('fetch', async (_url, init) => {
      sent = JSON.parse(init.body)
      return { ok: true, status: 200, json: async () => ({ timerId: 't' }), text: async () => '{"timerId":"t"}' }
    })
  })

  afterEach(() => vi.unstubAllGlobals())

  it('sends the short name when the exercise has one', async () => {
    await scheduleRestTimer(90, { exerciseName: 'Single Leg Standing Calf Raise', exerciseShortName: '1-Leg Calf Raise', nextSetNumber: 2, totalSets: 4 })
    expect(sent.exerciseName).toBe('1-Leg Calf Raise')
  })

  it('falls back to the full name without one', async () => {
    await scheduleRestTimer(90, { exerciseName: 'Hip Thrust', exerciseShortName: null, nextSetNumber: 2, totalSets: 4 })
    expect(sent.exerciseName).toBe('Hip Thrust')
  })
})
