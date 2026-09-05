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
