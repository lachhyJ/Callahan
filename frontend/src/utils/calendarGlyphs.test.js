import { describe, it, expect } from 'vitest'
import { activityDots, isTournamentDay, tournamentsByDay } from './calendarGlyphs'

const cls = (dots) => dots.map((d) => d.className)

describe('activityDots', () => {
  it('is empty for no activities', () => {
    expect(activityDots([])).toEqual([])
    expect(activityDots(undefined)).toEqual([])
  })

  it('a classified Ultimate game -> one teal triangle', () => {
    expect(cls(activityDots([
      { type: 'Ultimate', sessionTypes: [{ name: 'Game', family: 'Ultimate' }] },
    ]))).toEqual(['calendar-dot calendar-dot-ultimate calendar-dot--triangle'])
  })

  it('a multi-tag activity shows every distinct coloured shape, blue lane first', () => {
    const dots = cls(activityDots([
      {
        type: 'Ultimate',
        sessionTypes: [
          { name: 'Field 1', family: 'Field' },
          { name: 'Throws', family: 'Ultimate' },
        ],
      },
    ]))
    expect(dots).toEqual([
      'calendar-dot calendar-dot-run calendar-dot--triangle',
      'calendar-dot calendar-dot-ultimate calendar-dot--disc',
    ])
  })

  it('dedupes tags that map to the same coloured shape', () => {
    // Throws and Pod both -> teal disc
    expect(cls(activityDots([
      { type: 'Ultimate', sessionTypes: [
        { name: 'Throws', family: 'Ultimate' },
        { name: 'Pod', family: 'Ultimate' },
      ] },
    ]))).toEqual(['calendar-dot calendar-dot-ultimate calendar-dot--disc'])
  })

  it('a field session recorded as a run still lands in the blue lane', () => {
    expect(cls(activityDots([
      { type: 'Running', sessionTypes: [{ name: 'Field 2', family: 'Field' }] },
    ]))).toEqual(['calendar-dot calendar-dot-run calendar-dot--diamond'])
  })

  it('an unclassified activity falls back to its Garmin type’s misc shape', () => {
    expect(cls(activityDots([{ type: 'Ultimate', sessionTypes: [] }])))
      .toEqual(['calendar-dot calendar-dot-ultimate calendar-dot--square-hollow'])
    expect(cls(activityDots([{ type: 'Running', sessionTypes: [] }])))
      .toEqual(['calendar-dot calendar-dot-run calendar-dot--ring'])
  })

  it('combines across separate activities on the same day', () => {
    const dots = cls(activityDots([
      { type: 'Running', sessionTypes: [{ name: 'Easy Aerobic Run', family: 'Run' }] },
      { type: 'Ultimate', sessionTypes: [{ name: 'Game', family: 'Ultimate' }] },
    ]))
    expect(dots).toEqual([
      'calendar-dot calendar-dot-run calendar-dot--disc',
      'calendar-dot calendar-dot-ultimate calendar-dot--triangle',
    ])
  })

  it('solo is the hollow partner of the throws/pod disc', () => {
    expect(cls(activityDots([
      { type: 'Ultimate', sessionTypes: [{ name: 'Solo', family: 'Ultimate' }] },
      { type: 'Ultimate', sessionTypes: [{ name: 'Throws', family: 'Ultimate' }] },
    ]))).toEqual([
      'calendar-dot calendar-dot-ultimate calendar-dot--ring',
      'calendar-dot calendar-dot-ultimate calendar-dot--disc',
    ])
  })

  it('club training is the hollow partner of a game triangle', () => {
    expect(cls(activityDots([
      { type: 'Ultimate', sessionTypes: [{ name: 'Club Training', family: 'Ultimate' }] },
      { type: 'Ultimate', sessionTypes: [{ name: 'Game', family: 'Ultimate' }] },
    ]))).toEqual([
      'calendar-dot calendar-dot-ultimate calendar-dot--triangle-hollow',
      'calendar-dot calendar-dot-ultimate calendar-dot--triangle',
    ])
  })
})

describe('isTournamentDay', () => {
  it('is true when any activity is attached to a tournament', () => {
    expect(isTournamentDay([{ tournamentId: null }, { tournamentId: 4 }])).toBe(true)
  })

  it('is false with no tournament links or no activities', () => {
    expect(isTournamentDay([{ tournamentId: null }, {}])).toBe(false)
    expect(isTournamentDay(undefined)).toBe(false)
  })
})

describe('tournamentsByDay', () => {
  it('covers every day from start to end inclusive', () => {
    const days = tournamentsByDay([{ id: 7, name: 'Melb Tourney 26', startDate: '2026-12-12', endDate: '2026-12-13' }])
    expect([...days.keys()]).toEqual(['2026-12-12', '2026-12-13'])
    expect(days.get('2026-12-13')).toEqual({ id: 7, name: 'Melb Tourney 26' })
  })

  it('crosses a month end and a DST change without skipping a day', () => {
    // Melbourne DST starts 4 Oct 2026
    const days = tournamentsByDay([{ id: 1, name: 'X', startDate: '2026-10-03', endDate: '2026-10-05' }])
    expect([...days.keys()]).toEqual(['2026-10-03', '2026-10-04', '2026-10-05'])
    expect([...tournamentsByDay([{ id: 2, name: 'Y', startDate: '2026-11-30', endDate: '2026-12-01' }]).keys()])
      .toEqual(['2026-11-30', '2026-12-01'])
  })

  it('treats a missing or backwards end date as a single day, and caps long spans', () => {
    expect(tournamentsByDay([{ id: 1, name: 'A', startDate: '2026-12-12', endDate: null }]).size).toBe(1)
    expect(tournamentsByDay([{ id: 1, name: 'A', startDate: '2026-12-12', endDate: '2026-12-01' }]).size).toBe(1)
    expect(tournamentsByDay([{ id: 1, name: 'A', startDate: '2026-01-01', endDate: '2026-12-31' }]).size).toBe(14)
  })

  it('is empty for no tournaments', () => {
    expect(tournamentsByDay(undefined).size).toBe(0)
    expect(tournamentsByDay(null).size).toBe(0)
  })
})
