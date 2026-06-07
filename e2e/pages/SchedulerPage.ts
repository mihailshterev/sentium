import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

export class SchedulerPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto("/scheduler");
  }

  async expectLoaded(): Promise<void> {
    await expect(this.page.getByRole("heading", { name: "Automated Scheduler" })).toBeVisible();
  }

  async expectEngineStatusVisible(): Promise<void> {
    await expect(this.page.getByText(/engine pipeline status/i)).toBeVisible();
  }

  async expectJobCountVisible(): Promise<void> {
    await expect(this.page.getByText(/active core loops/i)).toBeVisible();
  }

  async expectEmptyState(): Promise<void> {
    await expect(this.page.getByText(/no automated cron jobs/i)).toBeVisible();
  }

  async expectJobVisible(jobName: string): Promise<void> {
    await expect(this.page.getByText(jobName)).toBeVisible();
  }

  async clickRefresh(): Promise<void> {
    await this.page.getByRole("button", { name: /refresh engine/i }).click();
  }
}
