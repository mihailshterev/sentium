import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

export class DashboardPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto("/");
  }

  async expectLoaded(): Promise<void> {
    await expect(this.page.getByRole("heading", { name: "Dashboard" })).toBeVisible();
  }

  async expectStatCard(label: string): Promise<void> {
    await expect(this.page.getByRole("heading", { name: label })).toBeVisible();
  }

  async clickQuickAccess(name: string): Promise<void> {
    await this.page.getByRole("link", { name }).click();
  }

  async expectSystemStatus(text: string): Promise<void> {
    await expect(this.page.getByText(text)).toBeVisible();
  }

  async expectAgentCount(count: number): Promise<void> {
    await expect(this.page.getByText(new RegExp(`${count}`))).toBeVisible();
  }
}
