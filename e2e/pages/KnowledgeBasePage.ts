import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

export class KnowledgeBasePage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto("/knowledge-base");
  }

  async expectLoaded(): Promise<void> {
    await expect(this.page.getByRole("heading", { name: "Knowledge Base" })).toBeVisible();
  }

  async switchToGlobalContext(): Promise<void> {
    await this.page.getByTestId("tab-global-context").click();
  }

  async switchToAgentLearnings(): Promise<void> {
    await this.page.getByTestId("tab-agent-learnings").click();
  }

  async expectCollectionStatsVisible(): Promise<void> {
    await expect(this.page.getByText("Collections", { exact: true })).toBeVisible();
  }

  async expectLearningVisible(text: string): Promise<void> {
    await expect(this.page.getByText(text)).toBeVisible();
  }

  async expectLearningCount(count: number): Promise<void> {
    await expect(this.page.getByText(new RegExp(`${count} total`))).toBeVisible();
  }
}
