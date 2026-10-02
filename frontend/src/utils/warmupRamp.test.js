import { describe, expect, it } from 'vitest'
import { warmupRamp } from './warmupRamp'

describe('warmupRamp', () => {
  it('returns nothing without a working weight or warm-up rows', () => {
    expect(warmupRamp('', 3)).toEqual([])
    expect(warmupRamp(0, 3)).toEqual([])
    expect(warmupRamp(100, 0)).toEqual([])
  })

  it('ramps three rows 50/75/90% with falling reps, snapped to 2.5kg', () => {
    expect(warmupRamp(130, 3)).toEqual([
      { weightKg: 65, reps: 5 },
      { weightKg: 97.5, reps: 3 },
      { weightKg: 117.5, reps: 1 },
    ])
  })

  it('uses a single 60% set for one warm-up row', () => {
    expect(warmupRamp(100, 1)).toEqual([{ weightKg: 60, reps: 5 }])
  })

  it('never goes below the empty bar on a barbell', () => {
    expect(warmupRamp(30, 1)[0].weightKg).toBe(20)
  })

  it('never reaches the working weight', () => {
    expect(warmupRamp(20, 3).every((r) => r.weightKg <= 20)).toBe(true)
  })

  it('snaps dumbbells to the rack (total of a pair)', () => {
    // 60% of 40 = 24 -> 12/db -> nearest of [10, 12.5] is 12.5 -> 25
    expect(warmupRamp(40, 1, 'dumbbell', [10, 12.5, 15, 20])).toEqual([{ weightKg: 25, reps: 5 }])
  })

  it('spreads past three rows', () => {
    const r = warmupRamp(100, 4)
    expect(r).toHaveLength(4)
    expect(r[0].weightKg).toBeLessThan(r[3].weightKg)
  })
})
