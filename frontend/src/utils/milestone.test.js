import { describe, expect, it } from 'vitest'
import { milestoneLine } from './milestone'

describe('milestoneLine', () => {
  const today = '2026-11-10'

  it('names the milestone, its day and how far off it is', () => {
    expect(milestoneLine({ title: 'T0 testing', day: '2026-11-24' }, today)).toBe('T0 testing · Tue 24 Nov · in 14 days')
  })

  it('says today and tomorrow in words', () => {
    expect(milestoneLine({ title: 'T0 testing', day: '2026-11-10' }, today)).toMatch(/· today$/)
    expect(milestoneLine({ title: 'T0 testing', day: '2026-11-11' }, today)).toMatch(/· tomorrow$/)
  })

  it('shows nothing for no milestone or one already past', () => {
    expect(milestoneLine(null, today)).toBeNull()
    expect(milestoneLine({ title: 'T0 testing', day: '2026-11-09' }, today)).toBeNull()
  })
})
