import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

const SLOW = 60_000;

export class SemanticMapPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto("/semantic-map");
  }

  async expectLoaded(): Promise<void> {
    await expect(this.page.locator("canvas")).toBeVisible({ timeout: SLOW });
  }

  async expectCanvasVisible(): Promise<void> {
    await expect(this.page.locator("canvas")).toBeVisible({ timeout: SLOW });
  }

  async searchMap(query: string): Promise<void> {
    const input = this.page.getByPlaceholder(/search/i);
    await input.fill(query);
    await input.press("Enter");
  }

  async expectSearchInputVisible(): Promise<void> {
    await expect(this.page.getByPlaceholder(/search/i)).toBeVisible({ timeout: SLOW });
  }

  async expectDemoModeVisible(): Promise<void> {
    await expect(this.page.getByText(/demo/i)).toBeVisible({ timeout: SLOW });
  }
}
