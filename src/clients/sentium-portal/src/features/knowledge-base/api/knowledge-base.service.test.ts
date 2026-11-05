import { describe, it, expect, vi, beforeEach } from "vitest";
import * as knowledgeBaseService from "./knowledge-base.service";
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

describe("knowledge-base.service agent learnings", () => {
  it("fetchAgentLearnings uses the default paging without an agentName filter", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([]);
    await knowledgeBaseService.fetchAgentLearnings();
    expect(client.get).toHaveBeenCalledWith("/praxis/agent-learnings?page=1&pageSize=20");
  });

  it("fetchAgentLearnings adds paging and the agentName filter when provided", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([]);
    await knowledgeBaseService.fetchAgentLearnings("Analyzer", 2, 10);
    expect(client.get).toHaveBeenCalledWith("/praxis/agent-learnings?page=2&pageSize=10&agentName=Analyzer");
  });

  it("fetchAgentLearningStats calls the stats endpoint", async () => {
    vi.mocked(client.get).mockResolvedValueOnce({});
    await knowledgeBaseService.fetchAgentLearningStats();
    expect(client.get).toHaveBeenCalledWith("/praxis/agent-learnings/stats");
  });

  it("updateAgentLearning puts content and tags", async () => {
    vi.mocked(client.put).mockResolvedValueOnce({} as never);
    await knowledgeBaseService.updateAgentLearning("l1", { content: "c", tags: "t" });
    expect(client.put).toHaveBeenCalledWith("/praxis/agent-learnings/l1", { content: "c", tags: "t" });
  });

  it("deleteAgentLearning deletes by id", async () => {
    vi.mocked(client.delete).mockResolvedValueOnce(undefined);
    await knowledgeBaseService.deleteAgentLearning("learn-1");
    expect(client.delete).toHaveBeenCalledWith("/praxis/agent-learnings/learn-1");
  });
});

describe("knowledge-base.service Codex endpoints", () => {
  it("fetchKnowledgeBaseStats calls correct endpoint", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([]);
    await knowledgeBaseService.fetchKnowledgeBaseStats();
    expect(client.get).toHaveBeenCalledWith("/codex/knowledge-base/stats");
  });

  it("deleteKnowledgeMapCollection encodes the collection name", async () => {
    vi.mocked(client.delete).mockResolvedValueOnce(undefined as never);
    await knowledgeBaseService.deleteKnowledgeMapCollection("user memories");
    expect(client.delete).toHaveBeenCalledWith("/codex/knowledge-base/collections/user%20memories");
  });
});
