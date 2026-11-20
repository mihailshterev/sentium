import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

export class SchedulerPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto("/scheduler");
  }

  async expectLoaded(): Promise<void> {
    await expect(this.page.getByRole("heading", { name: "Scheduler", exact: true })).toBeVisible();
  }

  async expectStatsVisible(): Promise<void> {
    await expect(this.page.getByText("Paused", { exact: true }).first()).toBeVisible();
    await expect(this.page.getByText(/last run failed/i)).toBeVisible();
  }

  async expectEmptyState(): Promise<void> {
    await expect(this.page.getByText(/no schedules yet/i)).toBeVisible();
  }

  async expectJobVisible(jobName: string): Promise<void> {
    await expect(this.page.getByText(jobName)).toBeVisible();
  }

  async clickRefresh(): Promise<void> {
    await this.page.getByRole("button", { name: /^refresh$/i }).click();
  }

  async openNewSchedule(): Promise<void> {
    await this.page.getByTestId("schedule-new").click();
    await expect(this.page.getByRole("dialog", { name: "New schedule" })).toBeVisible();
  }

  async openBackgroundJobs(): Promise<void> {
    await this.page.getByTestId("scheduler-tab-background").click();
  }
}
