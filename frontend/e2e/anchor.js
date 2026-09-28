// The one date the e2e suites run on. The backend is pinned to it through
// Dev__FixedNow (playwright.config.js), DevSeed builds its fixture around it,
// and each test pins the browser clock to it (pinClock). Without all three
// agreeing, the date-bearing screens drift every day and on every weekday.
//
// Changing it means re-baselining every screenshot once.
export const ANCHOR = '2026-09-08T10:00:00+10:00'
export const ANCHOR_DATE = '2026-09-08'

export async function pinClock(page) {
  await page.clock.setFixedTime(new Date(ANCHOR))
}
