import { test, expect } from "../fixtures/auth.fixture";

test.describe.serial("Settings", () => {
  test.beforeEach(async ({ settingsPage }) => {
    await settingsPage.goto();
    await settingsPage.expectLoaded();
  });

  test("renders the Settings heading", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByRole("heading", { name: "Settings" })).toBeVisible();
  });

  test("shows the agent harness section", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByText("Built-in Governance Harness")).toBeVisible();
  });

  test("shows the prompt enhancement section", async ({ page }) => {
    await expect(page.getByText("Prompt Enhancement", { exact: true })).toBeVisible();
  });

  test("shows the global prompt textarea", async ({ page }) => {
    await expect(page.locator("textarea")).toBeVisible();
  });

  test("shows the save button", async ({ settingsPage }) => {
    await settingsPage.expectSaveButtonVisible();
  });

  test("can fill the user harness prompt", { tag: "@regression" }, async ({ settingsPage, page }) => {
    await settingsPage.fillUserHarnessPrompt("E2E test prompt content");
    await expect(page.locator("textarea")).toHaveValue("E2E test prompt content");
  });

  test("saves settings and shows success feedback", { tag: "@regression" }, async ({ settingsPage }) => {
    await settingsPage.fillUserHarnessPrompt(`E2E regression prompt ${Date.now()}`);
    await settingsPage.saveSettings();
    await settingsPage.expectSuccessFeedback();
  });
});
