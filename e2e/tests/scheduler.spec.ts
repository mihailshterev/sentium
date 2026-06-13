import { test, expect } from "../fixtures/auth.fixture";

test.describe.serial("Automated Scheduler", () => {
  test.beforeEach(async ({ schedulerPage }) => {
    await schedulerPage.goto();
    await schedulerPage.expectLoaded();
  });

  test("renders the Automated Scheduler heading", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByRole("heading", { name: "Automated Scheduler" })).toBeVisible();
  });

  test("shows the engine pipeline status card", { tag: "@smoke" }, async ({ schedulerPage }) => {
    await schedulerPage.expectEngineStatusVisible();
  });

  test("shows the active core loops card", { tag: "@smoke" }, async ({ schedulerPage }) => {
    await schedulerPage.expectJobCountVisible();
  });

  test("shows empty state when no cron jobs exist", { tag: "@regression" }, async ({ schedulerPage }) => {
    await schedulerPage.expectEmptyState();
  });

  test("shows the Refresh Engine button", async ({ page }) => {
    await expect(page.getByRole("button", { name: /refresh engine/i })).toBeVisible();
  });

  test("shows the Engine Notice card", async ({ page }) => {
    await expect(page.getByText(/engine notice/i)).toBeVisible();
  });
});
