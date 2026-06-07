import { test, expect } from "../fixtures/auth.fixture";

test.describe.serial("System", () => {
  test.beforeEach(async ({ systemPage }) => {
    await systemPage.goto();
    await systemPage.expectLoaded();
  });

  test("renders the System heading", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByRole("heading", { name: "System" })).toBeVisible();
  });

  test("shows CPU metric stat card", { tag: "@smoke" }, async ({ systemPage }) => {
    await systemPage.expectCpuMetricVisible();
  });

  test("shows Memory Usage stat card", { tag: "@smoke" }, async ({ systemPage }) => {
    await systemPage.expectMemoryMetricVisible();
  });

  test("shows System Uptime stat card", { tag: "@smoke" }, async ({ systemPage }) => {
    await systemPage.expectUptimeVisible();
  });

  test("shows all four stat cards", { tag: "@regression" }, async ({ systemPage }) => {
    await systemPage.expectStatCardsVisible();
  });

  test("shows the Refresh button", async ({ page }) => {
    await expect(page.getByRole("button", { name: /refresh/i })).toBeVisible();
  });

  test("shows Memory section details", { tag: "@regression" }, async ({ page }) => {
    await expect(page.getByText("System Memory")).toBeVisible();
  });
});
