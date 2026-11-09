import { describe, expect, it } from 'vitest'
import { suggestUltimateSessionType } from './ultimateSessionSuggestion'

const TYPES = [
  { id: 4, name: 'Solo' },
  { id: 5, name: 'Throws' },
  { id: 6, name: 'Pod' },
  { id: 7, name: 'Club Training' },
  { id: 9, name: 'Field 1' },
  { id: 10, name: 'Field 2' },
]

const suggest = (notes, extra = {}) => suggestUltimateSessionType({ notes, ...extra }, TYPES)?.id ?? null

describe('suggestUltimateSessionType', () => {
  it('reads a titled field session as Field 1 or Field 2', () => {
    expect(suggest('Field 1')).toBe(9)
    expect(suggest('Melbourne Ultimate — field 2 session')).toBe(10)
  })

  it('prefers the field session over a keyword it also contains', () => {
    expect(suggest('Field 1 pod')).toBe(9)
    expect(suggest('Throws - field 1 warm-up')).toBe(9)
  })

  it('keeps the existing keywords, and the throws-before-solo order', () => {
    expect(suggest('Throws- Solo')).toBe(5)
    expect(suggest('Pod')).toBe(6)
    expect(suggest('Club Training')).toBe(7)
  })

  it('suggests nothing for a default Garmin title, or an already-classified activity', () => {
    expect(suggest('Melbourne Ultimate Disc')).toBeNull()
    expect(suggest('Field 1', { activitySessionTypeId: 9 })).toBeNull()
    expect(suggest(undefined)).toBeNull()
  })
})
