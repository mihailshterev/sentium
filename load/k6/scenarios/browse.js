// Read-heavy navigation: each iteration opens one portal page, firing the same parallel GETs the page does.
import { check } from "k6";
import { getPage } from "../lib/http.js";
import { ensureSignedIn } from "../lib/session.js";
import { pick, think } from "../lib/think.js";

const SEARCH_TERMS = ["drill", "tyres", "filter", "battery", "passport"];

function pages() {
  const now = new Date();
  const year = now.getUTCFullYear();
  const month = now.getUTCMonth() + 1;

  return {
    home: [
      ["/api/identity/account/session", "GET /account/session"],
      ["/api/nuntius/notifications/unread-count", "GET /notifications/unread-count"],
      ["/api/tempus/upcoming?days=7", "GET /tempus/upcoming"],
      ["/api/praxis/conversations?page=1&pageSize=5", "GET /praxis/conversations"],
      ["/api/vigil/status/overview", "GET /vigil/status/overview"],
    ],
    assistant: [
      ["/api/praxis/conversations?page=1&pageSize=20", "GET /praxis/conversations"],
      ["/api/praxis/agents", "GET /praxis/agents"],
      ["/api/praxis/models", "GET /praxis/models"],
    ],
    inventory: [
      ["/api/locus/locations", "GET /locus/locations"],
      [`/api/locus/assets?q=${pick(SEARCH_TERMS)}&page=1&pageSize=20`, "GET /locus/assets"],
    ],
    timeline: [
      [`/api/tempus/years/${year}`, "GET /tempus/years/{year}"],
      [`/api/tempus/years/${year}/months/${month}`, "GET /tempus/years/{year}/months/{month}"],
    ],
    notifications: [["/api/nuntius/notifications?limit=20", "GET /nuntius/notifications"]],
    settings: [
      ["/api/identity/account/me", "GET /account/me"],
      ["/api/registry/settings/schema", "GET /registry/settings/schema"],
    ],
    skills: [["/api/praxis/skills", "GET /praxis/skills"]],
  };
}

// How often members open each page, roughly: home and the assistant dominate.
const PAGE_MIX = [
  "home",
  "home",
  "home",
  "assistant",
  "assistant",
  "inventory",
  "timeline",
  "timeline",
  "notifications",
  "settings",
  "skills",
];

export function browse(data) {
  ensureSignedIn(data.userCount);

  const page = pick(PAGE_MIX);
  const responses = getPage(pages()[page]);
  check(responses, { [`${page} page loads`]: (all) => all.every((r) => r.status === 200) });

  think(1, 4);
}
