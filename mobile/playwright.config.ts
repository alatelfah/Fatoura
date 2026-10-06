import { defineConfig, devices } from '@playwright/test';

const database =
  process.env.FATOURA_E2E_DB ??
  'Server=localhost,1433;Database=Fatoura_E2E;User Id=sa;Password=Fatoura_Dev_2026!;TrustServerCertificate=True';

/** Smoke test of the Expo web build (run `npm run export:web` first) against the real API. */
export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  workers: 1,
  reporter: 'list',
  use: {
    baseURL: 'http://localhost:8090',
    trace: 'retain-on-failure',
    launchOptions: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE ? { executablePath: process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE } : {},
  },
  projects: [{ name: 'mobile-web', use: { ...devices['Pixel 7'], browserName: 'chromium' } }],
  webServer: [
    {
      command: 'dotnet run --project ../backend/src/Fatoura.Api --no-launch-profile',
      url: 'http://localhost:5082/api/health',
      env: { ASPNETCORE_ENVIRONMENT: 'Development', ASPNETCORE_URLS: 'http://localhost:5082', ConnectionStrings__Fatoura: database },
      reuseExistingServer: !process.env.CI,
      timeout: 240_000,
      stdout: 'ignore',
    },
    { command: 'node e2e/serve.mjs', url: 'http://localhost:8090', reuseExistingServer: !process.env.CI },
  ],
});
