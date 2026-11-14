import { beforeEach, describe, expect, it, vi } from "vitest";
import { client } from "@/shared/api/client";
import type { SaveTaskPayload } from "../types/timeline";
import * as api from "./timeline.service";

vi.mock("@/shared/api/client", async (importOriginal) => ({
  ...(await importOriginal()),
  client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn(), postForm: vi.fn() },
}));

describe("timeline service", () => {
  beforeEach(() => vi.clearAllMocks());

  it("reads the year with the viewer's date, and a month", async () => {
    await api.fetchYear(2026, "2026-11-08");
    await api.fetchYear(2027);
    await api.fetchMonth(2026, 11);

    expect(client.get).toHaveBeenNthCalledWith(1, "/tempus/years/2026?today=2026-11-08");
    expect(client.get).toHaveBeenNthCalledWith(2, "/tempus/years/2027");
    expect(client.get).toHaveBeenNthCalledWith(3, "/tempus/years/2026/months/11");
  });

  it("writes tasks under /tempus/tasks", async () => {
    const payload: SaveTaskPayload = {
      kind: "Task",
      title: "Plan",
      description: null,
      year: 2026,
      month: 11,
      date: null,
      startTime: null,
      endDate: null,
      dueDate: null,
      priority: "Normal",
      status: "Todo",
      tags: [],
      assignedAgent: null,
      repeat: null,
      reminderOffsetMinutes: null,
    };
    await api.createTask(payload);
    await api.updateTask("t 1", payload);
    await api.scheduleTask("t1", { year: 2026, month: 12, date: "2026-12-01" });
    await api.setTaskStatus("t1", "Done");
    await api.updateTask("t1", payload, "Following");
    await api.deleteTask("t1");
    await api.deleteTask("t2", "Following");

    expect(client.post).toHaveBeenCalledWith("/tempus/tasks", payload);
    expect(client.put).toHaveBeenCalledWith("/tempus/tasks/t%201?scope=One", payload);
    expect(client.put).toHaveBeenCalledWith("/tempus/tasks/t1?scope=Following", payload);
    expect(client.put).toHaveBeenCalledWith("/tempus/tasks/t1/schedule", { year: 2026, month: 12, date: "2026-12-01" });
    expect(client.put).toHaveBeenCalledWith("/tempus/tasks/t1/status", { status: "Done" });
    expect(client.delete).toHaveBeenCalledWith("/tempus/tasks/t1?scope=One");
    expect(client.delete).toHaveBeenCalledWith("/tempus/tasks/t2?scope=Following");
  });

  it("answers proposals and manages notes", async () => {
    await api.fetchProposals();
    await api.acceptProposal("p1");
    await api.rejectProposal("p2");
    await api.createNote({ year: 2026, month: 11, date: null, taskId: null, content: "Hi" });
    await api.deleteNote("n1");

    expect(client.get).toHaveBeenCalledWith("/tempus/proposals?status=Pending");
    expect(client.post).toHaveBeenCalledWith("/tempus/proposals/p1/accept", undefined);
    expect(client.post).toHaveBeenCalledWith("/tempus/proposals/p2/reject", undefined);
    expect(client.post).toHaveBeenCalledWith("/tempus/notes", expect.objectContaining({ content: "Hi" }));
    expect(client.delete).toHaveBeenCalledWith("/tempus/notes/n1");
  });
});
