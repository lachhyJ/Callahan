import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { clearAllCache, peekCache, staleWhileRevalidate } from './swrCache'

// vitest runs in node here, so localStorage is stubbed with a plain Map.
function stubStorage() {
  const store = new Map()
  vi.stubGlobal('localStorage', {
    getItem: (k) => (store.has(k) ? store.get(k) : null),
    setItem: (k, v) => store.set(k, String(v)),
    removeItem: (k) => store.delete(k),
    key: (i) => [...store.keys()][i] ?? null,
    get length() { return store.size },
  })
  return store
}

describe('peekCache', () => {
  beforeEach(() => { stubStorage() })
  afterEach(() => { vi.unstubAllGlobals() })

  it('is null for a key that was never cached', () => {
    expect(peekCache('nope')).toBeNull()
  })

  it('returns the data staleWhileRevalidate last stored, synchronously', async () => {
    await staleWhileRevalidate('k', () => Promise.resolve({ a: 1 }), () => {})
    expect(peekCache('k')).toEqual({ a: 1 })
  })

  it('is null when storage is unavailable rather than throwing', () => {
    vi.stubGlobal('localStorage', { getItem: () => { throw new Error('denied') } })
    expect(peekCache('k')).toBeNull()
  })

  it('treats a cached null (a 204) as empty, same as nothing cached', async () => {
    await staleWhileRevalidate('k', () => Promise.resolve(null), () => {})
    expect(peekCache('k')).toBeNull()
  })
})

describe('staleWhileRevalidate', () => {
  beforeEach(() => { stubStorage() })
  afterEach(() => { vi.unstubAllGlobals() })

  it('hands back the cached value first, then the fresh one', async () => {
    await staleWhileRevalidate('k', () => Promise.resolve('old'), () => {})
    const seen = []
    await staleWhileRevalidate('k', () => Promise.resolve('new'), (d) => seen.push(d))
    expect(seen).toEqual(['old', 'new'])
  })

  it('keeps the cached value when the background fetch fails', async () => {
    await staleWhileRevalidate('k', () => Promise.resolve('old'), () => {})
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    const seen = []
    await staleWhileRevalidate('k', () => Promise.reject(new Error('offline')), (d) => seen.push(d))
    expect(seen).toEqual(['old'])
    warn.mockRestore()
  })
})

describe('clearAllCache', () => {
  it('is safe to call when storage is unavailable', () => {
    vi.stubGlobal('localStorage', undefined)
    expect(() => clearAllCache()).not.toThrow()
    vi.unstubAllGlobals()
  })
})
