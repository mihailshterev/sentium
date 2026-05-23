import { defineConfig, devices } from "@playwright/test";
import path from "path";

const AUTH_FILE = path.join("playwright", ".auth", "user.json");

export default defineConfig({
  testDir: "./tests",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: process.env.CI ? [["html", { open: "never" }], ["github"]] : [["html", { open: "on-failure" }]],
  globalSetup: "./global-setup.ts",

  use: {
    baseURL: "http://localhost:5173",
    storageState: AUTH_FILE,
    trace: "on-first-retry",
    screenshot: "only-on-failure",
    video: "on-first-retry",
    headless: true,
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
  },

  projects: [
    {
      name: "auth-setup",
      testMatch: /auth\.spec\.ts/,
      use: {
        ...devices["Desktop Chrome"],
        storageState: undefined,
      },
    },
    {
      name: "chromium",
      testIgnore: /auth\.spec\.ts/,
      use: {
        ...devices["Desktop Chrome"],
        storageState: AUTH_FILE,
      },
    },
    {
      name: "firefox",
      testIgnore: /auth\.spec\.ts/,
      use: {
        ...devices["Desktop Firefox"],
        storageState: AUTH_FILE,
      },
    },
    {
      name: "webkit",
      testIgnore: /auth\.spec\.ts/,
      use: {
        ...devices["Desktop Safari"],
        storageState: AUTH_FILE,
      },
    },
  ],

  webServer: {
    command: "dotnet run --project ../src/aspire/Sentium.AppHost/Sentium.AppHost.csproj",
    url: "http://localhost:5173",
    reuseExistingServer: !process.env.CI,
    timeout: 300 * 1000,
    env: {
      DOTNET_ASPIRE_SHOW_DASHBOARD: "false",
      ASPNETCORE_ENVIRONMENT: "Testing",
    },
    stdout: "pipe",
    stderr: "pipe",
  },
});
