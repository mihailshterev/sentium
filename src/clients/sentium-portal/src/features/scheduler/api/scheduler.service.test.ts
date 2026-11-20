import { describe, it, expect, vi, beforeEach } from "vitest";
import * as schedulerService from "./scheduler.service";
import { client } from "@/shared/api/client";

vi.mock("@/shared/api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api/client")>();
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

    expect(client.get).toHaveBeenCalledWith("/praxis/scheduler/jobs");
    expect(result).toBe(jobs);
  });
});

describe("deleteSchedule()", () => {
  it("deletes by URL-encoded job id", async () => {
    vi.mocked(client.delete).mockResolvedValueOnce(undefined);

    await schedulerService.deleteSchedule("job/2");

    expect(client.delete).toHaveBeenCalledWith("/praxis/scheduler/jobs/job%2F2");
  });
});

describe("schedule mutations", () => {
  const payload = { name: "Briefing", kind: "Agent" as const, cronExpression: "0 30 7 ? * *", agentName: "A", prompt: "p" };

  it("creates with POST and edits with PUT", async () => {
    await schedulerService.createSchedule(payload);
    await schedulerService.updateSchedule("s1", payload);

    expect(client.post).toHaveBeenCalledWith("/praxis/scheduler/jobs", payload);
    expect(client.put).toHaveBeenCalledWith("/praxis/scheduler/jobs/s1", payload);
  });

  it("pauses and resumes", async () => {
    await schedulerService.pauseSchedule("s1");
    await schedulerService.resumeSchedule("s1");

    expect(client.post).toHaveBeenCalledWith("/praxis/scheduler/jobs/s1/pause", null);
    expect(client.post).toHaveBeenCalledWith("/praxis/scheduler/jobs/s1/resume", null);
  });
});

describe("fetchPreApprovableTools()", () => {
  it("passes only the fields of the target that are set", async () => {
    await schedulerService.fetchPreApprovableTools({ kind: "Agent", agentName: "Timeline Planner" });

    expect(client.get).toHaveBeenCalledWith("/praxis/scheduler/pre-approvable-tools?kind=Agent&agentName=Timeline+Planner");
  });

  it("includes the job when editing", async () => {
    await schedulerService.fetchPreApprovableTools({ kind: "Workflow", workflowId: "w1", jobId: "s1" });

    expect(client.get).toHaveBeenCalledWith("/praxis/scheduler/pre-approvable-tools?kind=Workflow&workflowId=w1&jobId=s1");
  });
});

describe("fetchBackgroundJobs()", () => {
  it("requests the background jobs endpoint", async () => {
    await schedulerService.fetchBackgroundJobs();

    expect(client.get).toHaveBeenCalledWith("/praxis/scheduler/background-jobs");
  });
});
