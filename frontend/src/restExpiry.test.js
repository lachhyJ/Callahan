import { describe, expect, it } from 'vitest'
import { isRestOver } from './restExpiry'

describe('isRestOver', () => {
  const endAt = 1_000_000

  it('is not over while any time remains, even under half a second', () => {
    expect(isRestOver(endAt, endAt - 1)).toBe(false)
    expect(isRestOver(endAt, endAt - 200)).toBe(false)
    expect(isRestOver(endAt, endAt - 499)).toBe(false)
  })

  it('is over at and after endAt', () => {
    expect(isRestOver(endAt, endAt)).toBe(true)
    expect(isRestOver(endAt, endAt + 5000)).toBe(true)
  })
})
