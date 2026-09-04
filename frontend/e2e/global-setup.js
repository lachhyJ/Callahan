// Resets the backend to a fixed synthetic fixture (see backend/DevSeed.cs — fake
// workout/run/wellness history, never real data) and logs in via the dev-login
// endpoint (see README's "Verifying UI changes without a password"), saving the
// resulting token as Playwright storage state. Every spec then starts already
// authenticated against identical, deterministic data — no password, no UI login
// flow, and no baseline drift from run to run.
import { chromium, request } from '@playwright/test'

const API_BASE = process.env.PLAYWRIGHT_API_BASE ?? 'http://localhost:8099'
const APP_BASE = process.env.PLAYWRIGHT_APP_BASE ?? 'http://localhost:5183'
const STORAGE_STATE_PATH = 'e2e/.auth/user.json'

async function devLoginWithRetry(api, attempts = 20, delayMs = 1500) {
  // Playwright doesn't document globalSetup running strictly after webServer becomes
  // ready, so poll rather than assume the backend is already up.
  let lastError
  for (let i = 0; i < attempts; i++) {
    try {
      const res = await api.post(`${API_BASE}/api/auth/dev-login`)
      if (res.ok()) return res.json()
      lastError = new Error(`dev-login returned ${res.status()}`)
    } catch (err) {
      lastError = err
    }
    await new Promise((r) => setTimeout(r, delayMs))
  }
  throw new Error(
    `dev-login never succeeded after ${attempts} attempts: ${lastError}. Is the ` +
      `backend running with ASPNETCORE_ENVIRONMENT=Development and ` +
      `Auth__AllowDevLogin=true?`,
  )
}

export default async function globalSetup() {
  const api = await request.newContext()
  const { token } = await devLoginWithRetry(api)
  const seedRes = await api.post(`${API_BASE}/api/dev/seed`)
  if (!seedRes.ok()) {
    throw new Error(`/api/dev/seed failed (${seedRes.status()})`)
  }
  await api.dispose()

  const browser = await chromium.launch()
  const page = await browser.newPage()
  // Navigate first so localStorage attaches to the app's origin.
  await page.goto(`${APP_BASE}/login`)
  await page.evaluate((t) => localStorage.setItem('callahan_token', t), token)
  await page.context().storageState({ path: STORAGE_STATE_PATH })
  await browser.close()
}
