import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

export class SandboxPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto("/sandbox");
  }

  async expectLoaded(): Promise<void> {
    await expect(this.page.getByRole("heading", { name: "Sandbox Inspector" })).toBeVisible();
  }

  async expectStatsVisible(): Promise<void> {
    await expect(this.page.getByText(/total runs/i)).toBeVisible();
  }

  async filterByStatus(status: string): Promise<void> {
    await this.page.getByRole("button", { name: status, exact: true }).click();
  }

  async filterByLanguage(lang: string): Promise<void> {
    await this.page.getByRole("button", { name: lang, exact: true }).click();
  }

  async searchFor(query: string): Promise<void> {
    await this.page.getByPlaceholder(/search by agent or job/i).fill(query);
  }

  async expectNoExecutions(): Promise<void> {
    await expect(this.page.getByText(/no executions found/i)).toBeVisible();
  }

  async expectSearchInputVisible(): Promise<void> {
    await expect(this.page.getByPlaceholder(/search by agent or job/i)).toBeVisible();
  }

  async clickRefresh(): Promise<void> {
    await this.page.getByRole("button", { name: /refresh/i }).click();
  }
}
