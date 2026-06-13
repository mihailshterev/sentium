import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

export class SkillsPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto("/skills");
  }

  async expectLoaded(): Promise<void> {
    await expect(this.page.getByRole("heading", { name: "Agent Skills" })).toBeVisible();
  }

  async switchToBuiltIn(): Promise<void> {
    await this.page.getByTestId("tab-builtin").click();
  }

  async switchToCustom(): Promise<void> {
    await this.page.getByTestId("tab-custom").click();
  }

  async switchToUploaded(): Promise<void> {
    await this.page.getByTestId("tab-uploaded").click();
  }

  async expectSkillVisible(name: string): Promise<void> {
    await expect(this.page.getByText(name)).toBeVisible();
  }

  async expectBuiltInTabActive(): Promise<void> {
    await expect(this.page.getByTestId("tab-builtin")).toHaveAttribute("class", /activeTab/);
  }

  async expectUploadedTabVisible(): Promise<void> {
    await expect(this.page.getByTestId("tab-uploaded")).toBeVisible();
  }
}
