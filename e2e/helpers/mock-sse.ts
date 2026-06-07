import type { Page } from "@playwright/test";

export async function mockSseRoute(page: Page, urlPattern: string | RegExp, events: string[]): Promise<void> {
  await page.route(urlPattern, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "text/event-stream",
      headers: {
        "Cache-Control": "no-cache",
        Connection: "keep-alive",
      },
      body: events.join(""),
    });
  });
}

export function sseMessage(data: object): string {
  return `data: ${JSON.stringify(data)}\n\n`;
}

export function sseDone(): string {
  return sseMessage({ type: "done" });
}
