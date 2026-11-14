import { test, expect } from "../fixtures";

// Third-party requests and CSP violations are caught for every test by the ownOriginOnly fixture.
test.describe("Content security", () => {
  test("serves the portal with an own-origin Content-Security-Policy", { tag: "@smoke" }, async ({ page }) => {
    const response = await page.goto("/");

    const policy = response?.headers()["content-security-policy"] ?? "";
    expect(policy).toContain("default-src 'self'");
    expect(policy).toContain("script-src 'self'");
    expect(policy).toContain("connect-src 'self'");
  });

  test("renders the self-hosted fonts", { tag: "@smoke" }, async ({ page, dashboardPage }) => {
    await dashboardPage.goto();
    await dashboardPage.expectLoaded();

    const loaded = await page.evaluate(async () => {
      await document.fonts.ready;
      const families = ["Inter", "Space Grotesk", "JetBrains Mono"];
      await Promise.all(families.map((family) => document.fonts.load(`400 16px "${family}"`)));
      const loadedFaces = [...document.fonts].filter((face) => face.status === "loaded");
      return families.filter((family) => loadedFaces.some((face) => face.family.replace(/["']/g, "") === family));
    });

    expect(loaded).toEqual(["Inter", "Space Grotesk", "JetBrains Mono"]);
  });
});
