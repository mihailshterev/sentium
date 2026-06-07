import { test, expect } from "../fixtures/auth.fixture";

test.describe.serial("Watchdog", () => {
  test.beforeEach(async ({ watchdogPage }) => {
    await watchdogPage.goto();
    await watchdogPage.expectLoaded();
  });

  test("renders the Watchdog heading", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByRole("heading", { name: "Watchdog" })).toBeVisible();
  });

  test("shows the Services table", { tag: "@smoke" }, async ({ watchdogPage }) => {
    await watchdogPage.expectServicesTableVisible();
  });

  test("shows the Infrastructure table", { tag: "@smoke" }, async ({ watchdogPage }) => {
    await watchdogPage.expectInfraTableVisible();
  });

  test("shows summary stats", { tag: "@regression" }, async ({ watchdogPage }) => {
    await watchdogPage.expectSummaryStatsVisible();
  });

  test("shows Host Metrics section", { tag: "@regression" }, async ({ watchdogPage }) => {
    await watchdogPage.expectHostMetricsVisible();
  });

  test("shows the Refresh button", async ({ page }) => {
    await expect(page.getByRole("button", { name: /refresh/i })).toBeVisible();
  });

  test("shows Incidents section", async ({ page }) => {
    await expect(page.getByText("Incidents", { exact: true })).toBeVisible();
  });
});
