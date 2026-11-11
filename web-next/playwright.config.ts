import { defineConfig, devices } from '@playwright/test'
export default defineConfig({
  testDir: './e2e',
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  timeout: 45_000,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report' }]],
  use: { baseURL: 'http://127.0.0.1:3000', trace: 'retain-on-failure' },
  projects: [
    { name: 'setup', testMatch: /auth\.setup\.ts/ },
    { name: 'chromium', use: { ...devices['Desktop Chrome'], storageState: 'e2e/.auth/member.json' },
      dependencies: ['setup'], testIgnore: /auth\.setup\.ts/ },
  ],
  webServer: { command: 'npm run start -- -p 3000', url: 'http://127.0.0.1:3000',
    timeout: 120_000, reuseExistingServer: !process.env.CI },
})
