import { defineConfig, devices, type PlaywrightTestConfig } from "@playwright/test";
import { appPort, baseURL, externalBaseURL, isCI, ollamaStubPort, paths, skipBuild } from "./support/env";

const buildCommands = skipBuild
  ? []
  : [
      `pnpm --dir "${paths.portal}" exec vite build --logLevel warn`,
      `dotnet build "${paths.webProject}" -c Release --nologo -v quiet`,
    ];

const appCommand = [
  `docker compose -f "${paths.composeFile}" up -d --wait`,
  ...buildCommands,
  [
    `dotnet run --project "${paths.webProject}" -c Release --no-build --no-launch-profile`,
    `-- --environment Testing --urls http://localhost:${appPort}`,
  ].join(" "),
].join(" && ");

const webServer: PlaywrightTestConfig["webServer"] = externalBaseURL
  ? undefined
  : [
      {
        name: "ollama-stub",
        command: `node support/ollama-stub.ts ${ollamaStubPort}`,
        url: `http://127.0.0.1:${ollamaStubPort}/api/version`,
        reuseExistingServer: !isCI,
        timeout: 10_000,
      },
      {
        name: "sentium-web",
        command: appCommand,
        url: `http://localhost:${appPort}/alive`,
        reuseExistingServer: !isCI,
        timeout: (skipBuild ? 2 : 6) * 60_000,
        stdout: "pipe",
        stderr: "pipe",
        env: {
          ASPNETCORE_ENVIRONMENT: "Testing",
          DOTNET_ENVIRONMENT: "Testing",
        },
      },
    ];

export default defineConfig({
  testDir: "./tests",
  outputDir: "./test-results",
  fullyParallel: true,
  forbidOnly: isCI,
  retries: isCI ? 2 : 0,
  workers: isCI ? 2 : undefined,
  timeout: 30_000,
  expect: { timeout: 10_000 },
  reporter: isCI
    ? [["github"], ["list"], ["html", { open: "never" }], ["junit", { outputFile: "test-results/junit.xml" }]]
    : [["list"], ["html", { open: "on-failure" }]],

  use: {
    baseURL,
    actionTimeout: 10_000,
    navigationTimeout: 15_000,
    trace: isCI ? "on-first-retry" : "retain-on-failure",
    screenshot: "only-on-failure",
    video: isCI ? "on-first-retry" : "off",
    contextOptions: { reducedMotion: "reduce" },
  },

  projects: [
    {
      name: "setup",
      testMatch: /.*\.setup\.ts/,
    },
    {
      name: "chromium",
      dependencies: ["setup"],
      use: { ...devices["Desktop Chrome"], storageState: paths.authState },
    },
    {
      name: "firefox",
      dependencies: ["setup"],
      use: { ...devices["Desktop Firefox"], storageState: paths.authState },
    },
    {
      name: "webkit",
      dependencies: ["setup"],
      use: { ...devices["Desktop Safari"], storageState: paths.authState },
    },
  ],

  webServer,
});
