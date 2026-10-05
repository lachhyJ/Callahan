import { describe, expect, it, vi } from 'vitest'
import { BUILD_VERSION_KEY, createStaleBundleCheck } from './staleBundle'

function memoryStorage(initial = {}) {
  const data = { ...initial }
  return {
    getItem: (k) => (k in data ? data[k] : null),
    setItem: (k, v) => { data[k] = v },
  }
}

function setup({ versions, stored, active = null }) {
  const queue = [...versions]
  const reload = vi.fn()
  const storage = memoryStorage(stored ? { [BUILD_VERSION_KEY]: stored } : {})
  const check = createStaleBundleCheck({
    getHealth: () => {
      const next = queue.shift()
      return next instanceof Error ? Promise.reject(next) : Promise.resolve({ version: next })
    },
    storage,
    loadActiveWorkout: () => active,
    reload,
  })
  return { check, reload, storage }
}

describe('createStaleBundleCheck', () => {
  it('does not reload on a first visit', async () => {
    const { check, reload, storage } = setup({ versions: ['a'] })
    await check()
    expect(reload).not.toHaveBeenCalled()
    expect(storage.getItem(BUILD_VERSION_KEY)).toBe('a')
  })

  it('reloads at launch when the browser last saw a different version', async () => {
    const { check, reload } = setup({ versions: ['b'], stored: 'a' })
    await check()
    expect(reload).toHaveBeenCalledTimes(1)
  })

  it('reloads on resume when the server version changed since this page loaded', async () => {
    const { check, reload } = setup({ versions: ['a', 'b'], stored: 'a' })
    await check()
    expect(reload).not.toHaveBeenCalled()
    await check()
    expect(reload).toHaveBeenCalledTimes(1)
  })

  it('does not reload on resume when the version is unchanged', async () => {
    const { check, reload } = setup({ versions: ['a', 'a', 'a'] })
    await check()
    await check()
    await check()
    expect(reload).not.toHaveBeenCalled()
  })

  it('holds off while a workout is in progress, then reloads once it ends', async () => {
    let active = { templateId: '1' }
    const reload = vi.fn()
    const queue = ['a', 'b', 'b']
    const check = createStaleBundleCheck({
      getHealth: () => Promise.resolve({ version: queue.shift() }),
      storage: memoryStorage(),
      loadActiveWorkout: () => active,
      reload,
    })
    await check()
    await check()
    expect(reload).not.toHaveBeenCalled()
    active = null
    await check()
    expect(reload).toHaveBeenCalledTimes(1)
  })

  it('ignores a failed health check and keeps its baseline', async () => {
    const { check, reload } = setup({ versions: ['a', new Error('offline'), 'a'] })
    await check()
    await check()
    await check()
    expect(reload).not.toHaveBeenCalled()
  })
})
