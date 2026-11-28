// Test data for a run: N Member accounts (one per VU), a shared inventory location the write scenario files assets
// under, and a shared knowledge-base corpus the search scenario queries. Idempotent, so a rerun against a live stack
// reuses what exists; teardown() removes the location and the corpus and restores the registration setting.
import http from "k6/http";
import { fail } from "k6";
import { KB_DOCUMENTS, KB_SOURCE, LOAD_USER_PASSWORD, SOVEREIGN } from "./config.js";
import { del, expectStatus, get, json, post, put, url } from "./http.js";
import { login, loadUserName } from "./session.js";

const ACCESS_SETTINGS = "/api/registry/settings/access";
const LOCATION_NAME = "Load test shelf";
const REGISTER_BATCH = 20;
const INGEST_BATCH = 20;
const REGISTERED_OR_EXISTS = http.expectedStatuses(200, 400);

/** Ingestion source type "Custom" (Sentium.Codex.Contracts.Models.IngestionSourceType). */
const CUSTOM_SOURCE_TYPE = 3;

const TOPICS = [
  "cordless drill battery charging and storage",
  "winter tyres swap schedule and torque settings",
  "dishwasher filter cleaning and descaling",
  "garden irrigation timer programming",
  "bike chain lubrication and gear indexing",
  "router firmware updates and guest wifi",
  "smoke detector battery replacement",
  "espresso machine backflush routine",
  "heat pump filter maintenance",
  "family passport renewal checklist",
];

function signInAsSovereign() {
  const res = login(SOVEREIGN.userName, SOVEREIGN.password, "setup");
  if (res.status !== 200) {
    fail(
      `setup: the Sovereign ${SOVEREIGN.userName} could not sign in (${res.status}). Is the Testing environment seeding it?`,
    );
  }
}

function setRegistrationOpen(open) {
  const current = get(ACCESS_SETTINGS, "GET /registry/settings/access", "setup");
  if (!expectStatus(current, 200, "read access settings")) {
    fail(`setup: cannot read the access settings (${current.status})`);
  }
  const value = json(current, {}).value || {};
  const wasOpen = value.registrationOpen === true;
  if (wasOpen !== open) {
    const res = put(ACCESS_SETTINGS, { ...value, registrationOpen: open }, "PUT /registry/settings/access", "setup");
    expectStatus(res, 200, "update access settings");
  }
  return wasOpen;
}

function registerUsers(count) {
  for (let start = 0; start < count; start += REGISTER_BATCH) {
    const requests = [];
    for (let i = start; i < Math.min(count, start + REGISTER_BATCH); i++) {
      requests.push([
        "POST",
        url("/api/identity/account/register"),
        JSON.stringify({ userName: loadUserName(i), password: LOAD_USER_PASSWORD }),
        {
          headers: { "Content-Type": "application/json" },
          tags: { name: "POST /account/register", type: "setup" },
          responseCallback: REGISTERED_OR_EXISTS,
        },
      ]);
    }
    // 400 means the user already exists from an earlier run, which is fine: sign-in proves the password below.
    for (const res of http.batch(requests)) {
      if (res.status !== 200 && res.status !== 400) {
        fail(`setup: registering a load user failed (${res.status} ${res.body})`);
      }
    }
  }
}

function findLocation(nodes) {
  for (const node of nodes || []) {
    if (node.name === LOCATION_NAME) {
      return node.id;
    }
    const nested = findLocation(node.children);
    if (nested) {
      return nested;
    }
  }
  return null;
}

function ensureLocation() {
  const existing = findLocation(json(get("/api/locus/locations", "GET /locus/locations", "setup"), []));
  if (existing) {
    return existing;
  }
  const res = post(
    "/api/locus/locations",
    { name: LOCATION_NAME, description: "Created by the load suite.", accessNotes: null, parentLocationId: null },
    "POST /locus/locations",
    "setup",
  );
  if (!expectStatus(res, [200, 201], "create load location")) {
    fail(`setup: cannot create the load location (${res.status} ${res.body})`);
  }
  return json(res, {}).id;
}

function ingestCorpus() {
  // Re-ingesting under the same source would duplicate chunks; start from a clean corpus.
  del(`/api/codex/ingestion?source=${KB_SOURCE}`, "DELETE /codex/ingestion", "setup");

  for (let start = 0; start < KB_DOCUMENTS; start += INGEST_BATCH) {
    const documents = [];
    for (let i = start; i < Math.min(KB_DOCUMENTS, start + INGEST_BATCH); i++) {
      const topic = TOPICS[i % TOPICS.length];
      documents.push({
        content: `Household note ${i + 1} about ${topic}. ` + `Steps, parts and reminders for ${topic}. `.repeat(12),
        source: KB_SOURCE,
        sourceType: CUSTOM_SOURCE_TYPE,
        metadata: { title: `Load note ${i + 1}`, topic },
      });
    }
    const res = post("/api/codex/ingestion/batch", documents, "POST /codex/ingestion/batch", "setup", {
      timeout: "120s",
    });
    if (!expectStatus(res, 202, "ingest corpus")) {
      console.warn(
        `setup: ingesting the corpus answered ${res.status} ${res.error || ""} ${String(res.body).slice(0, 300)}`,
      );
    }
  }
}

export function seed(userCount) {
  signInAsSovereign();
  const registrationWasOpen = setRegistrationOpen(true);
  registerUsers(userCount);
  // Registering signs the new account in, replacing the Sovereign's cookie in this jar.
  http.cookieJar().clear(url("/"));
  signInAsSovereign();
  const locationId = ensureLocation();
  ingestCorpus();

  // Prove the accounts work before any VU starts, so a bad password fails setup instead of every iteration.
  http.cookieJar().clear(url("/"));
  const probe = login(loadUserName(0), LOAD_USER_PASSWORD, "setup");
  if (probe.status !== 200) {
    fail(`setup: load users cannot sign in (${probe.status}); an earlier run may have used another LOAD_USER_PASSWORD`);
  }

  return { userCount, locationId, registrationWasOpen };
}

export function unseed(data) {
  http.cookieJar().clear(url("/"));
  signInAsSovereign();
  if (!data.registrationWasOpen) {
    setRegistrationOpen(false);
  }
  del(`/api/codex/ingestion?source=${KB_SOURCE}`, "DELETE /codex/ingestion", "setup");
  if (data.locationId) {
    del(`/api/locus/locations/${data.locationId}`, "DELETE /locus/locations/{id}", "setup");
  }
}
