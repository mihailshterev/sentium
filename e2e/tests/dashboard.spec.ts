import { test, expect } from "../fixtures/auth.fixture";

test.describe.serial("Dashboard", () => {
  test.beforeEach(async ({ dashboardPage }) => {
    await dashboardPage.goto();
    await dashboardPage.expectLoaded();
  });

  test("renders the page heading", async ({ page }) => {
    await expect(page.getByRole("heading", { name: "Control Center" })).toBeVisible();
    await expect(page.getByText("Real-time system monitoring and intelligence")).toBeVisible();
  });

  test("displays the system status badge", async ({ page }) => {
    await expect(page.getByText(/all systems operational|degraded services|checking status/i)).toBeVisible();
  });

  test("shows stat cards section", async ({ page }) => {
    const main = page.getByRole("main");
    await expect(main.getByText("Agents", { exact: true })).toBeVisible();
    await expect(main.getByText("Workflows", { exact: true }).first()).toBeVisible();
    await expect(main.getByText("AI Models", { exact: true })).toBeVisible();
  });

  test("navbar is visible after login", async ({ navbar }) => {
    await navbar.expectNavVisible();
  });

  test("navigates to Agents via navbar", async ({ page, navbar }) => {
    await navbar.navigateTo("Agents");
    await expect(page).toHaveURL(/\/agents/);
  });

  test("navigates to Workflows via navbar", async ({ page, navbar }) => {
    await navbar.navigateTo("Workflows");
    await expect(page).toHaveURL(/\/workflows/);
  });

  test("navigates to Assistant via navbar", async ({ page, navbar }) => {
    await navbar.navigateTo("Assistant");
    await expect(page).toHaveURL(/\/assistant/);
  });

  test("navigates to Workspaces via navbar", async ({ page, navbar }) => {
    await navbar.navigateTo("Workspaces");
    await expect(page).toHaveURL(/\/workspaces/);
  });
});
