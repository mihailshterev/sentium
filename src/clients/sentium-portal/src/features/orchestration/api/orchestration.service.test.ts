import { describe, it, expect, vi, beforeEach } from "vitest";
import * as orchestrationService from "./orchestration.service";
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

describe("orchestration.service", () => {
  it("runDynamicWorkflow posts to its endpoint", async () => {
    vi.mocked(client.post).mockResolvedValueOnce({ eventId: "e" } as never);
    await orchestrationService.runDynamicWorkflow({ activity: "x" });
    expect(client.post).toHaveBeenCalledWith("/praxis/orchestration/run-dynamic-workflow", { activity: "x" });
  });

  it("runWorkflowPipeline posts to its endpoint", async () => {
    vi.mocked(client.post).mockResolvedValueOnce({ eventId: "e" } as never);
    await orchestrationService.runWorkflowPipeline({ workflowId: "w", scenario: "s" } as never);
    expect(client.post).toHaveBeenCalledWith("/praxis/orchestration/run-workflow", {
      workflowId: "w",
      scenario: "s",
    });
  });
});
