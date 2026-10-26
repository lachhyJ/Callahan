import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { clearRestProgress, postRestProgress } from './client'

// The progress payload is what the Garmin field shows between rests, so it is
// spelled with the same slot strings as a scheduled rest and, like it, prefers
// the short exercise name.
describe('postRestProgress', () => {
  let call

  beforeEach(() => {
    call = null
    vi.stubGlobal('localStorage', { getItem: () => null, removeItem: () => {} })
    vi.stubGlobal('fetch', async (url, init) => {
      call = { url: String(url), method: init.method, body: init.body ? JSON.parse(init.body) : null }
      return { ok: true, status: 200, json: async () => ({}), text: async () => '' }
    })
  })

  afterEach(() => vi.unstubAllGlobals())

  it('sends the next set\'s load and position under the short name', async () => {
    await postRestProgress({
      exerciseName: 'Single Leg Standing Calf Raise',
      exerciseShortName: '1-Leg Calf Raise',
      targetReps: '12',
      targetWeightKg: '40',
      enteredReps: '12',
      nextSetNumber: 1,
      totalSets: 3,
    })
    expect(call.url).toContain('/api/resttimer/progress')
    expect(call.method).toBe('POST')
    expect(call.body).toMatchObject({
      exerciseName: '1-Leg Calf Raise',
      targetWeight: '40 kg',
      enteredReps: '12',
      nextSetNumber: 1,
      totalSets: 3,
      doneLabel: '',
    })
  })

  it('clears at the dedicated endpoint', async () => {
    await clearRestProgress()
    expect(call.url).toContain('/api/resttimer/progress/clear')
    expect(call.method).toBe('POST')
  })
})
