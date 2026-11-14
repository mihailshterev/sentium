import { test, expect } from "../fixtures";

const SANDBOX_DOWN = "Sandbox is down";
const INCIDENT_WAIT = 45_000;

test.describe("Notifications", () => {
  test("a Vigil incident reaches the Sovereign's inbox", { tag: "@smoke" }, async ({ dashboardPage, navbar, page }) => {
    await dashboardPage.goto();

    await expect(navbar.notificationBell).toBeVisible();

    await expect(async () => {
      const panel = await navbar.openInboxPanel();
      try {
        await expect(panel.getByText(SANDBOX_DOWN)).toBeVisible({ timeout: 2_000 });
      } finally {
        await page.keyboard.press("Escape");
      }
    }).toPass({ timeout: INCIDENT_WAIT });

    const panel = await navbar.openInboxPanel();
    await panel.getByRole("link", { name: "Open inbox" }).click();

    await expect(page).toHaveURL(/\/inbox$/);
    await expect(page.getByRole("heading", { level: 1, name: "Inbox" })).toBeVisible();
    await expect(page.getByTestId("notification-item").filter({ hasText: SANDBOX_DOWN })).toBeVisible();
  });
});
