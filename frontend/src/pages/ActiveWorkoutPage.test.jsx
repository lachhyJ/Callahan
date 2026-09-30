import { describe, expect, it, vi } from 'vitest'
import { renderToString } from 'react-dom/server'
import { MemoryRouter, Route, Routes } from 'react-router-dom'

// Mounting smoke test. Twice in one week ActiveWorkoutPage threw on first render
// (a ref assigned before its `const` declaration, a TDZ error) and neither lint nor
// the unit tests noticed, because nothing rendered the page. oxlint has no
// no-use-before-define rule, so this is the guard: a server render runs every
// render-body statement and state initialiser, which is where that class of bug lives.
// Effects don't run under renderToString, so it says nothing about data flow.

// Every API call stays pending forever. `then` and symbols return undefined so the
// module doesn't look thenable to the importer.
vi.mock('../api/client', () => new Proxy({}, {
  get: (_, key) => (key === 'then' || typeof key === 'symbol' ? undefined : vi.fn(() => new Promise(() => {}))),
  has: () => true,
}))

const store = new Map()
globalThis.localStorage = {
  getItem: (k) => (store.has(k) ? store.get(k) : null),
  setItem: (k, v) => store.set(k, String(v)),
  removeItem: (k) => store.delete(k),
}

describe('ActiveWorkoutPage', () => {
  it('renders without throwing on first mount', async () => {
    const { default: ActiveWorkoutPage } = await import('./ActiveWorkoutPage')
    for (const path of ['/workout/custom', '/workout/1']) {
      const html = renderToString(
        <MemoryRouter initialEntries={[path]}>
          <Routes>
            <Route path="/workout/custom" element={<ActiveWorkoutPage />} />
            <Route path="/workout/:templateId" element={<ActiveWorkoutPage />} />
          </Routes>
        </MemoryRouter>,
      )
      expect(html.length).toBeGreaterThan(0)
    }
  })
})
