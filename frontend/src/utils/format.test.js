import { describe, expect, it } from 'vitest'
import { MONTH_NAMES, SET_TYPE_LABELS, formatClock, formatVolume, formatWeight } from './format'

describe('formatWeight', () => {
  it('drops the decimal on whole kilos', () => {
    expect(formatWeight(60)).toBe('60')
    expect(formatWeight(2.0)).toBe('2')
  })

  it('keeps one place on half kilos', () => {
    expect(formatWeight(62.5)).toBe('62.5')
    expect(formatWeight(13.5)).toBe('13.5')
  })

  it('accepts strings, since the API and form inputs both supply them', () => {
    expect(formatWeight('100')).toBe('100')
    expect(formatWeight('47.5')).toBe('47.5')
  })

  it('handles the negative loads used for assisted lifts', () => {
    // Assistance is stored as negative kg — see the 2026-09-01 LiftProgress work.
    expect(formatWeight(-14)).toBe('-14')
    expect(formatWeight(-7.5)).toBe('-7.5')
  })

  it('renders bodyweight as plain zero', () => {
    expect(formatWeight(0)).toBe('0')
  })
})

describe('formatVolume', () => {
  it('rounds to whole numbers below 1000', () => {
    expect(formatVolume(840)).toBe('840')
    expect(formatVolume(840.6)).toBe('841')
  })

  it('switches to thousands at exactly 1000', () => {
    expect(formatVolume(999)).toBe('999')
    expect(formatVolume(1000)).toBe('1.0k')
  })

  it('keeps one decimal place in the thousands', () => {
    expect(formatVolume(15100)).toBe('15.1k')
  })
})

describe('formatClock', () => {
  it('pads seconds to two digits', () => {
    expect(formatClock(5)).toBe('0:05')
    expect(formatClock(65)).toBe('1:05')
  })

  it('handles the minute boundary', () => {
    expect(formatClock(59)).toBe('0:59')
    expect(formatClock(60)).toBe('1:00')
  })

  it('counts past an hour in minutes rather than rolling over', () => {
    // Rest timers cap well below this, but a session clock does not.
    expect(formatClock(3600)).toBe('60:00')
  })
})

describe('label maps', () => {
  it('leaves the common set type unmarked', () => {
    expect(SET_TYPE_LABELS.Normal).toBe('')
    expect(SET_TYPE_LABELS.Warmup).toBe('W')
  })

  it('covers every set type the backend can send', () => {
    // Mirrors backend SetType.cs — a new type added there without a label
    // here renders as undefined in the history views.
    expect(Object.keys(SET_TYPE_LABELS).sort()).toEqual(['Drop', 'Failure', 'Normal', 'Warmup'])
  })

  it('has twelve months starting at January', () => {
    expect(MONTH_NAMES).toHaveLength(12)
    expect(MONTH_NAMES[0]).toBe('January')
    expect(MONTH_NAMES[11]).toBe('December')
  })
})
