import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

export class AssistantPage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto("/assistant");
  }

  async expectLoaded(): Promise<void> {
    await expect(this.page.getByRole("heading", { name: /assistant/i })).toBeVisible();
  }

  async startNewConversation(): Promise<void> {
    await this.page.getByRole("button", { name: /new conversation/i }).click();
  }

  async selectConversation(title: string): Promise<void> {
    await this.page.getByRole("button", { name: title }).click();
  }

  async typeMessage(message: string): Promise<void> {
    await this.page.getByPlaceholder(/ask sentium/i).fill(message);
  }

  async sendMessage(): Promise<void> {
    await this.page.locator("form button[type='submit']").click();
  }

  async sendMessageWithEnter(message: string): Promise<void> {
    const input = this.page.getByPlaceholder(/ask sentium/i);
    await input.fill(message);
    await input.press("Enter");
  }

  async selectModel(model: string): Promise<void> {
    await this.page.getByRole("combobox", { name: /model/i }).selectOption(model);
  }

  async expectMessageVisible(text: string): Promise<void> {
    await expect(this.page.getByText(text)).toBeVisible();
  }

  async expectWelcomeScreen(): Promise<void> {
    await expect(this.page.getByRole("heading", { name: /how can I help/i })).toBeVisible();
  }

  async expectTypingIndicator(): Promise<void> {
    await expect(this.page.getByRole("status", { name: /thinking|typing/i })).toBeVisible();
  }

  async deleteConversation(title: string): Promise<void> {
    const item = this.page.getByRole("listitem").filter({ hasText: title });
    await item.getByRole("button", { name: /delete/i }).click();
  }
}
