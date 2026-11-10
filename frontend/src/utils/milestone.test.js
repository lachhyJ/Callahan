import { describe, expect, it } from 'vitest'
import { upcomingMilestone } from './milestone'

describe('upcomingMilestone', () => {
  const today = '2026-11-10'

  it('keeps the day and names the milestone with how far off it is', () => {
    expect(upcomingMilestone({ title: 'T0 testing', day: '2026-11-24' }, today))
      .toEqual({ day: '2026-11-24', label: 'T0 testing · in 14 days' })
  })

  it('says today and tomorrow in words', () => {
    expect(upcomingMilestone({ title: 'T0 testing', day: '2026-11-10' }, today).label).toBe('T0 testing · today')
    expect(upcomingMilestone({ title: 'T0 testing', day: '2026-11-11' }, today).label).toBe('T0 testing · tomorrow')
  })

  it('drops no milestone, an incomplete one, or one already past', () => {
    expect(upcomingMilestone(null, today)).toBeNull()
    expect(upcomingMilestone({ title: '', day: '2026-11-24' }, today)).toBeNull()
    expect(upcomingMilestone({ title: 'T0 testing', day: '2026-11-09' }, today)).toBeNull()
  })
})
