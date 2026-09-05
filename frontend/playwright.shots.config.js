// Config for the README presentation screenshots (e2e-shots/). Separate from
// playwright.config.js so the visual-regression suite's testDir, snapshot handling and
// diff thresholds are not shared with a job that only writes PNGs.
import { defineConfig, devices } from '@playwright/test'
import base from './playwright.config.js'

const PHONE = { width: 440, height: 956 }

export default defineConfig({
  ...base,
  testDir: './e2e-shots',
  reporter: [['list']],
  projects: [
    { name: 'dark', use: { ...devices['Desktop Chrome'], viewport: PHONE, colorScheme: 'dark' } },
  ],
})
