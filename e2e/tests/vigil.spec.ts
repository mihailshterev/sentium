import { test, expect } from "../fixtures";

test.describe("Vigil", () => {
  test.beforeEach(async ({ vigilPage }) => {
    await vigilPage.goto();
    await vigilPage.expectLoaded();
  });

  test("renders the Vigil heading", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByRole("heading", { name: "Vigil" })).toBeVisible();
  });

  test("shows the Services table", { tag: "@smoke" }, async ({ vigilPage }) => {
    await vigilPage.expectServicesTableVisible();
  });

  test("shows the Infrastructure table", { tag: "@smoke" }, async ({ vigilPage }) => {
    await vigilPage.expectInfraTableVisible();
  });

  test("shows summary stats", { tag: "@regression" }, async ({ vigilPage }) => {
    await vigilPage.expectSummaryStatsVisible();
  });

  test("shows Host Metrics section", { tag: "@regression" }, async ({ vigilPage }) => {
    await vigilPage.expectHostMetricsVisible();
  });

  test("shows the Refresh button", async ({ page }) => {
    await expect(page.getByRole("button", { name: /refresh/i })).toBeVisible();
  });

  test("shows Incidents section", async ({ page }) => {
    await expect(page.getByText("Incidents", { exact: true })).toBeVisible();
  });
});
