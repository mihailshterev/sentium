import { describe, it, expect, vi, beforeEach } from "vitest";
import * as workflowsService from "./workflows.service";
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

describe("workflows.service", () => {
  it("fetchWorkflowsPaged calls client.get with correct path", async () => {
    vi.mocked(client.get).mockResolvedValueOnce({ items: [], totalCount: 0, page: 1, pageSize: 100, totalPages: 0 });
    await workflowsService.fetchWorkflowsPaged();
    expect(client.get).toHaveBeenCalledWith("/praxis/workflows?page=1&pageSize=100");
  });

  it("createWorkflow posts to /workflows", async () => {
    const payload = { name: "Pipeline", description: "desc", agents: [] };
    vi.mocked(client.post).mockResolvedValueOnce({});
    await workflowsService.createWorkflow(payload);
    expect(client.post).toHaveBeenCalledWith("/praxis/workflows", payload);
  });

  it("updateWorkflow puts to /workflows/:id without the id in the body", async () => {
    vi.mocked(client.put).mockResolvedValueOnce({});
    await workflowsService.updateWorkflow({ id: "wf-1", name: "Pipeline v2", description: "desc", agents: [] });
    expect(client.put).toHaveBeenCalledWith("/praxis/workflows/wf-1", {
      name: "Pipeline v2",
      description: "desc",
      agents: [],
    });
  });

  it("deleteWorkflow deletes by id", async () => {
    vi.mocked(client.delete).mockResolvedValueOnce(undefined);
    await workflowsService.deleteWorkflow("wf-1");
    expect(client.delete).toHaveBeenCalledWith("/praxis/workflows/wf-1");
  });

  it("fetchWorkflowRuns uses a paged query", async () => {
    vi.mocked(client.get).mockResolvedValueOnce({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 5,
      totalPages: 0,
    } as never);
    await workflowsService.fetchWorkflowRuns(5);
    expect(client.get).toHaveBeenCalledWith("/praxis/workflows/runs?page=1&pageSize=5");
  });

  it("fetchWorkflowRun calls client.get with run id path", async () => {
    vi.mocked(client.get).mockResolvedValueOnce({});
    await workflowsService.fetchWorkflowRun("run-1");
    expect(client.get).toHaveBeenCalledWith("/praxis/workflows/runs/run-1");
  });

  it("fetchWorkflowRunsPaged adds an encoded search term", async () => {
    vi.mocked(client.get).mockResolvedValueOnce({ items: [] });
    await workflowsService.fetchWorkflowRunsPaged(1, 20, "backup 100%");
    expect(client.get).toHaveBeenCalledWith("/praxis/workflows/runs?page=1&pageSize=20&search=backup%20100%25");
  });

  it("renameWorkflowRun puts the title to the run's title path", async () => {
    vi.mocked(client.put).mockResolvedValueOnce(undefined);
    await workflowsService.renameWorkflowRun("run-1", "Backup audit");
    expect(client.put).toHaveBeenCalledWith("/praxis/workflows/runs/run-1/title", { title: "Backup audit" });
  });
});
