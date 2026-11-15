import type { Page } from "@playwright/test";
import { expect } from "@playwright/test";

export class ProfilePage {
  constructor(private readonly page: Page) {}

  async goto(): Promise<void> {
    await this.page.goto("/profile");
  }

  async expectLoaded(): Promise<void> {
    await expect(this.page.getByRole("button", { name: /save changes/i })).toBeVisible();
  }

  async fillFirstName(text: string): Promise<void> {
    await this.page.locator("#firstName").clear();
    await this.page.locator("#firstName").fill(text);
  }

  async fillLastName(text: string): Promise<void> {
    await this.page.locator("#lastName").clear();
    await this.page.locator("#lastName").fill(text);
  }

  async fillUserName(text: string): Promise<void> {
    await this.page.locator("#userName").clear();
    await this.page.locator("#userName").fill(text);
  }

  async saveProfile(): Promise<void> {
    await this.page.getByRole("button", { name: /save changes/i }).click();
  }

  async expectSuccessFeedback(): Promise<void> {
    await expect(this.page.getByText(/profile updated successfully/i)).toBeVisible();
  }

  async expectUserNameVisible(userName: string): Promise<void> {
    await expect(this.page.getByText(userName)).toBeVisible();
  }

  /** Opens the pairing dialog and returns the one-time code it shows. */
  async createPairingCode(): Promise<string> {
    await this.page.getByTestId("pair-device").click();
    const code = this.page.getByTestId("pairing-code");
    await expect(code).toHaveText(/^[A-Z0-9]{4}-[A-Z0-9]{4}$/);
    return (await code.textContent())!;
  }

  async closePairingDialog(): Promise<void> {
    await this.page.getByRole("dialog").getByRole("button", { name: "Done" }).click();
  }

  deviceItem(name: string) {
    return this.page.getByTestId("device-item").filter({ hasText: name });
  }

  async removeDevice(name: string): Promise<void> {
    await this.page.getByRole("button", { name: `Remove ${name}` }).click();
    await this.page.getByRole("dialog").getByRole("button", { name: "Remove" }).click();
  }

  async expectRoleBadge(role: string): Promise<void> {
    await expect(this.page.getByRole("main").getByText(new RegExp(role, "i")).first()).toBeVisible();
  }
}
