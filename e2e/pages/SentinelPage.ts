import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

export class SentinelPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto("/sentinel");
  }

  async expectLoaded(): Promise<void> {
    await expect(this.page.getByRole("heading", { name: "Sentinel" })).toBeVisible();
  }

  async expectAuditTableVisible(): Promise<void> {
    await expect(this.page.getByText(/security pulse/i)).toBeVisible();
  }

  async expectAuditRowVisible(agentId: string): Promise<void> {
    await expect(this.page.getByText(agentId).first()).toBeVisible();
  }

  async expectSovereignControlsVisible(): Promise<void> {
    await expect(this.page.getByText(/sovereign controls/i)).toBeVisible();
  }

  async expectAlignmentGaugeVisible(): Promise<void> {
    await expect(this.page.getByText(/semantic alignment/i)).toBeVisible();
  }

  async getLockdownToggle() {
    return this.page.getByTestId("lockdown-toggle");
  }

  async expectLockdownToggleVisible(): Promise<void> {
    await expect(this.page.getByTestId("lockdown-toggle")).toBeVisible();
  }

  async expectPolicySettingsLinkVisible(): Promise<void> {
    await expect(this.page.getByRole("link", { name: /configure policy in settings/i })).toBeVisible();
  }

  async clickRefresh(): Promise<void> {
    await this.page.getByRole("button", { name: /refresh/i }).click();
  }
}
