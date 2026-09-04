import { describe, it, expect } from 'vitest'
import { shouldOfferCreate } from './exerciseCreate'

const catalog = [{ name: 'Bench Press' }, { name: 'Pull-up' }]

describe('shouldOfferCreate', () => {
  it('offers to create a name the catalog does not have', () => {
    expect(shouldOfferCreate('Nordic Curl', catalog)).toBe(true)
  })

  it('stays quiet for an exact existing name', () => {
    expect(shouldOfferCreate('Bench Press', catalog)).toBe(false)
  })

  // The duplicate this guards against is permanent and splits an exercise's
  // history, so near-misses a person would read as the same name must not
  // slip through.
  it('treats case and surrounding whitespace as the same name', () => {
    expect(shouldOfferCreate('bench press', catalog)).toBe(false)
    expect(shouldOfferCreate('  Bench Press  ', catalog)).toBe(false)
    expect(shouldOfferCreate('BENCH PRESS', catalog)).toBe(false)
  })

  it('matches against a catalog entry with untrimmed whitespace too', () => {
    expect(shouldOfferCreate('Pull-up', [{ name: ' Pull-up ' }])).toBe(false)
  })

  it('offers nothing for an empty or whitespace-only query', () => {
    expect(shouldOfferCreate('', catalog)).toBe(false)
    expect(shouldOfferCreate('   ', catalog)).toBe(false)
  })

  it('handles a catalog that has not loaded yet', () => {
    expect(shouldOfferCreate('Nordic Curl', null)).toBe(true)
  })
})
