import { describe, it, expect, vi, beforeEach } from "vitest";
import * as schedulerService from "./scheduler.service";
import { client } from "../api/client";

vi.mock("../api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../api/client")>();
  return {
    ...actual,
    client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  };
});

beforeEach(() => {
  vi.clearAllMocks();
});

describe("fetchActiveSchedulerJobs()", () => {
  it("requests the scheduler jobs endpoint and returns the response", async () => {
    const jobs = [{ id: "j1" }] as never;
    vi.mocked(client.get).mockResolvedValueOnce(jobs);

    const result = await schedulerService.fetchActiveSchedulerJobs();

    expect(client.get).toHaveBeenCalledWith("/agent-runtime/scheduler/jobs");
    expect(result).toBe(jobs);
  });
});

describe("deleteScheduledJob()", () => {
  it("deletes with URL-encoded agent and job ids", async () => {
    vi.mocked(client.delete).mockResolvedValueOnce(undefined);

    await schedulerService.deleteScheduledJob("agent 1", "job/2");

    expect(client.delete).toHaveBeenCalledWith("/agent-runtime/scheduler/agents/agent%201/jobs/job%2F2");
  });
});
