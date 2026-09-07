import { defineConfig, devices } from "@playwright/test";

const baseURL = process.env.PLAYWRIGHT_BASE_URL ?? "http://localhost:3100";
const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";
const external = process.env.PLAYWRIGHT_EXTERNAL === "1";

export default defineConfig({
  testDir: "./e2e",
  testMatch: "**/*.spec.ts",
  fullyParallel: false,
  workers: 1,
  timeout: 45_000,
  retries: process.env.CI ? 2 : 0,
  reporter: [["list"], ["html", { open: "never" }]],
  use: {
    baseURL,
    trace: "on-first-retry",
    screenshot: "only-on-failure",
    timezoneId: "UTC",
  },
  webServer: external ? undefined : [
    {
      command: "bun e2e/fixture-api.ts",
      url: `${apiURL}/health`,
      reuseExistingServer: !process.env.CI,
      env: { PLAYWRIGHT_BASE_URL: baseURL, PLAYWRIGHT_API_PORT: new URL(apiURL).port },
    },
    {
      command: `bun --bun next dev --hostname localhost --port ${new URL(baseURL).port}`,
      url: `${baseURL}/api/health`,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
      env: { API_URL: apiURL, NEXT_PUBLIC_API_URL: apiURL, NEXT_PUBLIC_APP_URL: baseURL, NEXT_TELEMETRY_DISABLED: "1" },
    },
  ],
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] } },
  ],
});
