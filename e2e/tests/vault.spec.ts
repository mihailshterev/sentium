import { test, expect } from "../fixtures";

// The seeded vault (MemoriaE2EDataSeeder): e2e-hub links to e2e-ideas, Projects/e2e-project and the missing
// e2e-missing; e2e-ideas links back to the hub.
test.describe("Vault", () => {
  test.beforeEach(async ({ vaultPage }) => {
    await vaultPage.goto();
    await vaultPage.expectLoaded();
  });

  test("shows the synced vault and follows wikilinks between notes", { tag: "@smoke" }, async ({ vaultPage, page }) => {
    await expect(page.getByText("e2e-vault")).toBeVisible();

    await vaultPage.openNote("e2e-hub");
    await expect(vaultPage.notePanel.getByRole("link", { name: /Open in Obsidian/ })).toHaveAttribute(
      "href",
      "obsidian://open?vault=e2e-vault&file=e2e-hub",
    );
    await expect(vaultPage.noteContent.getByText("e2e-missing")).toBeVisible();

    await vaultPage.noteContent.getByRole("button", { name: "e2e-ideas" }).click();
    await expect(vaultPage.notePanel.getByRole("heading", { level: 2, name: "e2e-ideas" })).toBeVisible();
    await expect(page).toHaveURL(/note=/);

    const backlinks = vaultPage.notePanel.getByRole("region", { name: "Backlinks" });
    await expect(backlinks.getByRole("button", { name: "e2e-hub" })).toBeVisible();
    await expect(backlinks.getByRole("button", { name: "e2e-project" })).toBeVisible();
  });

  test("draws the note graph", async ({ vaultPage, page }) => {
    await vaultPage.showGraph();
    await expect(page.getByRole("img", { name: "Graph of 3 notes" })).toBeVisible();
  });
});
