import { describe, expect, it } from 'vitest'
import { compareByPlayOrder } from './tournamentStats'

describe('compareByPlayOrder', () => {
  it('orders same-day games by start time, not by id', () => {
    // 13 Nov: Zephyr synced (id 61) before Hills (id 62) but was played later.
    const games = [
      { id: 63, date: '2026-11-13', startTimeLocal: '2026-11-13T15:40:59' },
      { id: 61, date: '2026-11-13', startTimeLocal: '2026-11-13T10:44:33' },
      { id: 64, date: '2026-11-14', startTimeLocal: '2026-11-14T12:27:11' },
      { id: 62, date: '2026-11-13', startTimeLocal: '2026-11-13T09:00:34' },
    ]
    expect([...games].sort(compareByPlayOrder).map((g) => g.id)).toEqual([62, 61, 63, 64])
  })

  it('puts an untimed manual entry first within its day, falling back to id', () => {
    const games = [
      { id: 5, date: '2026-11-13', startTimeLocal: '2026-11-13T09:00:00' },
      { id: 7, date: '2026-11-13', startTimeLocal: null },
      { id: 6, date: '2026-11-13' },
    ]
    expect([...games].sort(compareByPlayOrder).map((g) => g.id)).toEqual([6, 7, 5])
  })
})
