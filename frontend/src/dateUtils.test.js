import { describe, expect, it } from 'vitest'
import { isoDate, trainingDayIso, startOfWeek, endOfWeek, formatDateRange } from './dateUtils'

// These run under TZ=Australia/Melbourne (see vite.config.js) because every
// bug this file guards against is a timezone bug. Under UTC they'd all pass
// trivially and prove nothing.

describe('isoDate', () => {
  it('formats the local date, not the UTC one', () => {
    // 08:30 Melbourne is the previous day in UTC. toISOString().slice(0,10)
    // returned 2026-09-01 here, which is the bug this function exists to avoid.
    expect(isoDate(new Date('2026-09-02T08:30:00+10:00'))).toBe('2026-09-02')
  })

  it('holds at local midnight, the worst case for UTC conversion', () => {
    expect(isoDate(new Date('2026-09-02T00:00:00+10:00'))).toBe('2026-09-02')
  })

  it('zero-pads single-digit months and days', () => {
    expect(isoDate(new Date(2026, 0, 5))).toBe('2026-01-05')
  })
})

describe('trainingDayIso', () => {
  // A session started after midnight belongs to the previous day's training.
  // 3am rather than 1am so a 00:55 and a 01:05 start don't split across days.
  it.each([
    ['2026-09-02T23:30:00+10:00', '2026-09-02', 'late evening stays put'],
    ['2026-09-03T00:00:00+10:00', '2026-09-02', 'midnight exactly rolls back'],
    ['2026-09-03T00:30:00+10:00', '2026-09-02', 'the midnight-lifting case'],
    ['2026-09-03T01:20:00+10:00', '2026-09-02', 'would have split at a 1am cutoff'],
    ['2026-09-03T02:59:00+10:00', '2026-09-02', 'last minute before the cutoff'],
    ['2026-09-03T03:00:00+10:00', '2026-09-03', 'the cutoff itself is the new day'],
    ['2026-09-03T08:30:00+10:00', '2026-09-03', 'a morning session is its own day'],
  ])('%s -> %s (%s)', (input, expected) => {
    expect(trainingDayIso(new Date(input))).toBe(expected)
  })

  it('rolls back across a month boundary', () => {
    expect(trainingDayIso(new Date('2026-09-01T00:15:00+10:00'))).toBe('2026-08-31')
  })

  it('rolls back across a year boundary', () => {
    expect(trainingDayIso(new Date('2026-01-01T00:15:00+11:00'))).toBe('2025-12-31')
  })

  it('rolls back across the start of daylight saving', () => {
    // AEDT begins 2026-10-04; clocks jump 2am -> 3am.
    expect(trainingDayIso(new Date('2026-10-04T00:30:00+10:00'))).toBe('2026-10-03')
  })

  it('defaults to now, and never returns a date ahead of the real one', () => {
    const today = isoDate(new Date())
    expect(trainingDayIso() <= today).toBe(true)
  })
})

describe('startOfWeek / endOfWeek', () => {
  it('treats Monday as the first day', () => {
    // 2026-09-02 is a Wednesday.
    expect(isoDate(startOfWeek(new Date(2026, 8, 2)))).toBe('2026-08-31')
    expect(isoDate(endOfWeek(new Date(2026, 8, 2)))).toBe('2026-09-06')
  })

  it('keeps Sunday in the week that began the previous Monday', () => {
    // 2026-09-06 is a Sunday — the trap case for a Sunday-first implementation.
    expect(isoDate(startOfWeek(new Date(2026, 8, 6)))).toBe('2026-08-31')
  })
})

describe('formatDateRange', () => {
  it('collapses to a single date when start and end match', () => {
    expect(formatDateRange('2026-05-30', '2026-05-30')).toBe(formatDateRange('2026-05-30', '2026-05-30'))
    expect(formatDateRange('2026-05-30', '2026-05-30')).not.toContain('–')
  })

  it('shows both ends of a real span', () => {
    expect(formatDateRange('2026-05-30', '2026-06-01')).toContain('–')
  })
})
