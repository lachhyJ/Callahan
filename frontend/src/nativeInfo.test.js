import { describe, expect, it } from 'vitest'
import { nativeBuildTag } from './nativeInfo'

const base = { branch: 'main', commit: 'abc1234', dirty: false, behind: 0 }

describe('nativeBuildTag', () => {
  it('is just the commit for a clean, current main build', () => {
    expect(nativeBuildTag(base)).toBe('abc1234')
  })

  it('marks uncommitted changes and a checkout behind origin/main', () => {
    expect(nativeBuildTag({ ...base, dirty: true })).toBe('abc1234+')
    expect(nativeBuildTag({ ...base, behind: 2 })).toBe('abc1234↓2')
    expect(nativeBuildTag({ ...base, dirty: true, behind: 2 })).toBe('abc1234+↓2')
  })

  it('spells out a non-main branch', () => {
    expect(nativeBuildTag({ ...base, branch: 'footer' })).toBe('footer@abc1234')
  })

  it('tolerates an older binary that sends no behind count', () => {
    expect(nativeBuildTag({ branch: 'main', commit: 'abc1234', dirty: false })).toBe('abc1234')
  })

  it('appends signing days left, or expired', () => {
    const now = Date.now()
    const inDays = (n) => new Date(now + n * 86400000 - 3600000).toISOString()
    expect(nativeBuildTag({ ...base, provisioningExpiresAt: inDays(6) })).toBe('abc1234 · 6d')
    expect(nativeBuildTag({ ...base, provisioningExpiresAt: inDays(-1) })).toBe('abc1234 · signing expired')
  })
})
