// CRUD round trips that clean up after themselves: an inventory asset, a timeline task and a conversation.
import exec from "k6/execution";
import { del, expectStatus, get, json, post, put } from "../lib/http.js";
import { ensureSignedIn } from "../lib/session.js";
import { pick, think } from "../lib/think.js";

function stamp() {
  return `${exec.vu.idInTest}-${exec.vu.iterationInScenario}`;
}

function assetRoundTrip(locationId) {
  const body = { displayName: `Load drill ${stamp()}`, locationId, category: "Tools", tags: ["load-test"] };
  const created = post("/api/locus/assets", body, "POST /locus/assets");
  if (!expectStatus(created, 201, "create asset")) {
    return;
  }
  const id = json(created, {}).id;

  think(0.5, 1.5);
  expectStatus(
    put(`/api/locus/assets/${id}`, { ...body, physicalDescription: "Blue, 18V" }, "PUT /locus/assets/{id}"),
    200,
    "update asset",
  );
  expectStatus(get(`/api/locus/assets/${id}`, "GET /locus/assets/{id}"), 200, "read asset");
  expectStatus(del(`/api/locus/assets/${id}`, "DELETE /locus/assets/{id}"), [200, 204], "delete asset");
}

function taskRoundTrip() {
  const now = new Date();
  const body = {
    title: `Load task ${stamp()}`,
    description: "Created by the load suite.",
    year: now.getUTCFullYear(),
    month: now.getUTCMonth() + 1,
    date: null,
    dueDate: null,
    tags: ["load-test"],
  };
  const created = post("/api/tempus/tasks", body, "POST /tempus/tasks");
  if (!expectStatus(created, 201, "create task")) {
    return;
  }
  const id = json(created, {}).id;

  think(0.5, 1.5);
  expectStatus(
    put(`/api/tempus/tasks/${id}`, { ...body, title: `${body.title} (edited)` }, "PUT /tempus/tasks/{id}"),
    200,
    "update task",
  );
  expectStatus(del(`/api/tempus/tasks/${id}`, "DELETE /tempus/tasks/{id}"), [200, 204], "delete task");
}

function conversationRoundTrip(model) {
  const created = post(
    "/api/praxis/conversations",
    { title: `Load chat ${stamp()}`, model },
    "POST /praxis/conversations",
  );
  if (!expectStatus(created, 201, "create conversation")) {
    return;
  }
  const id = json(created, {}).id;

  think(0.5, 1.5);
  expectStatus(
    put(
      `/api/praxis/conversations/${id}/title`,
      { title: "Renamed by the load suite" },
      "PUT /praxis/conversations/{id}/title",
    ),
    [200, 204],
    "rename conversation",
  );
  expectStatus(
    put(`/api/praxis/conversations/${id}/pinned`, { pinned: true }, "PUT /praxis/conversations/{id}/pinned"),
    [200, 204],
    "pin conversation",
  );
  expectStatus(
    del(`/api/praxis/conversations/${id}`, "DELETE /praxis/conversations/{id}"),
    [200, 204],
    "delete conversation",
  );
}

export function write(data) {
  ensureSignedIn(data.userCount);

  const action = pick(["asset", "task", "conversation"]);
  if (action === "asset") {
    assetRoundTrip(data.locationId);
  } else if (action === "task") {
    taskRoundTrip();
  } else {
    conversationRoundTrip(data.model);
  }

  think(2, 5);
}
