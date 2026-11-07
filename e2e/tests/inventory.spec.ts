import { test, expect } from "../fixtures";
import { uniqueName } from "../support/test-data";

test.describe("Inventory", () => {
  test.beforeEach(async ({ inventoryPage }) => {
    await inventoryPage.goto();
    await inventoryPage.expectLoaded();
  });

  test("shows the seeded baseline inventory", { tag: "@smoke" }, async ({ inventoryPage }) => {
    await expect(inventoryPage.treeItem("e2e-baseline-home")).toBeVisible();
    await expect(inventoryPage.treeItem("e2e-baseline-garage")).toBeVisible();
    await expect(inventoryPage.assetRow("e2e-baseline-drill")).toBeVisible();
  });

  test("filters the seeded asset by location and tag", async ({ inventoryPage, page }) => {
    await inventoryPage.selectLocation("e2e-baseline-garage");
    await expect(page).toHaveURL(/location=/);
    await expect(inventoryPage.assetRow("e2e-baseline-drill")).toBeVisible();

    await inventoryPage.goto("?tag=power&tag=tools");
    await expect(inventoryPage.assetRow("e2e-baseline-drill")).toBeVisible();
  });

  test("catalogues an asset through its whole lifecycle", { tag: "@regression" }, async ({ inventoryPage, page }) => {
    test.slow();

    const home = uniqueName("e2e-home");
    const shelf = uniqueName("e2e-shelf");
    const asset = uniqueName("e2e-lamp");
    const tag = uniqueName("e2e-tag", 32);
    const photo = `${uniqueName("front")}.png`;

    await test.step("create a location with a sub-location", async () => {
      await inventoryPage.createLocation(home);
      await expect(inventoryPage.treeItem(home)).toHaveAttribute("aria-selected", "true");

      await inventoryPage.addChildLocation(home, shelf);
      await expect(inventoryPage.treeItem(shelf)).toHaveAttribute("aria-level", "3");
    });

    await test.step("create a tagged asset and attach a photo", async () => {
      await inventoryPage.selectLocation(shelf);
      await inventoryPage.createAsset(asset, tag);
      await inventoryPage.expectAssetDetail(asset);

      await inventoryPage.uploadAttachment(photo);
      await inventoryPage.deleteAttachment(photo);
    });

    await test.step("find the asset by search and by tag", async () => {
      await inventoryPage.goto();
      await inventoryPage.search(asset);
      await expect(inventoryPage.assetRow(asset)).toBeVisible();
      await expect(inventoryPage.assetRow("e2e-baseline-drill")).toBeHidden();

      await inventoryPage.goto(`?tag=${encodeURIComponent(tag)}`);
      await expect(inventoryPage.assetRow(asset)).toBeVisible();
    });

    await test.step("deleting a non-empty location moves its contents to the parent", async () => {
      await inventoryPage.goto();
      await inventoryPage.deleteLocation(shelf);
      await inventoryPage.selectLocation(home);
      await expect(inventoryPage.assetRow(asset)).toBeVisible();
    });

    await test.step("delete the asset and then its empty location", async () => {
      await inventoryPage.assetRow(asset).click();
      await inventoryPage.deleteAssetFromDetail(asset);
      await expect(page.getByText("Nothing catalogued here yet")).toBeVisible();

      await inventoryPage.deleteLocation(home);
    });
  });
});
