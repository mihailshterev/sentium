import type { Locator, Page } from "@playwright/test";
import { expect } from "@playwright/test";

/** A 1x1 transparent PNG for attachment uploads. */
export const TINY_PNG = Buffer.from(
  "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=",
  "base64",
);

export class InventoryPage {
  constructor(private readonly page: Page) {}

  async goto(query = ""): Promise<void> {
    await this.page.goto(`/inventory${query}`);
  }

  async expectLoaded(): Promise<void> {
    await expect(this.page.getByRole("heading", { name: "Inventory", exact: true })).toBeVisible();
    await expect(this.page.getByRole("tree", { name: "Locations" })).toBeVisible();
  }

  treeItem(name: string): Locator {
    return this.page.getByRole("treeitem", { name, exact: true });
  }

  assetRow(name: string): Locator {
    return this.page.getByTestId(`asset-row-${name}`);
  }

  private get dialog(): Locator {
    return this.page.getByRole("dialog");
  }

  async selectLocation(name: string): Promise<void> {
    await this.treeItem(name).click();
  }

  async createLocation(name: string): Promise<void> {
    await this.page.getByRole("button", { name: "New location" }).click();
    await this.dialog.getByLabel("Name").fill(name);
    await this.dialog.getByRole("button", { name: "Create location" }).click();
    await expect(this.dialog).toBeHidden();
  }

  async addChildLocation(parent: string, name: string): Promise<void> {
    await this.treeItem(parent).hover();
    await this.page.getByRole("button", { name: `Add a location inside ${parent}` }).click();
    await this.dialog.getByLabel("Name").fill(name);
    await this.dialog.getByRole("button", { name: "Create location" }).click();
    await expect(this.dialog).toBeHidden();
  }

  /** Creates a private asset (the e2e stack has no embedding model) at the selected location. */
  async createAsset(name: string, tag: string): Promise<void> {
    await this.page.getByRole("button", { name: "New asset" }).first().click();
    await this.dialog.getByLabel("Name", { exact: true }).fill(name);
    const tags = this.dialog.getByLabel("Tags");
    await tags.fill(tag);
    await tags.press("Enter");
    await this.dialog.getByRole("switch").uncheck();
    await this.dialog.getByRole("button", { name: "Create asset" }).click();
  }

  async expectAssetDetail(name: string): Promise<void> {
    await expect(this.page.getByRole("heading", { name, exact: true })).toBeVisible();
  }

  async uploadAttachment(fileName: string): Promise<void> {
    await this.page
      .getByTestId("attachment-input")
      .setInputFiles({ name: fileName, mimeType: "image/png", buffer: TINY_PNG });
    await expect(this.page.getByRole("button", { name: `View ${fileName}` })).toBeVisible();
  }

  async deleteAttachment(fileName: string): Promise<void> {
    await this.page.getByRole("button", { name: `View ${fileName}` }).hover();
    await this.page.getByTestId(`attachment-delete-${fileName}`).click();
    await this.page.getByTestId("confirm-dialog-confirm").click();
    await expect(this.page.getByRole("button", { name: `View ${fileName}` })).toBeHidden();
  }

  async search(text: string): Promise<void> {
    await this.page.getByLabel("Search assets").fill(text);
  }

  /** Deletes a location; a non-empty one moves its contents to the dialog's default target (its parent). */
  async deleteLocation(name: string): Promise<void> {
    await this.treeItem(name).hover();
    await this.page.getByTestId(`location-delete-${name}`).click();
    await this.page.getByTestId("confirm-dialog-confirm").click();
    await expect(this.treeItem(name)).toBeHidden();
  }

  async deleteAssetFromDetail(name: string): Promise<void> {
    await this.page.getByTestId(`asset-delete-${name}`).click();
    await this.page.getByTestId("confirm-dialog-confirm").click();
    await expect(this.page.getByRole("heading", { name: "Inventory", exact: true })).toBeVisible();
  }
}
