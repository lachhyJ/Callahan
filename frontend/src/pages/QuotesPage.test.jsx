import { describe, expect, it, vi } from 'vitest'
import { renderToString } from 'react-dom/server'

// Mounting smoke test. A server render runs every render-body statement, so an
// undefined identifier or a bad destructure throws here (the class of bug that has
// reached main before). Effects do not run under renderToString, so this covers the
// initial "loading" render only, not the list, the add form's warnings or Undo.

vi.mock('../api/client', () => new Proxy({}, {
  get: (_, key) => (key === 'then' || typeof key === 'symbol' ? undefined : vi.fn(() => new Promise(() => {}))),
  has: () => true,
}))

describe('QuotesPage', () => {
  it('renders its initial state without throwing', async () => {
    const { default: QuotesPage } = await import('./QuotesPage')
    const html = renderToString(<QuotesPage />)
    expect(html).toContain('Quotes')
    expect(html).toContain('Loading quotes')
    expect(html).toContain('One per line')
  })
})
