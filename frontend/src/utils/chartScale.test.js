import { describe, expect, it } from 'vitest'
import { buildTicks, linearScale, niceStep } from './chartScale'

describe('niceStep', () => {
  it('picks a round step for the rough range/3', () => {
    expect(niceStep(30)).toBe(10)
    expect(niceStep(3)).toBe(1)
    expect(niceStep(300)).toBe(100)
  })

  it('survives a zero range without returning NaN', () => {
    expect(Number.isFinite(niceStep(0))).toBe(true)
  })
})

describe('buildTicks', () => {
  it('walks from zero to the max inclusive', () => {
    expect(buildTicks(0, 4, 1)).toEqual([0, 1, 2, 3, 4])
  })

  it('starts at the first multiple of step at or above min', () => {
    expect(buildTicks(12, 40, 10)).toEqual([20, 30, 40])
    expect(buildTicks(20, 40, 10)).toEqual([20, 30, 40])
  })

  it('keeps the final tick that float drift would otherwise drop', () => {
    // 0.1-style accumulation lands just past the max without the epsilon.
    expect(buildTicks(0, 3, 0.1)).toContain(3)
  })

  it('does not emit 7.500000000000001 as a label', () => {
    expect(buildTicks(0, 10, 2.5)).toEqual([0, 2.5, 5, 7.5, 10])
  })

  it('returns a single tick when min equals max', () => {
    expect(buildTicks(5, 5, 1)).toEqual([5])
  })

  it('reproduces the loops it replaced', () => {
    // The two shapes previously written inline across the chart components.
    const fromZero = (max, step) => { const o = []; for (let t = 0; t <= max; t += step) o.push(t); return o }
    const fromFloor = (min, max, step) => {
      const o = []
      for (let t = Math.ceil(min / step) * step; t <= max + 1e-9; t += step) o.push(Math.round(t * 10) / 10)
      return o
    }
    expect(buildTicks(0, 5, 1)).toEqual(fromZero(5, 1))
    expect(buildTicks(0, 20, 5)).toEqual(fromZero(20, 5))
    expect(buildTicks(62.5, 190, 25)).toEqual(fromFloor(62.5, 190, 25))
    expect(buildTicks(-10, 30, 10)).toEqual(fromFloor(-10, 30, 10))
  })
})

describe('linearScale', () => {
  const y = linearScale({ min: 0, max: 100, top: 10, height: 200 })

  it('puts min at the bottom and max at the top', () => {
    expect(y(0)).toBe(210)
    expect(y(100)).toBe(10)
  })

  it('interpolates linearly in between', () => {
    expect(y(50)).toBe(110)
  })

  it('handles a negative minimum, as the season chart needs', () => {
    const pct = linearScale({ min: -20, max: 40, top: 0, height: 60 })
    expect(pct(-20)).toBe(60)
    expect(pct(40)).toBe(0)
    expect(pct(0)).toBe(40)
  })

  it('does not divide by zero on a flat series', () => {
    const flat = linearScale({ min: 7, max: 7, top: 0, height: 50 })
    expect(Number.isFinite(flat(7))).toBe(true)
  })
})
