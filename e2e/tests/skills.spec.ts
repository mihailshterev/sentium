import { test, expect } from "../fixtures/auth.fixture";

test.describe.serial("Skills", () => {
  test.beforeEach(async ({ skillsPage }) => {
    await skillsPage.goto();
    await skillsPage.expectLoaded();
  });

  test("renders the Agent Skills heading", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByRole("heading", { name: "Agent Skills" })).toBeVisible();
  });

  test("shows the Built-in tab", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByTestId("tab-builtin")).toBeVisible();
  });

  test("shows the Custom tab", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByTestId("tab-custom")).toBeVisible();
  });

  test("shows the Uploaded tab", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByTestId("tab-uploaded")).toBeVisible();
  });

  test("Built-in tab is active by default", { tag: "@regression" }, async ({ page }) => {
    await expect(page.getByTestId("tab-builtin")).toBeVisible();
    await expect(page.getByText(/built-in/i).first()).toBeVisible();
  });

  test("can switch to Custom tab", { tag: "@regression" }, async ({ skillsPage, page }) => {
    await skillsPage.switchToCustom();
    await expect(page.getByTestId("tab-custom")).toBeVisible();
  });

  test("can switch to Uploaded tab", { tag: "@regression" }, async ({ skillsPage, page }) => {
    await skillsPage.switchToUploaded();
    await expect(page.getByTestId("tab-uploaded")).toBeVisible();
  });
});
