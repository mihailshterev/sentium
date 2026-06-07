import { test, expect } from "../fixtures/auth.fixture";

test.describe.serial("User Management", () => {
  test.beforeEach(async ({ usersPage }) => {
    await usersPage.goto();
    await usersPage.expectLoaded();
  });

  test("renders the User Management heading", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByRole("heading", { name: "User Management" })).toBeVisible();
  });

  test("shows the Registered Users section", { tag: "@smoke" }, async ({ usersPage }) => {
    await usersPage.expectRegisteredUsersHeading();
  });

  test("shows the test user in the list", { tag: "@smoke" }, async ({ usersPage }) => {
    await usersPage.expectUserVisible("a@test.test");
  });

  test("shows the user table headers", async ({ page }) => {
    await expect(page.getByRole("main").getByText("User", { exact: true })).toBeVisible();
    await expect(page.getByRole("main").getByText("Roles", { exact: true })).toBeVisible();
  });

  test("shows the Refresh button", async ({ page }) => {
    await expect(page.getByRole("button", { name: /refresh/i })).toBeVisible();
  });

  test("shows Assign / Remove column for sovereign user", { tag: "@regression" }, async ({ page }) => {
    await expect(page.getByText(/assign \/ remove/i)).toBeVisible();
  });
});
