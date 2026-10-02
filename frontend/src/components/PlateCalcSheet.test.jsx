import { describe, expect, it, vi } from 'vitest'
import { renderToString } from 'react-dom/server'

// Mounting smoke test for the open sheet. ActiveWorkoutPage's own smoke test never
// opens it, which is how ExercisePlanNote referencing a dropped `suffix` variable sat
// on main until found by hand. A server render runs every render-body statement, so
// that class of bug (undefined identifier, bad destructure) throws here.
// Effects don't run under renderToString: the sheet always renders as its initial
// 'barbell' type, so the barbell path is the one covered. It says nothing about
// data flow or the other equipment types.

// The server renderer rejects portals, and there's no jsdom here; pass children through.
vi.mock('react-dom', async (importOriginal) => ({
  ...(await importOriginal()),
  createPortal: (children) => children,
}))

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
globalThis.document = { body: {}, addEventListener: () => {}, removeEventListener: () => {} }

async function render(props) {
  const { default: PlateCalcSheet } = await import('./PlateCalcSheet')
  return renderToString(
    <PlateCalcSheet exerciseId={7} exerciseName="Barbell Squat" targetWeightKg="100" onApplyWeight={() => {}} onClose={() => {}} {...props} />,
  )
}

describe('PlateCalcSheet', () => {
  it('renders closed without throwing', async () => {
    expect((await render({ exerciseId: null, exerciseName: null, targetWeightKg: '' })).length).toBeGreaterThan(0)
  })

  it('renders open with a bare target', async () => {
    expect((await render({})).length).toBeGreaterThan(0)
  })

  it('renders the whole-exercise plan when later sets have known weights', async () => {
    const html = await render({ currentlyLoadedKg: 80, upcomingWeightsKg: [105, 110], previousTargetKg: 95 })
    expect(html).toContain('Rest of this exercise')
  })

  it('renders the plan without a loaded baseline', async () => {
    const html = await render({ upcomingWeightsKg: [105, 110] })
    expect(html).toContain('Rest of this exercise')
  })

  it('renders for a warm-up set with confirmed working sets', async () => {
    const html = await render({ isCurrentSetWarmup: true, anyWorkingSetConfirmed: true, upcomingWeightsKg: [60] })
    expect(html.length).toBeGreaterThan(0)
  })
})
