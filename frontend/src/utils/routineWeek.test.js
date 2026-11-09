import { describe, expect, it } from 'vitest'
import { routineDaysThisWeek } from './routineWeek'

const routine = (...dates) => ({ recent: dates.map((date) => ({ date })) })

describe('routineDaysThisWeek', () => {
  // 2026-11-11 is a Wednesday; its week starts Monday 2026-11-09.
  it('counts ticks from Monday up to today', () => {
    expect(routineDaysThisWeek(routine('2026-11-09', '2026-11-11'), '2026-11-11')).toBe(2)
  })

  it('ignores last week, including the Sunday just before Monday', () => {
    expect(routineDaysThisWeek(routine('2026-11-08', '2026-11-02'), '2026-11-11')).toBe(0)
  })

  it('on a Sunday still counts the whole Monday-first week', () => {
    expect(routineDaysThisWeek(routine('2026-11-09', '2026-11-15'), '2026-11-15')).toBe(2)
  })

  it('is zero for a missing routine or no history', () => {
    expect(routineDaysThisWeek(undefined, '2026-11-11')).toBe(0)
    expect(routineDaysThisWeek({ recent: [] }, '2026-11-11')).toBe(0)
  })
})
