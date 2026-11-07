import { test as setup, expect } from "@playwright/test";
import { paths, testUser } from "../support/env";

/**
 * Signs the seeded user in once through the API and saves the session cookie for every browser project.
 * Logging in through the UI is covered by auth.spec.ts; repeating it here would only add flakiness.
 */
setup("authenticate the seeded e2e user", async ({ page }) => {
  const login = await page.request.post("/api/identity/account/login", {
    data: { userName: testUser.userName, password: testUser.password },
  });
  expect(login.status(), await login.text()).toBe(200);

  const session = await page.request.get("/api/identity/account/session");
  expect(session.status()).toBe(200);
  expect(await session.json()).toMatchObject({ name: testUser.userName });

  // The portal treats this cookie as a signed-in session: confirm it end to end before sharing it.
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Control Center" })).toBeVisible();

  await page.context().storageState({ path: paths.authState });
});
