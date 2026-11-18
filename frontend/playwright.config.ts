import { resolve } from 'node:path'
import { defineConfig, devices } from '@playwright/test'

const frontendDirectory = process.cwd()
const repositoryDirectory = resolve(frontendDirectory, '..')
const reuseApiServer =
  process.env.E2E_API_ALREADY_RUNNING === 'true' || !process.env.CI

// The same-origin Vite proxy exercises the actual React app and API together.
export default defineConfig({
  testDir: './e2e',
  testMatch: '**/*.e2e.ts',
  fullyParallel: false,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  timeout: 90_000,
  expect: { timeout: 15_000 },
  reporter: process.env.CI
    ? [['list'], ['html', { outputFolder: 'playwright-report', open: 'never' }]]
    : 'list',
  outputDir: 'test-results',
  use: {
    baseURL: process.env.E2E_BASE_URL || 'http://127.0.0.1:5173',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    ...devices['Desktop Chrome'],
  },
  webServer: [
    {
      command:
        'dotnet run --project src/InterviewHub.Api/InterviewHub.Api.csproj --configuration Release --no-build --urls http://127.0.0.1:5199',
      cwd: repositoryDirectory,
      url: 'http://127.0.0.1:5199/health',
      reuseExistingServer: reuseApiServer,
      timeout: 180_000,
      stdout: 'pipe',
      stderr: 'pipe',
    },
    {
      // Bind IPv4 explicitly so Playwright's 127.0.0.1 readiness probe reaches Vite.
      command: 'pnpm exec vite --host 127.0.0.1',
      cwd: frontendDirectory,
      url: 'http://127.0.0.1:5173',
      reuseExistingServer: !process.env.CI,
      timeout: 90_000,
      stdout: 'pipe',
      stderr: 'pipe',
    },
  ],
})
