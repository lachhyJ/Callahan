import { defineConfig, devices } from '@playwright/test'

// Visual-regression baselines. Viewport is the iPhone 17 Pro Max logical size
// (~/.claude/rules/ui-preview-verification.md) — 440x956, not Playwright's generic
// "iPhone" preset — because that's where Lachlan actually reads this app.
const PHONE = { width: 440, height: 956 }

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false, // one dev-DB-backed backend; avoid concurrent writes across tests
  forbidOnly: !!process.env.CI,
  retries: 0,
  reporter: [['html', { open: 'never' }]],
  globalSetup: './e2e/global-setup.js',

  // Deliberately off Vite's/ASP.NET's default ports (5173 / 8080). This repo has
  // several sibling worktrees (see `git worktree list`) plus unrelated projects (e.g.
  // FrisScore) that also default to 5173 — reuseExistingServer below will happily
  // treat *anyone's* server on the expected port as "ready", so a shared default port
  // silently runs the suite against the wrong app instead of failing loudly. Confirmed
  // live 2026-09-04: a stray FrisScore dev server on :5173 got screenshotted as if it
  // were Callahan.
  use: {
    baseURL: process.env.PLAYWRIGHT_APP_BASE ?? 'http://localhost:5183',
    storageState: 'e2e/.auth/user.json',
    trace: 'retain-on-failure',
  },

  expect: {
    toHaveScreenshot: {
      // A ratio tolerance scales with image size and hides small-but-real changes —
      // confirmed live 2026-09-04: 1% ratio missed a full accent-colour swap because
      // the affected element was a small fraction of a 440x956 frame. An absolute cap
      // still allows AA/font-hinting jitter (a handful to low hundreds of edge
      // pixels) without hiding a real, contained change.
      maxDiffPixels: 100,
    },
  },

  projects: [
    {
      name: 'mobile-light',
      use: { ...devices['Desktop Chrome'], viewport: PHONE, colorScheme: 'light' },
    },
    {
      name: 'mobile-dark',
      use: { ...devices['Desktop Chrome'], viewport: PHONE, colorScheme: 'dark' },
    },
  ],

  // Starts both halves of the stack if they aren't already running (e.g. from
  // .claude/launch.json in another session) — reuseExistingServer means this is safe
  // to run alongside a dev server someone already has up.
  webServer: [
    {
      // --no-launch-profile, not --launch-profile http: launchSettings.json's
      // applicationUrl (8080) otherwise wins over an ASPNETCORE_URLS env var set here
      // — confirmed live 2026-09-04, the env var was silently ignored with
      // --launch-profile. So the profile's other env vars (dev-login gate) are set
      // explicitly below instead of inherited from the profile.
      command: 'dotnet run --project backend/Callahan.Api.csproj --no-launch-profile',
      cwd: '..',
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        Auth__AllowDevLogin: 'true',
        // Off the shared default (8080) — see the port-collision note on `use` above.
        ASPNETCORE_URLS: 'http://localhost:8099',
        // The frontend below runs on a non-default origin too, so it needs an
        // explicit CORS allow — appsettings.json only allows :5173.
        Cors__AllowedOrigins__0: 'http://localhost:5183',
      },
      // /api/auth/dev-login is POST-only, and Playwright's readiness probe is a GET
      // that only accepts 200-403 as "ready" — an unauthenticated GET to any
      // [Authorize]'d route (401, in range) proves the server is up without a
      // dedicated health endpoint.
      url: 'http://localhost:8099/api/streaks',
      reuseExistingServer: true,
      timeout: 60_000,
    },
    {
      command: 'npm run dev -- --port 5183 --strictPort',
      env: { VITE_API_BASE: 'http://localhost:8099' },
      url: 'http://localhost:5183',
      reuseExistingServer: true,
      timeout: 30_000,
    },
  ],
})
