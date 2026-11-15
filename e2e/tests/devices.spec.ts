import { test, expect } from "../fixtures";

const TRAY_USER_AGENT = "SentiumTray/9.9.9-e2e";

/** The JSON body exactly as Sentium Tray sends it. */
const report = (machineName: string) => ({
  machineName,
  osDescription: "Windows 11 Pro",
  uptimeSeconds: 7_200,
  appVersion: "9.9.9-e2e",
  cpuPercent: 37,
  memoryTotalBytes: 16 * 1024 ** 3,
  memoryAvailableBytes: 4 * 1024 ** 3,
  disks: [{ name: "C:", totalBytes: 500 * 1024 ** 3, availableBytes: 200 * 1024 ** 3 }],
});

test.describe("Devices", () => {
  test("pair a PC, see it report on Vigil, then remove it", { tag: "@regression" }, async ({
    profilePage,
    vigilPage,
    page,
    playwright,
    baseURL,
  }) => {
    const name = `E2E-PC-${Date.now()}`;

    await profilePage.goto();
    const code = await profilePage.createPairingCode();
    await expect(page.getByTestId("pairing-server")).toHaveText(new URL(baseURL!).origin);
    await profilePage.closePairingDialog();

    // The tray app is played by a separate, cookie-less request context.
    const tray = await playwright.request.newContext({ baseURL, extraHTTPHeaders: { "User-Agent": TRAY_USER_AGENT } });
    let deviceId: string | undefined;
    try {
      const paired = await tray.post("/api/identity/devices/pair", { data: { code, name } });
      expect(paired.status()).toBe(200);
      const { deviceId: id, token } = await paired.json();
      deviceId = id;

      const reported = await tray.post("/api/vigil/devices/report", {
        data: report(name),
        headers: { Authorization: `Bearer ${token}` },
      });
      expect(reported.status()).toBe(200);
      expect(await reported.json()).toEqual({ nextReportSeconds: expect.any(Number) });

      await profilePage.goto();
      await expect(profilePage.deviceItem(name)).toContainText("v9.9.9-e2e");

      await vigilPage.goto();
      const row = vigilPage.deviceRow(name);
      await expect(row).toContainText("Online");
      await expect(row).toContainText("Windows 11 Pro");
      await expect(row).toContainText("37%");
      await expect(row).toContainText("C: 60%");

      await profilePage.goto();
      await profilePage.removeDevice(name);
      await expect(profilePage.deviceItem(name)).toHaveCount(0);
      deviceId = undefined;

      const afterRemoval = await tray.post("/api/vigil/devices/report", {
        data: report(name),
        headers: { Authorization: `Bearer ${token}` },
      });
      expect(afterRemoval.status()).toBe(401);
    } finally {
      // Keep a reused server under the per-user device limit when an assertion fails midway.
      if (deviceId) {
        await page.request.delete(`/api/identity/devices/${deviceId}`);
      }
      await tray.dispose();
    }
  });
});
