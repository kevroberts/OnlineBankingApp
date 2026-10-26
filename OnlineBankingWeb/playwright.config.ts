import { defineConfig, devices } from "@playwright/test";

/**
 * End-to-end tests run a real browser against the production build (`npm run build` first) and the
 * real API with its seeded data. Start the API's https profile before running `npm run test:e2e`.
 */
const baseURL = process.env.E2E_BASE_URL ?? "http://localhost:3000";

export default defineConfig({
  testDir: "./tests/e2e",
  // The tests share one seeded database, so they run one at a time.
  workers: 1,
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: [["list"], ["html", { open: "never" }]],
  globalSetup: "./tests/e2e/global-setup.ts",
  use: {
    baseURL,
    trace: "retain-on-failure",
    // Chromium accepts the API's Secure cookie on http://localhost.
    ...(process.env.PW_CHROMIUM_PATH
      ? { launchOptions: { executablePath: process.env.PW_CHROMIUM_PATH } }
      : {}),
  },
  projects: [
    { name: "setup", testMatch: /auth\.setup\.ts/ },
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"], storageState: "tests/e2e/.auth/alice.json" },
      dependencies: ["setup"],
    },
  ],
  webServer: {
    command: "npm start",
    url: baseURL,
    reuseExistingServer: true,
    timeout: 60_000,
  },
});
