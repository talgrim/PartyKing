import { defineConfig, devices } from '@playwright/test';
import { DEMO_API_URL, DEMO_UI_URL } from './e2e/constants';

// The demo server: the same API with `DemoSettings__Enabled=true` (see e2e/start-api.mjs) and a UI that talks to it.
// It has its own ports and database, so it runs after the ordinary suite (`npm run e2e` runs both) and shares nothing.
export default defineConfig({
  testDir: './e2e/demo',
  testMatch: '**/*.spec.ts',
  timeout: 60_000,
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never', outputFolder: 'playwright-report-demo' }]] : 'list',
  use: {
    baseURL: DEMO_UI_URL,
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    {
      command: 'node e2e/start-api.mjs',
      url: `${DEMO_API_URL}/health`,
      env: { E2E_DEMO: '1' },
      reuseExistingServer: false,
      timeout: 120_000,
      stdout: 'pipe',
      stderr: 'pipe',
    },
    {
      command: 'npm start -- --port 2237 --strictPort',
      url: DEMO_UI_URL,
      env: { VITE_API_URL: DEMO_API_URL },
      reuseExistingServer: false,
      timeout: 60_000,
    },
  ],
});
