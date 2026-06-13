import { test, expect } from "../fixtures/auth.fixture";

test.describe.serial("Profile", () => {
  test.beforeEach(async ({ profilePage }) => {
    await profilePage.goto();
    await profilePage.expectLoaded();
  });

  test("shows the test user email", { tag: "@smoke" }, async ({ profilePage }) => {
    await profilePage.expectEmailVisible("a@test.test");
  });

  test("shows a role badge", { tag: "@smoke" }, async ({ profilePage }) => {
    await profilePage.expectRoleBadge("Sovereign");
  });

  test("shows the Save Changes button", async ({ page }) => {
    await expect(page.getByRole("button", { name: /save changes/i })).toBeVisible();
  });

  test("shows the first name field", async ({ page }) => {
    await expect(page.locator("#firstName")).toBeVisible();
  });

  test("shows the email field", async ({ page }) => {
    await expect(page.locator("#email")).toBeVisible();
  });

  test("updates the first name and saves", { tag: "@regression" }, async ({ profilePage }) => {
    await profilePage.fillFirstName("E2E");
    await profilePage.saveProfile();
    await profilePage.expectSuccessFeedback();
  });
});
