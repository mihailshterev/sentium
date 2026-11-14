import { test, expect } from "../fixtures";

test.describe("Settings", () => {
  test.beforeEach(async ({ settingsPage }) => {
    await settingsPage.goto();
    await settingsPage.expectLoaded();
  });

  test("renders the Settings heading", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByRole("heading", { level: 1, name: "Settings" })).toBeVisible();
  });

  test("shows the agent harness settings", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByRole("switch", { name: "Built-in governance harness" })).toBeVisible();
    await expect(page.getByRole("switch", { name: "Prompt enhancement" })).toBeVisible();
  });

  test("shows the global prompt textarea", async ({ page }) => {
    await expect(page.getByLabel("Global prompt", { exact: true })).toBeVisible();
  });

  test("shows the save bar only after a change", async ({ settingsPage, page }) => {
    await expect(page.getByRole("button", { name: /save changes/i })).toHaveCount(0);
    await settingsPage.togglePromptEnhancement();
    await settingsPage.expectSaveButtonVisible();
  });

  test("can fill the user harness prompt", { tag: "@regression" }, async ({ settingsPage, page }) => {
    await settingsPage.fillUserHarnessPrompt("E2E test prompt content");
    await expect(page.getByLabel("Global prompt", { exact: true })).toHaveValue("E2E test prompt content");
  });

  test("saves settings and shows success feedback", { tag: "@regression" }, async ({ settingsPage }) => {
    await settingsPage.fillUserHarnessPrompt(`E2E regression prompt ${Date.now()}`);
    await settingsPage.saveSettings();
    await settingsPage.expectSuccessFeedback();
  });

  test("filters settings by search", { tag: "@regression" }, async ({ settingsPage, page }) => {
    await settingsPage.search("timeout");
    await expect(page.getByLabel("Call timeout", { exact: true })).toBeVisible();
    await expect(page.getByLabel("Global prompt", { exact: true })).toHaveCount(0);
  });

  test("shows the Sentinel policy settings", { tag: "@regression" }, async ({ settingsPage, page }) => {
    await settingsPage.goto("security");
    const security = settingsPage.section("Security");
    await expect(security).toBeVisible();
    await expect(page.getByTestId("semantic-intent-toggle")).toBeEnabled();
    await expect(page.getByTestId("autonomy-slider")).toBeEnabled();
  });

  test("shows the MCP and monitoring settings", { tag: "@regression" }, async ({ settingsPage }) => {
    await expect(settingsPage.section("MCP")).toBeVisible();
    await expect(settingsPage.section("Monitoring")).toBeVisible();
  });
});
