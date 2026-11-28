// Retrieval paths: semantic search over the shared corpus (embedding + pgvector), and conversation text search.
import { check } from "k6";
import { expectStatus, get, json, post } from "../lib/http.js";
import { ensureSignedIn } from "../lib/session.js";
import { pick, think } from "../lib/think.js";

const QUERIES = [
  "how do I charge the drill battery",
  "tyre torque settings",
  "clean the dishwasher filter",
  "program the irrigation timer",
  "passport renewal documents",
  "router guest wifi",
];

export function search(data) {
  ensureSignedIn(data.userCount);

  if (Math.random() < 0.7) {
    const res = post(
      "/api/codex/knowledge-map/search",
      { query: pick(QUERIES), topK: 10 },
      "POST /codex/knowledge-map/search",
      "search",
    );
    if (expectStatus(res, 200, "semantic search")) {
      check(res, { "semantic search returns a body": () => json(res) !== null });
    }
  } else {
    const term = pick(["drill", "timeline", "filter", "tyres"]);
    const res = get(
      `/api/praxis/conversations?page=1&pageSize=20&search=${term}`,
      "GET /praxis/conversations?search",
      "search",
    );
    expectStatus(res, 200, "conversation search");
  }

  think(2, 6);
}
