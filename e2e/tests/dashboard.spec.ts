import { test, expect } from "../fixtures/auth.fixture";

test.describe.serial("Dashboard", () => {
  test.beforeEach(async ({ dashboardPage }) => {
    await dashboardPage.goto();
    await dashboardPage.expectLoaded();
  });

  test("renders the page heading", async ({ page }) => {
    await expect(page.getByRole("heading", { name: "Dashboard" })).toBeVisible();
    await expect(page.getByText("System overview and quick access")).toBeVisible();
  });

  test("displays the system status badge", async ({ page }) => {
    await expect(page.getByText(/all systems operational/i)).toBeVisible();
  });

  test("shows stat cards section", async ({ page }) => {
    await expect(page.getByText(/agent/i).first()).toBeVisible();
    await expect(page.getByText(/workflow/i).first()).toBeVisible();
  });

  test("shows Quick Access section", async ({ page }) => {
    await expect(page.getByText("Quick Access", { exact: true })).toBeVisible();
  });

  test("shows System Modules section", async ({ page }) => {
    await expect(page.getByText(/system modules/i)).toBeVisible();
  });

  test("shows Recent Activity section", async ({ page }) => {
    await expect(page.getByText(/recent activity/i)).toBeVisible();
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
