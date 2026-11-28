// Assistant turns, end to end: queue the turn, then follow its SSE stream until the terminal frame. The stream request
// returns only when the turn has ended, so chat_turn_ms is the time a user waits for the complete answer.
import { check } from "k6";
import { Rate, Trend } from "k6/metrics";
import { MODEL, TURNS_PER_CONVERSATION } from "../lib/config.js";
import { expectStatus, get, json, post } from "../lib/http.js";
import { ensureSignedIn } from "../lib/session.js";
import { pick, think } from "../lib/think.js";

const acceptMs = new Trend("chat_accept_ms", true);
const turnMs = new Trend("chat_turn_ms", true);
const turnCompleted = new Rate("chat_turn_completed");

const PROMPTS = [
  "Where did I put the cordless drill?",
  "What is on my timeline this week?",
  "Summarise my notes about the dishwasher filter.",
  "Remind me how to swap the winter tyres.",
  "Draft a checklist for renewing our passports.",
];

let conversationId = null;
let turnsInConversation = 0;

/** The `data:` payloads of an SSE body; comments (heartbeats) and ids are skipped. */
function frames(body) {
  const result = [];
  for (const event of (body || "").split(/\r?\n\r?\n/)) {
    const data = event
      .split(/\r?\n/)
      .filter((line) => line.startsWith("data:"))
      .map((line) => line.slice(5).trimStart())
      .join("\n");
    if (data) {
      try {
        result.push(JSON.parse(data));
      } catch {
        // Not JSON: not a frame the relay sends; ignore it.
      }
    }
  }
  return result;
}

function startConversation() {
  const res = post(
    "/api/praxis/conversations",
    { title: "Load chat", model: MODEL },
    "POST /praxis/conversations",
    "chat",
  );
  conversationId = expectStatus(res, 201, "create chat conversation") ? json(res, {}).id : null;
  turnsInConversation = 0;
}

export function chat(data) {
  ensureSignedIn(data.userCount);

  if (!conversationId || turnsInConversation >= TURNS_PER_CONVERSATION) {
    startConversation();
    if (!conversationId) {
      turnCompleted.add(false);
      return;
    }
  }

  const started = Date.now();
  const accepted = post(
    "/api/praxis/assistant/chat",
    { conversationId, model: MODEL, messages: [{ role: "user", content: pick(PROMPTS) }] },
    "POST /praxis/assistant/chat",
    "chat",
  );
  acceptMs.add(accepted.timings.duration);

  const turnId = expectStatus(accepted, 202, "queue assistant turn") ? json(accepted, {}).turnId : null;
  if (!turnId) {
    turnCompleted.add(false);
    think(3, 6);
    return;
  }

  const stream = get(
    `/api/praxis/assistant/turns/${turnId}/stream`,
    "GET /praxis/assistant/turns/{id}/stream",
    "stream",
    {
      headers: { Accept: "text/event-stream" },
      timeout: "180s",
    },
  );
  turnMs.add(Date.now() - started);
  turnsInConversation++;

  const all = frames(stream.body);
  const last = all[all.length - 1] || {};
  const answered = all.some(
    (frame) => frame.type !== "done" && typeof frame.text === "string" && frame.text.length > 0,
  );
  const ok = check(stream, {
    "turn stream -> 200": (r) => r.status === 200,
    "turn ends with done": () => last.type === "done",
    "turn streamed an answer": () => answered,
  });
  turnCompleted.add(ok);

  // Reading the answer before the next question.
  think(4, 10);
}
