import { describe, expect, it } from 'vitest'
import {
  BALANCE_BARS,
  balanceBarGeometry,
  balanceHeadline,
  balanceState,
  latestBalanceRow,
} from './loadBalance'

// Real values from Garmin payloads on the account (15 Sep, 8 Oct, 22 Oct 2026).
const SEP_15 = {
  date: '2026-09-15',
  balanceAerobicLow: 245.05133, balanceAerobicLowMin: 194, balanceAerobicLowMax: 457,
  balanceAerobicHigh: 23.412582, balanceAerobicHighMin: 287, balanceAerobicHighMax: 550,
  balanceAnaerobic: 360.5324, balanceAnaerobicMin: 0, balanceAnaerobicMax: 262,
  balancePhrase: 'AEROBIC_HIGH_SHORTAGE',
}
const OCT_22 = {
  date: '2026-10-22',
  balanceAerobicLow: 225.95972, balanceAerobicLowMin: 190, balanceAerobicLowMax: 453,
  balanceAerobicHigh: 386.15875, balanceAerobicHighMin: 290, balanceAerobicHighMax: 553,
  balanceAnaerobic: 439.7343, balanceAnaerobicMin: 0, balanceAnaerobicMax: 262,
  balancePhrase: 'ANAEROBIC_FOCUS',
}

const stateOf = (row, bar) => balanceState(row[bar.value], row[bar.min], row[bar.max])

describe('balanceState', () => {
  it('places each bar of the real 22 Oct payload: two on target, anaerobic over', () => {
    expect(BALANCE_BARS.map((b) => stateOf(OCT_22, b))).toEqual(['on', 'on', 'over'])
  })

  it('flags the 15 Sep shortage in high aerobic, with anaerobic already over', () => {
    expect(BALANCE_BARS.map((b) => stateOf(SEP_15, b))).toEqual(['on', 'short', 'over'])
  })

  it('treats the range ends as on target', () => {
    expect(balanceState(190, 190, 453)).toBe('on')
    expect(balanceState(453, 190, 453)).toBe('on')
    expect(balanceState(189.9, 190, 453)).toBe('short')
    expect(balanceState(453.1, 190, 453)).toBe('over')
  })

  it('can never be short when the target minimum is 0', () => {
    expect(balanceState(0, 0, 262)).toBe('on')
  })

  it('is null when the value or either bound is missing', () => {
    expect(balanceState(null, 0, 262)).toBeNull()
    expect(balanceState(100, null, 262)).toBeNull()
    expect(balanceState(100, 0, undefined)).toBeNull()
  })
})

describe('balanceBarGeometry', () => {
  it('puts the target zone and the fill inside the track', () => {
    const g = balanceBarGeometry(225.95972, 190, 453)
    expect(g.zoneStart).toBeGreaterThan(0)
    expect(g.zoneEnd).toBeLessThan(1)
    expect(g.zoneStart).toBeLessThan(g.zoneEnd)
    expect(g.fill).toBeGreaterThan(g.zoneStart)
    expect(g.fill).toBeLessThan(g.zoneEnd)
  })

  it('keeps an overshoot on the track and past the end of the zone', () => {
    const g = balanceBarGeometry(439.7343, 0, 262)
    expect(g.fill).toBeLessThan(1)
    expect(g.fill).toBeGreaterThan(g.zoneEnd)
    expect(g.zoneStart).toBe(0)
  })

  it('keeps a shortage left of the start of the zone', () => {
    const g = balanceBarGeometry(23.412582, 287, 550)
    expect(g.fill).toBeLessThan(g.zoneStart)
  })

  it('is null for missing input or an empty scale', () => {
    expect(balanceBarGeometry(null, 0, 262)).toBeNull()
    expect(balanceBarGeometry(0, 0, 0)).toBeNull()
  })
})

describe('latestBalanceRow', () => {
  it('returns the newest row that has a balance, skipping newer rows without one', () => {
    const rows = [SEP_15, OCT_22, { date: '2026-10-23' }]
    expect(latestBalanceRow(rows).date).toBe('2026-10-22')
  })

  it('counts a row with only some fields', () => {
    expect(latestBalanceRow([{ date: '2026-10-01', balancePhrase: 'X', balanceAnaerobic: 100 }]).date).toBe('2026-10-01')
  })

  it('is null with no balance anywhere, or no rows', () => {
    expect(latestBalanceRow([{ date: '2026-10-01', sleepScore: 80 }])).toBeNull()
    expect(latestBalanceRow(null)).toBeNull()
  })
})

describe('balanceHeadline', () => {
  it('turns the phrases seen so far into sentence case', () => {
    expect(balanceHeadline('AEROBIC_HIGH_SHORTAGE')).toBe('Aerobic high shortage')
    expect(balanceHeadline('ANAEROBIC_FOCUS')).toBe('Anaerobic focus')
  })

  it('reads an unseen phrase sensibly and returns null for none', () => {
    expect(balanceHeadline('BALANCED')).toBe('Balanced')
    expect(balanceHeadline(null)).toBeNull()
    expect(balanceHeadline('')).toBeNull()
  })
})
