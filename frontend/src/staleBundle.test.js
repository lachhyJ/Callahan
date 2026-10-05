import { describe, expect, it, vi } from 'vitest'
import { createStaleBundleCheck, entryScript } from './staleBundle'

const page = (hash) => `<html><head>
<script type="module" crossorigin src="/assets/index-${hash}.js"></script>
<link rel="modulepreload" href="/assets/vendor-pFjnpeXZ.js">
</head></html>`

function setup({ running = 'assets/index-AAA.js', served, active = null }) {
  const reload = vi.fn()
  const check = createStaleBundleCheck({
    runningEntry: running,
    fetchIndex: () => (served instanceof Error ? Promise.reject(served) : Promise.resolve(served)),
    loadActiveWorkout: () => active,
    reload,
  })
  return { check, reload }
}

describe('entryScript', () => {
  it('picks the hashed entry script, not the vendor chunk', () => {
    expect(entryScript(page('AAA'))).toBe('assets/index-AAA.js')
  })

  it('returns null when the page has no entry script', () => {
    expect(entryScript('<html>502 Bad Gateway</html>')).toBeNull()
  })
})

describe('createStaleBundleCheck', () => {
  it('reloads when the served entry script differs from the running one', async () => {
    const { check, reload } = setup({ served: page('BBB') })
    await check()
    expect(reload).toHaveBeenCalledTimes(1)
  })

  it('does nothing when the bundle is current', async () => {
    const { check, reload } = setup({ served: page('AAA') })
    await check()
    expect(reload).not.toHaveBeenCalled()
  })

  it('holds off while a workout is in progress', async () => {
    const { check, reload } = setup({ served: page('BBB'), active: { templateId: '1' } })
    await check()
    expect(reload).not.toHaveBeenCalled()
  })

  it('does nothing when the running entry is unknown (dev server)', async () => {
    const { check, reload } = setup({ running: null, served: page('BBB') })
    await check()
    expect(reload).not.toHaveBeenCalled()
  })

  it('ignores a failed fetch and an error page', async () => {
    const failed = setup({ served: new Error('offline') })
    await failed.check()
    expect(failed.reload).not.toHaveBeenCalled()

    const errorPage = setup({ served: '<html>502 Bad Gateway</html>' })
    await errorPage.check()
    expect(errorPage.reload).not.toHaveBeenCalled()
  })
})
