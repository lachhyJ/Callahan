import { test, expect } from '@playwright/test'

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

// KNOWN REMAINING INSTABILITY, not fixed here: DevSeed anchors its fixture window on
// DateTime.Today, so every screen that renders an absolute date (History's "31 Aug –
// 6 Sept" week headers, the dashboard calendar, Trends' axes) shifts by a day, every
// day. Baselines regenerated today will fail tomorrow on those screens — though not
// on ones with no dates in them, like Streaks.
//
// Deliberately left alone because every fix has a real cost: anchoring the seed to a
// fixed date makes local dev data permanently stale-looking (the app's recent-window
// views would render empty, which is the reason it tracks today in the first place),
// and hiding the dates instead would drop them out of coverage. Freezing the browser
// clock does not help — the dates come from server-side seeded data, not the client.
// So for now: if a date-bearing baseline fails and the diff is only shifted dates,
// that is this, not a regression.

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
  { name: 'games', path: '/games' },
  { name: 'streaks', path: '/streaks' },
  { name: 'reports', path: '/reports' },
  { name: 'plate-calculator', path: '/plate-calculator' },
]

for (const { name, path } of SCREENS) {
  test(`${name} screen`, async ({ page }) => {
    await page.goto(path)
    // Let route-level data fetches settle before the shot; these are read screens,
    // not the mid-input states the "no live-preview" learned constraint is about.
    await page.waitForLoadState('networkidle')
    await hideBuildTag(page)
    await expect(page).toHaveScreenshot(`${name}.png`, { fullPage: true })
  })
}

test.describe('logged out', () => {
  // Overrides only storageState — viewport/colorScheme still come from the running
  // project (mobile-light / mobile-dark), unlike a manually-constructed
  // browser.newContext(), which would silently drop back to Playwright's defaults.
  test.use({ storageState: { cookies: [], origins: [] } })

  test('login screen', async ({ page }) => {
    await page.goto('/login')
    await expect(page).toHaveScreenshot('login.png', { fullPage: true })
  })
})
