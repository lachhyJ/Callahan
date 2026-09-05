import { test } from '@playwright/test'

// Presentation screenshots for the README — NOT a regression suite. These write PNGs
// into docs/screenshots/ rather than comparing against baselines, and they run from
// their own config (playwright.shots.config.js) so `npm run test:visual` is untouched.
//
// Two deliberate differences from e2e/visual.spec.js:
//
// 1. Viewport shots, not fullPage. The app's tab bar is `position: fixed; bottom: 0`,
//    so a fullPage capture of a scrolling screen paints it partway down the image
//    (visible in every committed baseline). That is correct for regression — the
//    whole page is the thing under test — and wrong for a screenshot a stranger
//    judges the app by.
// 2. The build stamp is hidden for the same reason it is in the regression suite: it
//    encodes a branch and commit nobody reading the README cares about.
async function prepare(page) {
  await page.addStyleTag({ content: '.build-tag { display: none !important }' })
}

const SHOTS = [
  { name: 'dashboard', path: '/dashboard' },
  { name: 'games', path: '/games' },
  { name: 'wellness', path: '/wellness' },
  { name: 'trends', path: '/trends' },
]

for (const { name, path } of SHOTS) {
  test(`shot: ${name}`, async ({ page }, testInfo) => {
    const theme = testInfo.project.name
    await page.goto(path)
    await page.waitForLoadState('networkidle')
    await prepare(page)
    await page.screenshot({
      path: `../docs/screenshots/${name}-${theme}.png`,
      // Explicitly not fullPage — see the note above.
      fullPage: false,
    })
  })
}

// The one shot backed by real data. Everything above runs on DevSeed's synthetic
// fixture, but the seed deliberately clears ActivityTracks (backend/DevSeed.cs), so
// its Ultimate games have no GPS stream and the field timeline — the most distinctive
// thing the app does — renders nothing at all.
//
// Rather than invent a fake track, this pushes one of the real game tracks already
// committed under tests/Callahan.Api.Tests/Fixtures/ onto a seeded game via the same
// PUT the Garmin sync uses. The strip that comes out is FieldGeometry.Analyse's actual
// output on actual GPS, not a mock of it.
//
// Only the *track* is real. The seeded game's date, opponent and summary counters stay
// synthetic, and since the seed has no laps the lap-derived aggregates on that page
// would not correspond to this track — so this captures the timeline element alone,
// never the surrounding page, to avoid framing synthetic numbers as real ones.
import { readFileSync } from 'node:fs'
import { gunzipSync } from 'node:zlib'

const FIXTURE = '../tests/Callahan.Api.Tests/Fixtures/game-16.json.gz'
const API = process.env.PLAYWRIGHT_API_BASE ?? 'http://localhost:8099'

test('shot: field-timeline (real GPS track)', async ({ page, request }) => {
  const { token } = await (await request.post(`${API}/api/auth/dev-login`)).json()
  const auth = { Authorization: `Bearer ${token}` }

  const activities = await (await request.get(`${API}/api/activities`, { headers: auth })).json()
  const game = (Array.isArray(activities) ? activities : activities.items).find(
    (a) => a.type === 'Ultimate' && a.activitySessionTypeName === 'Game',
  )
  if (!game) throw new Error('no seeded Ultimate "Game" activity to attach a track to')

  const fixture = JSON.parse(gunzipSync(readFileSync(FIXTURE)).toString())
  const put = await request.put(`${API}/api/activities/${game.id}/track`, {
    headers: auth,
    data: {
      startEpochMs: fixture.startEpochMs,
      sampleCount: fixture.sampleCount,
      medianSpacingSec: fixture.medianSpacingSec,
      samples: fixture.samples,
    },
  })
  if (!put.ok()) throw new Error(`track PUT failed (${put.status()}): ${await put.text()}`)

  // Its own wide, 2x context rather than the project's 440px phone viewport: this is
  // a diagram, not a phone-fidelity shot, and at 440px the strip's 10-minute tick
  // labels collide and clip at both ends.
  const ctx = await page.context().browser().newContext({
    viewport: { width: 1000, height: 700 },
    deviceScaleFactor: 2,
    colorScheme: 'dark',
    storageState: 'e2e/.auth/user.json',
  })
  const wide = await ctx.newPage()
  await wide.goto(`${process.env.PLAYWRIGHT_APP_BASE ?? 'http://localhost:5183'}/activities/${game.id}`)
  await wide.waitForLoadState('networkidle')
  // .bottom-tab-bar is position:fixed, so it paints over the timeline's tick labels
  // and legend and lands inside an element-scoped screenshot of that region. Element
  // capture clips to the element's box, not to what is logically inside it.
  await wide.addStyleTag({ content: '.bottom-tab-bar { display: none !important }' })
  const timeline = wide.locator('.field-timeline')
  await timeline.waitFor({ state: 'visible' })
  await timeline.screenshot({ path: '../docs/screenshots/field-timeline-dark.png' })
  await ctx.close()
})
