import type { Page } from "@playwright/test";

/** One update of an assistant turn, as the relay stream sends it. */
export interface TurnFrame {
  type: string;
  text: string;
}

export const turnFrame = (type: string, text: string): TurnFrame => ({ type, text });

interface MockTurnOptions {
  turnId?: string;
  /** Holds the stream back this long, e.g. to see the UI while the answer is pending. */
  delayMs?: number;
}

/**
 * Mocks one assistant turn: the chat request is accepted with a turn id, and the turn's stream replays `frames`
 * (numbered like the relay does) followed by the terminal "done" frame.
 */
export async function mockAssistantTurn(
  page: Page,
  frames: TurnFrame[],
  { turnId = "e2e-turn", delayMs = 0 }: MockTurnOptions = {},
): Promise<void> {
  await page.route(/\/api\/praxis\/assistant\/chat$/, (route) => route.fulfill({ status: 202, json: { turnId } }));

  await page.route(new RegExp(`/api/praxis/assistant/turns/${turnId}/stream`), async (route) => {
    if (delayMs > 0) {
      await new Promise((resolve) => setTimeout(resolve, delayMs));
    }
    const events = [...frames, { type: "done", done: true }].map((frame, index) => sseEvent(frame, index + 1));
    await route.fulfill({
      status: 200,
      contentType: "text/event-stream",
      headers: { "Cache-Control": "no-cache" },
      body: events.join(""),
    });
  });
}

function sseEvent(data: object, id: number): string {
  return `id: ${id}\ndata: ${JSON.stringify(data)}\n\n`;
}
