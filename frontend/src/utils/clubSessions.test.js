import { describe, it, expect } from 'vitest'
import { zoneShares, versusUsual, CLUB_COLUMNS, isListedClubSession } from './clubSessions'

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

describe('isListedClubSession', () => {
  const club = { type: 'Ultimate', date: '2026-11-18', activitySessionTypeName: 'Club Training', sessionTypes: [] }

  it('lists a club session on or after the count start', () => {
    expect(isListedClubSession(club)).toBe(true)
  })

  it('leaves out a club session from before the count start', () => {
    expect(isListedClubSession({ ...club, date: '2026-11-17' })).toBe(false)
  })

  it('counts Club Training as an extra tag', () => {
    expect(isListedClubSession({ ...club, activitySessionTypeName: 'Pod', sessionTypes: [{ name: 'Club Training' }] })).toBe(true)
  })

  it('leaves out other Ultimate sessions and non-Ultimate activities', () => {
    expect(isListedClubSession({ ...club, activitySessionTypeName: 'Pod' })).toBe(false)
    expect(isListedClubSession({ ...club, type: 'Run' })).toBe(false)
  })
})
