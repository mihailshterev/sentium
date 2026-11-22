import { test, expect } from "../fixtures";

test.describe("Dashboard", () => {
  test.beforeEach(async ({ dashboardPage }) => {
    await dashboardPage.goto();
    await dashboardPage.expectLoaded();
  });

  test("renders the page heading and greeting", async ({ page }) => {
    await expect(page.getByRole("heading", { name: "Home", exact: true })).toBeVisible();
    await expect(page.getByText(/^good (morning|afternoon|evening)/i)).toBeVisible();
  });

  test("displays the system status strip", async ({ page }) => {
    await expect(page.getByRole("main").getByRole("status")).toHaveText(
      /all systems operational|operational with \d+ warning|critical issue|checking status/i,
    );
  });

  test("shows what needs attention and where the user left off", async ({ page }) => {
    const main = page.getByRole("main");
    await expect(main.getByRole("heading", { name: "Needs attention" })).toBeVisible();
    await expect(main.getByRole("heading", { name: "Continue where you left off" })).toBeVisible();
  });

  test("links the key figures to their pages", async ({ page }) => {
    const main = page.getByRole("main");
    await expect(main.getByRole("link", { name: /^agents:/i })).toHaveAttribute("href", "/agents");
    await expect(main.getByRole("link", { name: /^services:/i })).toHaveAttribute("href", "/vigil");
  });

  test("opens the command palette with the keyboard", async ({ page }) => {
    await page.keyboard.press("ControlOrMeta+k");
    const palette = page.getByRole("dialog", { name: "Command palette" });
    await expect(palette).toBeVisible();
    await page.keyboard.type("sentinel");
    await page.keyboard.press("Enter");
    await expect(page).toHaveURL(/\/sentinel/);
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
