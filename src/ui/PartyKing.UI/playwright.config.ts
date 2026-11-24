import { defineConfig, devices } from '@playwright/test';
import { API_URL, IDENTITY_URL, UI_URL } from './e2e/constants';

export default defineConfig({
  testDir: './e2e',
  testMatch: '**/*.spec.ts',
  // The demo server has its own suite and config
  testIgnore: '**/demo/**',
  globalSetup: './e2e/global-setup.ts',
  // A game with ten players takes about 30 s on a CI runner, which is the default, so a test gets twice that
  timeout: 60_000,
  // One API and one database for the whole run; the tests share its state, so they run one at a time
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: UI_URL,
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    {
      // The accounts live in the identity service; this stands in for it (the API calls it, and so does the UI)
      command: 'node e2e/fixtures/fake-identity/server.mjs',
      url: `${IDENTITY_URL}/health`,
      reuseExistingServer: false,
      timeout: 30_000,
      stdout: 'pipe',
      stderr: 'pipe',
    },
    {
      command: 'node e2e/start-api.mjs',
      url: `${API_URL}/health`,
      // A running dev API has other settings (admin, rate limits), so never reuse one
      reuseExistingServer: false,
      timeout: 120_000,
      stdout: 'pipe',
      stderr: 'pipe',
    },
    {
      command: 'npm start',
      url: UI_URL,
      env: { VITE_IDENTITY_URL: IDENTITY_URL },
      reuseExistingServer: false,
      timeout: 60_000,
    },
  ],
});
