import { test, expect } from '@playwright/test'
import { pinClock } from './anchor.js'

// The TopBar's build stamp (App.jsx's .build-tag) is built from the worktree name,
// branch and commit, so its *width* changes with all three — "main@bd62bcd" against
// "Callahan-playwright · playwright@3a82724" is a threefold difference.
//
// Playwright's `mask` option does not solve this, which is what it was originally
// used for here. Masking paints a filled box the size of the element's bounding
// rect, so a different-width tag simply produces a different-width box and the mask
// itself becomes the diff — every authenticated screen failed on every branch other
// than the one the baselines were captured on, which is every branch eventually.
//
// `display: none` rather than `visibility: hidden`: .top-bar-right is a flex row, so
// a hidden-but-present tag still displaces "Log out" by its own variable width.
// Removing it from layout entirely is the only form that is stable across branches.
async function hideBuildTag(page) {
  await page.addStyleTag({ content: '.build-tag { display: none !important }' })
}

// Dates: the backend clock, DevSeed's fixture and the browser clock are all pinned
// to one anchor date (e2e/anchor.js), so date-bearing screens render the same on
// every run. Pinning only one side can't work — the dashboard and history derive
// "this week" from the browser clock, while the data and the streak/trends windows
// come from the server's. Local dev is unaffected: Dev__FixedNow is only set here.

// One screenshot per screen, at whatever theme the project ("mobile-light" /
// "mobile-dark") is running. Routes are the ones with real content on a normal
// account — pages needing a specific in-progress state (ActiveWorkoutPage,
// WorkoutSessionDetailPage, etc.) aren't included since there's no fixture for
// "a workout is currently running."
const SCREENS = [
  { name: 'templates-home', path: '/' },
  { name: 'dashboard', path: '/dashboard' },
  { name: 'history', path: '/history' },
  { name: 'trends', path: '/trends' },
  { name: 'program', path: '/program' },
  { name: 'wellness', path: '/wellness' },
  { name: 'exercises', path: '/exercises' },
  { name: 'games', path: '/ultimate' },
  { name: 'reports', path: '/reports' },
]

for (const { name, path } of SCREENS) {
  test(`${name} screen`, async ({ page }) => {
    await pinClock(page)
    await page.goto(path)
    // Let route-level data fetches settle before the shot; these are read screens,
    // not the mid-input states the "no live-preview" learned constraint is about.
    await page.waitForLoadState('networkidle')
    await hideBuildTag(page)
    // Viewport shot, not fullPage: the app shell is now fixed-height with an
    // inner scroll container (#app-scroll), so the document doesn't scroll and
    // fullPage would just be the viewport anyway. The top of each screen is the
    // stable part worth guarding.
    //
    // Below-the-fold coverage for Trends and the report detail route lives in
    // the SCROLLED screens further down; other screens' lower sections are still
    // uncovered, so treat DOM + geometry checks as the record for those.
    await expect(page).toHaveScreenshot(`${name}.png`)
  })
}

// The first-viewport shots above stop at the fold, so the lower parts of long screens
// (Trends is several viewports tall; a report is a stack of sections) had no baseline.
// These step #app-scroll down one viewport at a time and shoot each stop. Stops are
// numbered, so a screen that grows or shrinks adds/removes a shot rather than
// silently moving content out of coverage.
const SCROLLED = [
  { name: 'trends-scrolled', path: '/trends' },
  { name: 'wellness-scrolled', path: '/wellness' },
  { name: 'dashboard-scrolled', path: '/dashboard' },
  { name: 'history-scrolled', path: '/history' },
  // Last full month before the pinned anchor (2026-09-08), so it is a settled report.
  { name: 'report-detail', path: '/reports/2026/8' },
]

for (const { name, path } of SCROLLED) {
  test(`${name} screen`, async ({ page }) => {
    await pinClock(page)
    await page.goto(path)
    await page.waitForLoadState('networkidle')
    await hideBuildTag(page)
    const scroller = page.locator('#app-scroll')
    const { client, total } = await scroller.evaluate((el) => ({ client: el.clientHeight, total: el.scrollHeight }))
    // Overlap each step slightly so a section straddling a boundary is seen whole in one of the two.
    const step = client - 120
    const stops = Math.max(1, Math.ceil((total - client) / step) + 1)
    for (let i = 0; i < stops; i++) {
      await scroller.evaluate((el, top) => { el.scrollTo({ top, behavior: 'instant' }) }, i * step)
      await page.waitForTimeout(150) // let lazy/animated charts settle after the jump
      await expect(page).toHaveScreenshot(`${name}-${i + 1}.png`)
    }
  })
}

test.describe('logged out', () => {
  // Overrides only storageState — viewport/colorScheme still come from the running
  // project (mobile-light / mobile-dark), unlike a manually-constructed
  // browser.newContext(), which would silently drop back to Playwright's defaults.
  test.use({ storageState: { cookies: [], origins: [] } })

  test('login screen', async ({ page }) => {
    await pinClock(page)
    await page.goto('/login')
    await expect(page).toHaveScreenshot('login.png')
  })
})
