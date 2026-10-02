import { describe, expect, it } from 'vitest'
import {
  axisTicks,
  buildStatusDays,
  buildStatusRuns,
  DEFAULT_STATUS_RANGE,
  formatAcwr,
  latestStatusRow,
  STATUS_RANGES,
  statusMeta,
  statusRange,
  TRAINING_STATUS,
  wellnessRowsCacheKey,
} from './trainingStatus'

// Runs under TZ=Australia/Melbourne like the other date-handling tests.
const END = new Date(2026, 9, 2) // 2 Oct 2026, local

describe('statusMeta', () => {
  it('maps the codes seen on a real account', () => {
    expect(statusMeta(7).label).toBe('Productive')
    expect(statusMeta(3).label).toBe('Overreaching')
    expect(statusMeta(1).label).toBe('Detraining')
  })

  it('falls back to a neutral Other for an unmapped or missing code', () => {
    expect(statusMeta(99).label).toBe('Other')
    expect(statusMeta(null).label).toBe('Other')
  })

  it('gives every known status a distinct label and colour', () => {
    const labels = Object.values(TRAINING_STATUS).map((s) => s.label)
    const colors = Object.values(TRAINING_STATUS).map((s) => s.color)
    expect(new Set(labels).size).toBe(labels.length)
    expect(new Set(colors).size).toBe(colors.length)
  })
})

describe('buildStatusDays', () => {
  it('covers exactly `days` days ending on the end date, null where unsynced', () => {
    const days = buildStatusDays(
      [{ date: '2026-10-01', trainingStatusCode: 3 }, { date: '2026-10-02', trainingStatusCode: 7 }],
      4,
      END,
    )
    expect(days.map((d) => d.date)).toEqual(['2026-09-29', '2026-09-30', '2026-10-01', '2026-10-02'])
    expect(days.map((d) => d.code)).toEqual([null, null, 3, 7])
  })

  it('treats a row with no status as a gap', () => {
    const days = buildStatusDays([{ date: '2026-10-02', trainingStatusCode: null }], 1, END)
    expect(days[0].code).toBeNull()
  })
})

describe('buildStatusRuns', () => {
  it('collapses consecutive same-status days and keeps gaps as null runs', () => {
    const runs = buildStatusRuns([
      { date: '2026-09-28', code: 7 },
      { date: '2026-09-29', code: 7 },
      { date: '2026-09-30', code: 3 },
      { date: '2026-10-01', code: null },
      { date: '2026-10-02', code: 7 },
    ])
    expect(runs).toEqual([
      { code: 7, start: '2026-09-28', end: '2026-09-29', days: 2 },
      { code: 3, start: '2026-09-30', end: '2026-09-30', days: 1 },
      { code: null, start: '2026-10-01', end: '2026-10-01', days: 1 },
      { code: 7, start: '2026-10-02', end: '2026-10-02', days: 1 },
    ])
  })

  it('does not merge the same status either side of a different one', () => {
    const runs = buildStatusRuns([
      { date: '2026-09-01', code: 4 },
      { date: '2026-09-02', code: 5 },
      { date: '2026-09-03', code: 4 },
    ])
    expect(runs.map((r) => r.code)).toEqual([4, 5, 4])
  })

  it('returns no runs for no days', () => {
    expect(buildStatusRuns([])).toEqual([])
  })
})

describe('latestStatusRow', () => {
  it('picks the newest row that has a status, ignoring newer rows without one', () => {
    const rows = [
      { date: '2026-10-01', trainingStatusCode: 3 },
      { date: '2026-10-02', trainingStatusCode: null },
      { date: '2026-09-30', trainingStatusCode: 7 },
    ]
    expect(latestStatusRow(rows).date).toBe('2026-10-01')
  })

  it('is null when nothing has a status', () => {
    expect(latestStatusRow([{ date: '2026-10-01', trainingStatusCode: null }])).toBeNull()
    expect(latestStatusRow(null)).toBeNull()
  })
})

describe('formatAcwr', () => {
  it('formats the ratio with the loads behind it', () => {
    expect(formatAcwr({ acwrRatio: 1.7, acuteLoad: 431, chronicLoad: 245 })).toBe(
      'ratio 1.7 · acute 431 · chronic 245',
    )
  })

  it('is null without a ratio', () => {
    expect(formatAcwr({ acwrRatio: null, acuteLoad: 431 })).toBeNull()
    expect(formatAcwr(null)).toBeNull()
  })
})

describe('statusRange', () => {
  it('offers 4w / 12w / 6m, longest last', () => {
    expect(STATUS_RANGES.map((r) => r.key)).toEqual(['4w', '12w', '6m'])
    const days = STATUS_RANGES.map((r) => r.days)
    expect(days).toEqual([...days].sort((a, b) => a - b))
  })

  it('looks a range up by key', () => {
    expect(statusRange('6m').days).toBe(182)
  })

  it('falls back to the default for an unknown or stale stored key', () => {
    expect(statusRange('1y').key).toBe(DEFAULT_STATUS_RANGE)
    expect(statusRange(null).key).toBe(DEFAULT_STATUS_RANGE)
  })
})

describe('wellnessRowsCacheKey', () => {
  it('puts the window length in the key and no dates', () => {
    expect(wellnessRowsCacheKey(182)).toBe('wellness-rows-182d')
    expect(wellnessRowsCacheKey(84)).not.toBe(wellnessRowsCacheKey(182))
  })
})

describe('axisTicks', () => {
  it('includes the first and last day and spaces the rest evenly', () => {
    const days = buildStatusDays([], 29, END) // 29 days: index 0..28
    const ticks = axisTicks(days, 3)
    expect(ticks).toEqual([days[0].date, days[14].date, days[28].date])
  })

  it('returns fewer ticks than asked for when the window is short', () => {
    expect(axisTicks(buildStatusDays([], 2, END), 4)).toHaveLength(2)
  })

  it('is empty for no days', () => {
    expect(axisTicks([], 4)).toEqual([])
    expect(axisTicks(null, 4)).toEqual([])
  })
})
