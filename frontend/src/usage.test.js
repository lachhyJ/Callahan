import { describe, expect, it } from 'vitest'
import { normalisePath } from './usage'

describe('normalisePath', () => {
  it('leaves static routes alone', () => {
    for (const p of ['/', '/dashboard', '/trends', '/history', '/wellness', '/reports', '/plate-calculator', '/recently-deleted']) {
      expect(normalisePath(p)).toBe(p)
    }
  })

  it('collapses ids so a screen aggregates instead of splitting per record', () => {
    expect(normalisePath('/exercises/17')).toBe('/exercises/:exerciseId')
    expect(normalisePath('/exercises/23')).toBe('/exercises/:exerciseId')
    expect(normalisePath('/sessions/85')).toBe('/sessions/:sessionId')
    expect(normalisePath('/activities/12')).toBe('/activities/:activityId')
    expect(normalisePath('/tournaments/3')).toBe('/tournaments/:tournamentId')
  })

  it('keeps /workout/custom distinct from a template workout', () => {
    // Ordering matters: /workout/:templateId would swallow this otherwise, and
    // "did I start an empty workout or a programmed one" is a real question.
    expect(normalisePath('/workout/custom')).toBe('/workout/custom')
    expect(normalisePath('/workout/2')).toBe('/workout/:templateId')
  })

  it('handles the non-numeric streak param', () => {
    expect(normalisePath('/streaks/gym')).toBe('/streaks/:type')
    expect(normalisePath('/streaks')).toBe('/streaks')
  })

  it('handles the two-segment report route', () => {
    expect(normalisePath('/reports/2026/8')).toBe('/reports/:year/:month')
    expect(normalisePath('/reports')).toBe('/reports')
  })

  it('does not confuse the list route with the detail route', () => {
    expect(normalisePath('/exercises')).toBe('/exercises')
    expect(normalisePath('/tournaments')).toBe('/tournaments')
  })

  it('strips a trailing slash but keeps root', () => {
    expect(normalisePath('/trends/')).toBe('/trends')
    expect(normalisePath('/')).toBe('/')
  })

  it('survives an empty or missing path', () => {
    expect(normalisePath('')).toBe('/')
    expect(normalisePath(undefined)).toBe('/')
  })

  it('passes through an unknown route rather than dropping it', () => {
    // A route added later should still be recorded, just unaggregated.
    expect(normalisePath('/something-new')).toBe('/something-new')
  })
})
