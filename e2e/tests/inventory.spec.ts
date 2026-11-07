import { test, expect } from "../fixtures/auth.fixture";

// Browser projects run in parallel against one database: keep every name unique per run and project.
let suffix = "";
const names = () => ({
  home: `e2e-home-${suffix}`,
  shelf: `e2e-shelf-${suffix}`,
  asset: `e2e-lamp-${suffix}`,
  tag: `e2e-tag-${suffix}`.slice(0, 32),
  photo: `front-${suffix}.png`,
});

test.describe.serial("Inventory", () => {
  test.beforeAll(({}, testInfo) => {
    suffix = `${testInfo.project.name}-${Date.now().toString(36)}`;
  });

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

  test("creates a location with a sub-location", { tag: "@regression" }, async ({ inventoryPage }) => {
    const { home, shelf } = names();

    await inventoryPage.createLocation(home);
    await expect(inventoryPage.treeItem(home)).toHaveAttribute("aria-selected", "true");

    await inventoryPage.addChildLocation(home, shelf);
    await expect(inventoryPage.treeItem(shelf)).toHaveAttribute("aria-level", "3");
  });

  test("creates a tagged asset and attaches a photo", { tag: "@regression" }, async ({ inventoryPage }) => {
    const { shelf, asset, tag, photo } = names();

    await inventoryPage.selectLocation(shelf);
    await inventoryPage.createAsset(asset, tag);
    await inventoryPage.expectAssetDetail(asset);

    await inventoryPage.uploadAttachment(photo);
    await inventoryPage.deleteAttachment(photo);
  });

  test("finds the asset by search and by tag", { tag: "@regression" }, async ({ inventoryPage }) => {
    const { asset, tag } = names();

    await inventoryPage.search(asset);
    await expect(inventoryPage.assetRow(asset)).toBeVisible();
    await expect(inventoryPage.assetRow("e2e-baseline-drill")).toBeHidden();

    await inventoryPage.goto(`?tag=${encodeURIComponent(tag)}`);
    await expect(inventoryPage.assetRow(asset)).toBeVisible();
  });

  test(
    "deleting a non-empty location moves its contents to the parent",
    { tag: "@regression" },
    async ({ inventoryPage }) => {
      const { home, shelf, asset } = names();

      await inventoryPage.deleteLocation(shelf);
      await inventoryPage.selectLocation(home);
      await expect(inventoryPage.assetRow(asset)).toBeVisible();
    },
  );

  test("deletes the asset and then its empty location", { tag: "@regression" }, async ({ inventoryPage, page }) => {
    const { home, asset } = names();

    await inventoryPage.selectLocation(home);
    await inventoryPage.assetRow(asset).click();
    await inventoryPage.deleteAssetFromDetail(asset);
    await expect(page.getByText("Nothing catalogued here yet")).toBeVisible();

    await inventoryPage.deleteLocation(home);
  });
});
