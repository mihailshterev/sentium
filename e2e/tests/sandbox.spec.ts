import { test, expect } from "../fixtures/auth.fixture";

test.describe.serial("Sandbox Inspector", () => {
  test.beforeEach(async ({ sandboxPage }) => {
    await sandboxPage.goto();
    await sandboxPage.expectLoaded();
  });

  test("renders the Sandbox Inspector heading", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByRole("heading", { name: "Sandbox Inspector" })).toBeVisible();
  });

  test("shows stats cards", { tag: "@smoke" }, async ({ sandboxPage }) => {
    await sandboxPage.expectStatsVisible();
  });

  test("shows the search input", { tag: "@smoke" }, async ({ sandboxPage }) => {
    await sandboxPage.expectSearchInputVisible();
  });

  test("shows status filter buttons", async ({ page }) => {
    await expect(page.getByRole("button", { name: "All", exact: true }).first()).toBeVisible();
    await expect(page.getByRole("button", { name: "Succeeded", exact: true })).toBeVisible();
    await expect(page.getByRole("button", { name: "Failed", exact: true })).toBeVisible();
    await expect(page.getByRole("button", { name: "Denied", exact: true })).toBeVisible();
  });

  test("shows language filter buttons", async ({ page }) => {
    await expect(page.getByRole("button", { name: "Python", exact: true })).toBeVisible();
    await expect(page.getByRole("button", { name: "Node", exact: true })).toBeVisible();
  });

  test("shows no executions initially (empty seeded state)", { tag: "@regression" }, async ({ sandboxPage }) => {
    await sandboxPage.expectNoExecutions();
  });

  test("can filter by Succeeded status", { tag: "@regression" }, async ({ sandboxPage }) => {
    await sandboxPage.filterByStatus("Succeeded");
    await sandboxPage.expectNoExecutions();
  });
});
