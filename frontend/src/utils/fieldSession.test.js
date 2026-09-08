import { describe, it, expect } from 'vitest'
import { isFieldActivity } from './fieldSession'

describe('isFieldActivity', () => {
  it('is true when any tag has family Field', () => {
    expect(isFieldActivity({
      sessionTypes: [
        { name: 'Club Training', family: 'Ultimate' },
        { name: 'Field 2 - Repeat Effort & COD', family: 'Field' },
      ],
    })).toBe(true)
  })

  it('is true for a field primary', () => {
    expect(isFieldActivity({
      sessionTypes: [{ name: 'Field 1 - Acceleration & Jump Quality', family: 'Field' }],
    })).toBe(true)
  })

  it('is false for a non-field tag set', () => {
    expect(isFieldActivity({
      sessionTypes: [{ name: 'Game', family: 'Ultimate' }],
    })).toBe(false)
  })

  it('is false when unclassified', () => {
    expect(isFieldActivity({ sessionTypes: [] })).toBe(false)
    expect(isFieldActivity({})).toBe(false)
  })

  it('falls back to the name prefix when sessionTypes is absent', () => {
    expect(isFieldActivity({ activitySessionTypeName: 'Field 1 - Acceleration & Jump Quality' })).toBe(true)
    expect(isFieldActivity({ activitySessionTypeName: 'Easy Aerobic Run' })).toBe(false)
    expect(isFieldActivity({ activitySessionTypeName: null })).toBe(false)
  })
})
