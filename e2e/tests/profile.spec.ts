import { test, expect } from "../fixtures";

test.describe("Profile", () => {
  test.beforeEach(async ({ profilePage }) => {
    await profilePage.goto();
    await profilePage.expectLoaded();
  });

  test("shows the test username", { tag: "@smoke" }, async ({ profilePage }) => {
    await profilePage.expectUserNameVisible("testuser");
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

  test("shows the username field", async ({ page }) => {
    await expect(page.locator("#userName")).toBeVisible();
  });

  test("updates the first name and saves", { tag: "@regression" }, async ({ profilePage }) => {
    await profilePage.fillFirstName("E2E");
    await profilePage.saveProfile();
    await profilePage.expectSuccessFeedback();
  });
});
