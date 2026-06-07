import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

export class SystemPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto("/system");
  }

  async expectLoaded(): Promise<void> {
    await expect(this.page.getByRole("heading", { name: "System" })).toBeVisible();
  }

  async expectCpuMetricVisible(): Promise<void> {
    await expect(this.page.getByText(/cpu/i).first()).toBeVisible();
  }

  async expectMemoryMetricVisible(): Promise<void> {
    await expect(this.page.getByText(/memory/i).first()).toBeVisible();
  }

  async expectUptimeVisible(): Promise<void> {
    await expect(this.page.getByText(/system uptime|uptime/i).first()).toBeVisible();
  }

  async expectStatCardsVisible(): Promise<void> {
    await expect(this.page.getByText("CPU (Process)")).toBeVisible();
    await expect(this.page.getByText("Memory Usage")).toBeVisible();
  }

  async clickRefresh(): Promise<void> {
    await this.page.getByRole("button", { name: /refresh/i }).click();
  }
}
