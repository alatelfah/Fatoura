import { defineConfig, devices } from '@playwright/test';

const apiPort = 5081;
const webPort = 5174;
const database =
  process.env.FATOURA_E2E_DB ??
  'Server=localhost,1433;Database=Fatoura_E2E;User Id=sa;Password=Fatoura_Dev_2026!;TrustServerCertificate=True';

/**
 * End-to-end tests drive the real stack: the .NET API (Development settings, seeded admin, its own database)
 * and the Vite dev server proxying /api. Tests create uniquely named data, so the database need not be reset.
 */
export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: `http://localhost:${webPort}`,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    launchOptions: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE ? { executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE } : {},
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'], viewport: { width: 1400, height: 900 } } }],
  webServer: [
    {
      command: 'dotnet run --project ../../backend/src/Fatoura.Api --no-launch-profile',
      url: `http://localhost:${apiPort}/api/health`,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        ASPNETCORE_URLS: `http://localhost:${apiPort}`,
        ConnectionStrings__Fatoura: database,
      },
      reuseExistingServer: !process.env.CI,
      timeout: 240_000,
      stdout: 'ignore',
    },
    {
      command: `npx vite --port ${webPort} --strictPort`,
      url: `http://localhost:${webPort}`,
      env: { FATOURA_API: `http://localhost:${apiPort}` },
      reuseExistingServer: !process.env.CI,
      timeout: 60_000,
    },
  ],
});
