import type { Locator, Page } from "@playwright/test";
import { expect } from "@playwright/test";

export class VaultPage {
  readonly noteList: Locator;
  readonly notePanel: Locator;
  readonly noteContent: Locator;

  constructor(private readonly page: Page) {
    this.noteList = page.getByRole("list", { name: "Notes" });
    this.notePanel = page.getByTestId("vault-note-panel");
    this.noteContent = page.getByTestId("vault-note-content");
  }

  async goto(): Promise<void> {
    await this.page.goto("/vault");
  }

  async expectLoaded(): Promise<void> {
    await expect(this.page.getByRole("heading", { level: 1, name: "Vault" })).toBeVisible();
  }

  async openNote(title: string): Promise<void> {
    await this.noteList.getByRole("button", { name: new RegExp(`^${title}`) }).click();
    await expect(this.notePanel.getByRole("heading", { level: 2, name: title })).toBeVisible();
  }

  async showGraph(): Promise<void> {
    await this.page.getByTestId("vault-tab-graph").click();
  }
}
