import { defineConfig, devices } from "@playwright/test";

const baseURL = process.env.PLAYWRIGHT_BASE_URL ?? "http://localhost:3100";
const apiURL = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5100";
const external = process.env.PLAYWRIGHT_EXTERNAL === "1";
const ci = Boolean(process.env.CI);
const webPort = new URL(baseURL).port;
// CI exercises the production build the way Dockerfile.prod serves it: the
// standalone server with public/ and .next/static beside it. Local runs keep
// the faster dev server.
const webCommand = ci
  ? [
      "NEXT_BUILD=1 bunx next build",
      "rm -rf .next/standalone/public .next/standalone/.next/static",
      "cp -r public .next/standalone/public",
      "cp -r .next/static .next/standalone/.next/static",
      `PORT=${webPort} HOSTNAME=localhost bun .next/standalone/server.js`,
    ].join(" && ")
  : `bun --bun next dev --hostname localhost --port ${webPort}`;

export default defineConfig({
  testDir: "./e2e",
  testMatch: "**/*.spec.ts",
  fullyParallel: false,
  workers: 1,
  timeout: 45_000,
  retries: ci ? 1 : 0,
  reporter: [["list"], ["html", { open: "never" }]],
  use: {
    baseURL,
    trace: "on-first-retry",
    screenshot: "only-on-failure",
    timezoneId: "UTC",
  },
  webServer: external
    ? undefined
    : [
        {
          command: "bun e2e/fixture-api.ts",
          url: `${apiURL}/health`,
          reuseExistingServer: !ci,
          env: {
            PLAYWRIGHT_BASE_URL: baseURL,
            PLAYWRIGHT_API_PORT: new URL(apiURL).port,
          },
        },
        {
          command: webCommand,
          url: `${baseURL}/api/health`,
          reuseExistingServer: !ci,
          timeout: ci ? 300_000 : 120_000,
          env: {
            API_URL: apiURL,
            NEXT_PUBLIC_API_URL: apiURL,
            NEXT_PUBLIC_APP_URL: baseURL,
            // The Mini App's session route forwards to the bot; the fixture plays it.
            TELEGRAM_BOT_URL: apiURL,
            NEXT_TELEMETRY_DISABLED: "1",
          },
        },
      ],
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
});
