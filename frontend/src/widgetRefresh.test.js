import { describe, expect, it, vi } from 'vitest'
import { createWidgetRefresher } from './widgetRefresh'

// A timer the test fires by hand, so "after the burst settles" is exact.
function manualTimers() {
  let next = 1
  const scheduled = new Map()
  return {
    setTimer: (fn) => {
      const id = next++
      scheduled.set(id, fn)
      return id
    },
    clearTimer: (id) => scheduled.delete(id),
    fire: () => {
      const fns = [...scheduled.values()]
      scheduled.clear()
      fns.forEach((fn) => fn())
    },
    pendingCount: () => scheduled.size,
  }
}

// Let the promise chain inside the refresher run.
const flush = () => new Promise((resolve) => setTimeout(resolve, 0))

describe('createWidgetRefresher', () => {
  it('does nothing on the web build', async () => {
    const t = manualTimers()
    const reload = vi.fn()
    const refresh = createWidgetRefresher({ isNative: false, reload, ...t })

    refresh()
    t.fire()
    await flush()

    expect(t.pendingCount()).toBe(0)
    expect(reload).not.toHaveBeenCalled()
  })

  it('collapses a burst of calls into one reload', async () => {
    const t = manualTimers()
    const reload = vi.fn().mockResolvedValue(undefined)
    const refresh = createWidgetRefresher({ isNative: true, reload, ...t })

    refresh()
    refresh()
    refresh()
    expect(t.pendingCount()).toBe(1)
    expect(reload).not.toHaveBeenCalled()

    t.fire()
    await flush()
    expect(reload).toHaveBeenCalledTimes(1)
  })

  it('reloads again for a later burst', async () => {
    const t = manualTimers()
    const reload = vi.fn().mockResolvedValue(undefined)
    const refresh = createWidgetRefresher({ isNative: true, reload, ...t })

    refresh()
    t.fire()
    await flush()
    refresh()
    t.fire()
    await flush()

    expect(reload).toHaveBeenCalledTimes(2)
  })

  it('swallows a rejected reload, such as an installed build with no plugin method', async () => {
    const t = manualTimers()
    const reload = vi.fn().mockRejectedValue(new Error('"WidgetBridge.reload()" is not implemented on ios'))
    const refresh = createWidgetRefresher({ isNative: true, reload, ...t })
    const unhandled = vi.fn()
    process.on('unhandledRejection', unhandled)

    refresh()
    t.fire()
    await flush()
    process.off('unhandledRejection', unhandled)

    expect(reload).toHaveBeenCalledTimes(1)
    expect(unhandled).not.toHaveBeenCalled()
  })

  it('swallows a synchronous throw from reload', async () => {
    const t = manualTimers()
    const reload = vi.fn(() => {
      throw new Error('boom')
    })
    const refresh = createWidgetRefresher({ isNative: true, reload, ...t })

    refresh()
    expect(() => t.fire()).not.toThrow()
    await flush()
    expect(reload).toHaveBeenCalledTimes(1)
  })

  it('waits the configured delay', () => {
    const setTimer = vi.fn(() => 1)
    const refresh = createWidgetRefresher({ isNative: true, reload: vi.fn(), setTimer, clearTimer: vi.fn(), delayMs: 1234 })

    refresh()

    expect(setTimer).toHaveBeenCalledWith(expect.any(Function), 1234)
  })
})
