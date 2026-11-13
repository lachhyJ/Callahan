import { describe, it, expect } from 'vitest'
import { unclassifiedTournamentActivities } from './tournamentStats'

describe('unclassifiedTournamentActivities', () => {
  const all = [
    { id: 1, date: '2026-11-13', tournamentId: 4 },
    { id: 2, date: '2026-11-13', tournamentId: 4 },
    { id: 3, date: '2026-11-11', tournamentId: null },
    { id: 4, date: '2026-11-14', tournamentId: 4 },
  ]

  it('returns attached activities not in the Game list, oldest first', () => {
    const result = unclassifiedTournamentActivities(all, [{ id: 4 }])
    expect(result.map((a) => a.id)).toEqual([1, 2])
  })

  it('ignores activities with no tournament', () => {
    expect(unclassifiedTournamentActivities(all, []).some((a) => a.id === 3)).toBe(false)
  })

  it('is empty once everything is classified', () => {
    expect(unclassifiedTournamentActivities(all, [{ id: 1 }, { id: 2 }, { id: 4 }])).toEqual([])
  })
})
