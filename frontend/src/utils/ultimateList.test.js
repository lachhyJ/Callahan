import { describe, expect, it } from 'vitest'
import { partitionBySeason, pickCurrentSeason, seasonContents, sortEntries } from './ultimateList'

const TODAY = '2026-11-17'
const RECENT_SINCE = '2026-11-10'

const season = (id, startDate, endDate) => ({ id, name: `S${id}`, startDate, endDate })
const tournament = (id, startDate, endDate, seasonId = null) => ({
  kind: 'tournament',
  tournament: { id, name: `T${id}`, startDate, endDate, seasonId },
  games: [],
  pending: [],
  sortDate: startDate,
})
const game = (id, date) => ({ kind: 'game', game: { id, date: `${date}T10:00:00` }, sortDate: `${date}T10:00:00` })

const names = (entries) => entries.map((e) => (e.kind === 'tournament' ? e.tournament.name : `g${e.game.id}`))

describe('sortEntries', () => {
  it('puts a tournament that finished last weekend above upcoming ones', () => {
    const sorted = sortEntries([
      tournament(1, '2026-12-05', '2026-12-06'),
      tournament(2, '2026-11-14', '2026-11-15'),
      tournament(3, '2027-01-16', '2027-01-17'),
      tournament(4, '2026-10-16', '2026-10-17'),
    ], TODAY, RECENT_SINCE)
    expect(names(sorted)).toEqual(['T2', 'T1', 'T3', 'T4'])
  })
})

describe('pickCurrentSeason', () => {
  const s24 = season(1, '2024-09-01', '2025-04-12')
  const s25 = season(2, '2025-09-01', '2026-04-12')
  const s26 = season(3, '2026-09-01', '2027-04-12')

  it('picks the season containing today', () => {
    expect(pickCurrentSeason([s24, s25, s26], TODAY)).toBe(s26)
  })

  it('falls back to the most recently started season in the off-season', () => {
    expect(pickCurrentSeason([s24, s25, s26], '2026-06-01')).toBe(s25)
  })

  it('never picks a season that has not started', () => {
    expect(pickCurrentSeason([season(9, '2027-09-01', '2028-04-01')], TODAY)).toBeNull()
  })
})

describe('partitionBySeason', () => {
  const s25 = season(2, '2025-09-01', '2026-04-12')
  const s26 = season(3, '2026-09-01', '2027-04-12')

  it('folds older seasons and keeps the current one in the main list', () => {
    const sorted = sortEntries([
      tournament(1, '2026-10-16', '2026-10-17', 3),
      tournament(2, '2026-02-14', '2026-02-15', 2),
      game(10, '2026-03-01'), // loose game inside last season's range
      game(11, '2026-06-01'), // off-season, before the current season started
      game(12, '2026-09-20'), // loose game this season
    ], TODAY, RECENT_SINCE)
    const { current, main, pastGroups } = partitionBySeason(sorted, [s26, s25], TODAY, RECENT_SINCE)
    expect(current).toBe(s26)
    expect(names(main)).toEqual(['T1', 'g12'])
    expect(pastGroups.map((g) => g.season?.id ?? 'none')).toEqual([2, 'none'])
    expect(names(pastGroups[0].entries)).toEqual(['g10', 'T2'])
    expect(names(pastGroups[1].entries)).toEqual(['g11'])
  })

  it('keeps a recent or upcoming tournament in the main list whatever its season', () => {
    const recentInOldSeason = tournament(1, '2026-11-14', '2026-11-15', 2)
    const { main, pastGroups } = partitionBySeason([recentInOldSeason], [s26, s25], TODAY, RECENT_SINCE)
    expect(names(main)).toEqual(['T1'])
    expect(pastGroups.map((g) => g.season.id)).toEqual([2])
    expect(pastGroups[0].entries).toEqual([])
  })

  it('leaves everything in the main list when there are no seasons', () => {
    const sorted = [tournament(1, '2024-01-01', '2024-01-02')]
    const { current, main, pastGroups } = partitionBySeason(sorted, [], TODAY, RECENT_SINCE)
    expect(current).toBeNull()
    expect(main).toEqual(sorted)
    expect(pastGroups).toEqual([])
  })
})

describe('seasonContents', () => {
  const seasons = [season(1, '2025-10-01', '2026-04-15'), season(2, '2026-10-01', '2027-04-15')]
  const t = (id, startDate, endDate, seasonId = null) => ({ id, name: `T${id}`, startDate, endDate, seasonId })
  const g = (id, date, tournamentId = null) => ({ id, date: `${date}T10:00:00`, tournamentId })

  it('collects tournaments by explicit link or date, oldest first, with their games', () => {
    const tournaments = [
      t(1, '2026-03-01', '2026-03-02'),
      t(2, '2025-11-01', '2025-11-02'),
      t(3, '2026-11-01', '2026-11-02'),
      t(4, '2026-11-08', '2026-11-09', 1), // explicitly linked to S1 despite its dates
    ]
    const games = [g(10, '2026-03-01', 1), g(11, '2026-03-02', 1), g(12, '2025-11-01', 2), g(13, '2026-11-01', 3)]
    const { tournaments: ts } = seasonContents(seasons[0], seasons, tournaments, games)
    expect(ts.map((x) => x.tournament.name)).toEqual(['T2', 'T1', 'T4'])
    expect(ts.map((x) => x.games.length)).toEqual([1, 2, 0])
  })

  it('keeps standalone games that fall inside the season range', () => {
    const games = [g(20, '2025-12-10'), g(21, '2026-06-01'), g(22, '2026-10-05')]
    expect(seasonContents(seasons[0], seasons, [], games).loose.map((x) => x.id)).toEqual([20])
  })
})
