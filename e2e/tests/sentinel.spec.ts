import { test, expect } from "../fixtures";

test.describe("Sentinel", () => {
  test.beforeEach(async ({ sentinelPage }) => {
    await sentinelPage.goto();
    await sentinelPage.expectLoaded();
  });

  test("renders the Sentinel heading", { tag: "@smoke" }, async ({ page }) => {
    await expect(page.getByRole("heading", { name: "Sentinel" })).toBeVisible();
  });

  test("shows the Security Pulse audit panel", { tag: "@smoke" }, async ({ sentinelPage }) => {
    await sentinelPage.expectAuditTableVisible();
  });

  test("shows seeded audit records", { tag: "@smoke" }, async ({ sentinelPage }) => {
    await sentinelPage.expectAuditRowVisible("e2e-baseline-agent");
  });

  test("shows Sovereign Controls panel", { tag: "@regression" }, async ({ sentinelPage }) => {
    await sentinelPage.expectSovereignControlsVisible();
  });

  test("shows Semantic Alignment panel", { tag: "@regression" }, async ({ sentinelPage }) => {
    await sentinelPage.expectAlignmentGaugeVisible();
  });

  test("lockdown toggle is visible and interactive", { tag: "@regression" }, async ({ sentinelPage }) => {
    await sentinelPage.expectLockdownToggleVisible();
    const toggle = await sentinelPage.getLockdownToggle();
    await expect(toggle).not.toBeDisabled();
  });

  test("links to the policy settings", { tag: "@regression" }, async ({ sentinelPage, page }) => {
    await sentinelPage.expectPolicySettingsLinkVisible();
    await page.getByRole("link", { name: /configure policy in settings/i }).click();
    await expect(page).toHaveURL(/\/settings\/security$/);
  });
});
