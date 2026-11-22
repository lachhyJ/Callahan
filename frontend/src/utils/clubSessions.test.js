import { describe, it, expect } from 'vitest'
import { zoneShares, versusUsual, CLUB_COLUMNS } from './clubSessions'

describe('zoneShares', () => {
  it('is null without zones or with an all-zero set', () => {
    expect(zoneShares(null)).toBeNull()
    expect(zoneShares([0, 0, 0, 0, 0])).toBeNull()
  })
  it('shares sum to 100', () => {
    const shares = zoneShares([50, 25, 25, 0, 0])
    expect(shares.map((s) => s.pct)).toEqual([50, 25, 25, 0, 0])
  })
})

describe('versusUsual', () => {
  const sessions = [
    { id: 1, sprintCount: 10 }, { id: 2, sprintCount: 20 }, { id: 3, sprintCount: 30 }, { id: 4, sprintCount: null },
  ]
  const pick = (s) => s.sprintCount
  it('compares against the mean of the other sessions that have the figure', () => {
    expect(versusUsual(sessions, sessions[2], pick)).toBe(100 * (30 - 15) / 15)
  })
  it('is null with too few others, or when the session lacks the figure', () => {
    expect(versusUsual(sessions.slice(0, 2), sessions[0], pick)).toBeNull()
    expect(versusUsual(sessions, sessions[3], pick)).toBeNull()
  })
})

describe('CLUB_COLUMNS', () => {
  it('renders missing figures as null so the row can show a dash', () => {
    const s = { distanceKm: null, avgHeartRate: null, trainingLoad: null, sprintCount: null }
    expect(CLUB_COLUMNS.map((c) => c.value(s))).toEqual([null, null, null, null])
  })
})
