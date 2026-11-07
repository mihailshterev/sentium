import { test, expect } from "../fixtures";
import { testUser } from "../support/env";

// Start signed out: these tests exercise the login page itself.
test.use({ storageState: { cookies: [], origins: [] } });

test.describe("Authentication", () => {
  test("redirects an anonymous visitor to the login page", { tag: "@smoke" }, async ({ page }) => {
    await page.goto("/agents");
    await expect(page).toHaveURL(/\/login/);
  });

  test("signs in with valid credentials", { tag: "@smoke" }, async ({ page, loginPage }) => {
    await loginPage.goto();
    await loginPage.login(testUser.userName, testUser.password);

    await expect(page.getByRole("heading", { name: "Control Center" })).toBeVisible();
  });

  test("rejects invalid credentials", { tag: "@regression" }, async ({ page, loginPage }) => {
    await loginPage.goto();
    // An unknown user, so failed attempts never count towards the seeded user's lockout.
    await loginPage.login("e2e-no-such-user", "wrong-password1");

    await expect(page.getByText("Invalid username or password.")).toBeVisible();
    await expect(page).toHaveURL(/\/login/);
  });
});
