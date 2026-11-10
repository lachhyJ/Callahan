import { describe, expect, it, vi } from 'vitest'
import { renderToString } from 'react-dom/server'
import { MemoryRouter } from 'react-router-dom'

vi.mock('../api/client', () => new Proxy({}, {
  get: (_, key) => (key === 'then' || typeof key === 'symbol' ? undefined : vi.fn(() => new Promise(() => {}))),
  has: () => true,
}))

async function card(t) {
  const { TemplateCard } = await import('./WorkoutTemplatesPage')
  return renderToString(<MemoryRouter><TemplateCard t={t} /></MemoryRouter>)
}

describe('TemplateCard', () => {
  it('marks a due session', async () => {
    const html = await card({ id: 7, name: 'Gym A', subtitle: 'Lower', due: true })
    expect(html).toContain('Gym A')
    expect(html).toContain('template-card-due')
    expect(html).toContain('>Due<')
  })

  it.each([false, undefined])('shows no pill when due is %s', async (due) => {
    const html = await card({ id: 8, name: 'Gym B', subtitle: 'Upper', due })
    expect(html).toContain('Gym B')
    expect(html).not.toContain('template-card-due')
  })
})
