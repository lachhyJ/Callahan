import { test, expect } from '@playwright/test'

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
    await expect(page).toHaveScreenshot(`${name}.png`, {
      fullPage: true,
      // The TopBar's build stamp (App.jsx's .build-tag) bakes in the worktree name and
      // commit hash — visible on every authenticated screen and different on every
      // commit, which would invalidate every baseline regardless of any real UI change.
      // Masked, not asserted on: this suite verifies layout/styling, not that stamp.
      mask: [page.locator('.build-tag')],
    })
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
