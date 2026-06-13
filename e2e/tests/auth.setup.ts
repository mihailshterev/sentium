import { test as setup, expect } from "@playwright/test";
import path from "path";
import { LoginPage } from "../pages/index";

const AUTH_FILE = path.join("playwright", ".auth", "user.json");
const APP_URL = "http://localhost:5173/";

setup("authenticate", async ({ page, context }) => {
  setup.slow();

  await context.clearCookies();

  await expect(async () => {
    await page.goto("/", { waitUntil: "commit" });

    await page.waitForURL(/\/login|\/connect\/authorize/, { timeout: 30_000 });

    const loginPage = new LoginPage(page);
    await loginPage.fillEmail(process.env.E2E_USER_EMAIL || "a@test.test");
    await loginPage.fillPassword(process.env.E2E_USER_PASSWORD || "testuser1a");
    await loginPage.submitSignIn();

    await page.waitForURL(APP_URL, { timeout: 30_000, waitUntil: "commit" });
    await expect(page.getByRole("heading", { name: /control center/i })).toBeVisible({ timeout: 20_000 });
  }).toPass({ timeout: 150_000 });

  await context.storageState({ path: AUTH_FILE });

  await expect(async () => {
    await page.goto("/agents", { waitUntil: "commit" });
    await expect(page.getByRole("heading", { name: "Agent Registry" })).toBeVisible({ timeout: 15_000 });

    expect(page.url()).toContain("localhost:5173");
  }).toPass({ timeout: 90_000 });
});
