import { test as setup, expect } from "@playwright/test";
import path from "path";
import { LoginPage } from "../pages/index";

const AUTH_FILE = path.join("playwright", ".auth", "user.json");

setup("authenticate", async ({ page, context }) => {
  await context.clearCookies();

  await page.goto("/", { waitUntil: "commit" });

  await page.waitForURL("**/login*", { timeout: 15_000 });

  const loginPage = new LoginPage(page);
  await loginPage.fillEmail(process.env.E2E_USER_EMAIL || "a@test.test");
  await loginPage.fillPassword(process.env.E2E_USER_PASSWORD || "testuser1a");

  await loginPage.submitSignIn();
  await page.waitForURL("http://localhost:5173/");

  await expect(page.getByRole("heading", { name: /dashboard/i })).toBeVisible();

  await page.context().storageState({ path: AUTH_FILE });
});
