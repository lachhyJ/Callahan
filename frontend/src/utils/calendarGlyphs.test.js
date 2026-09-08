import { describe, it, expect } from 'vitest'
import { activityDots } from './calendarGlyphs'

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
          { name: 'Field 1 - Acceleration & Jump Quality', family: 'Field' },
          { name: 'Throws', family: 'Ultimate' },
        ],
      },
    ]))
    expect(dots).toEqual([
      'calendar-dot calendar-dot-run calendar-dot--triangle',
      'calendar-dot calendar-dot-ultimate calendar-dot--ring',
    ])
  })

  it('dedupes tags that map to the same coloured shape', () => {
    // Throws and Pod both -> teal ring
    expect(cls(activityDots([
      { type: 'Ultimate', sessionTypes: [
        { name: 'Throws', family: 'Ultimate' },
        { name: 'Pod', family: 'Ultimate' },
      ] },
    ]))).toEqual(['calendar-dot calendar-dot-ultimate calendar-dot--ring'])
  })

  it('a field session recorded as a run still lands in the blue lane', () => {
    expect(cls(activityDots([
      { type: 'Running', sessionTypes: [{ name: 'Field 2 - Repeat Effort & COD', family: 'Field' }] },
    ]))).toEqual(['calendar-dot calendar-dot-run calendar-dot--diamond'])
  })

  it('an unclassified activity falls back to its Garmin type’s misc shape', () => {
    expect(cls(activityDots([{ type: 'Ultimate', sessionTypes: [] }])))
      .toEqual(['calendar-dot calendar-dot-ultimate calendar-dot--disc'])
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
})
