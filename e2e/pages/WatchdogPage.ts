import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

export class WatchdogPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto("/watchdog");
  }

  async expectLoaded(): Promise<void> {
    await expect(this.page.getByRole("heading", { name: "Watchdog" })).toBeVisible();
  }

  async expectServicesTableVisible(): Promise<void> {
    await expect(this.page.getByText(/services/i).first()).toBeVisible();
  }

  async expectInfraTableVisible(): Promise<void> {
    await expect(this.page.getByText("Infrastructure", { exact: true })).toBeVisible();
  }

  async expectSummaryStatsVisible(): Promise<void> {
    await expect(this.page.getByText(/healthy|degraded|unhealthy/i).first()).toBeVisible();
  }

  async expectHostMetricsVisible(): Promise<void> {
    await expect(this.page.getByText(/host metrics/i)).toBeVisible();
  }

  async clickRefresh(): Promise<void> {
    await this.page.getByRole("button", { name: /refresh/i }).click();
  }
}
