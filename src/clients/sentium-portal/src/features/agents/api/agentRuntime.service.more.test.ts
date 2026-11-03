import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import * as agentsService from "./agents.service";
import * as assistantService from "@/features/assistant/api/assistant.service";
import * as knowledgeBaseService from "@/features/knowledge-base/api/knowledge-base.service";
import * as modelsService from "@/features/models/api/models.service";
import * as orchestrationService from "@/features/orchestration/api/orchestration.service";
import * as semanticMapService from "@/features/semantic-map/api/semantic-map.service";
import * as skillsService from "@/features/skills/api/skills.service";
import * as workflowsService from "@/features/workflows/api/workflows.service";
import * as workspacesService from "@/features/workspaces/api/workspaces.service";
import { client } from "@/shared/api/client";

vi.mock("@/shared/api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api/client")>();
  return {
    ...actual,
    client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  };
});

const okResponse = (body: unknown) => ({ ok: true, status: 200, json: async () => body }) as unknown as Response;

beforeEach(() => {
  vi.clearAllMocks();
  vi.stubGlobal("fetch", vi.fn());
});

afterEach(() => {
  vi.unstubAllGlobals();
});

const svc = {
  ...agentsService,
  ...assistantService,
  ...knowledgeBaseService,
  ...modelsService,
  ...orchestrationService,
  ...semanticMapService,
  ...skillsService,
  ...workflowsService,
  ...workspacesService,
};

describe("agentRuntime.service client endpoints", () => {
  it("fetchModels maps model objects to names", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([{ name: "gemma" }, { name: "qwen" }]);
    expect(await svc.fetchModels()).toEqual(["gemma", "qwen"]);
    expect(client.get).toHaveBeenCalledWith("/praxis/models");
  });

  it("deleteOllamaModel encodes the model name", async () => {
    vi.mocked(client.delete).mockResolvedValueOnce({} as never);
    await svc.deleteOllamaModel("a/b");
    expect(client.delete).toHaveBeenCalledWith("/praxis/models?name=a%2Fb");
  });

  it("workflow CRUD hits the right endpoints", async () => {
    vi.mocked(client.post).mockResolvedValue({} as never);
    vi.mocked(client.put).mockResolvedValue({} as never);
    vi.mocked(client.delete).mockResolvedValue(undefined as never);
    await svc.createWorkflow({ name: "w" } as never);
    await svc.updateWorkflow({ id: "1", name: "w" } as never);
    await svc.deleteWorkflow("1");
    expect(client.post).toHaveBeenCalledWith("/praxis/workflows", { name: "w" });
    expect(client.put).toHaveBeenCalledWith("/praxis/workflows/1", { name: "w" });
    expect(client.delete).toHaveBeenCalledWith("/praxis/workflows/1");
  });

  it("workflow runs use a paged query and id path", async () => {
    vi.mocked(client.get).mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 5, totalPages: 0 } as never);
    await svc.fetchWorkflowRuns(5);
    await svc.fetchWorkflowRun("r1");
    expect(client.get).toHaveBeenCalledWith("/praxis/workflows/runs?page=1&pageSize=5");
    expect(client.get).toHaveBeenCalledWith("/praxis/workflows/runs/r1");
  });

  it("conversation CRUD hits the right endpoints", async () => {
    vi.mocked(client.get).mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 20,
      totalPages: 0,
    } as never);
    vi.mocked(client.post).mockResolvedValue({} as never);
    vi.mocked(client.delete).mockResolvedValue(undefined as never);
    await svc.fetchConversations();
    await svc.fetchConversation("c1");
    await svc.createConversation({ title: "t" } as never);
    await svc.deleteConversation("c1");
    expect(client.get).toHaveBeenCalledWith("/praxis/conversations?page=1&pageSize=20");
    expect(client.get).toHaveBeenCalledWith("/praxis/conversations/c1");
    expect(client.delete).toHaveBeenCalledWith("/praxis/conversations/c1");
  });

  it("orchestration triggers post to their endpoints", async () => {
    vi.mocked(client.post).mockResolvedValue({ eventId: "e" } as never);
    await svc.runDynamicWorkflow({ activity: "x" });
    await svc.runWorkflowPipeline({ workflowId: "w", scenario: "s" } as never);
    expect(client.post).toHaveBeenCalledWith("/praxis/orchestration/run-dynamic-workflow", { activity: "x" });
    expect(client.post).toHaveBeenCalledWith("/praxis/orchestration/run-workflow", {
      workflowId: "w",
      scenario: "s",
    });
  });

  it("workspace endpoints build correct URLs", async () => {
    vi.mocked(client.get).mockResolvedValue([] as never);
    vi.mocked(client.delete).mockResolvedValue(undefined as never);
    await svc.listWorkspaceFiles();
    await svc.listWorkspaceFiles("w 1");
    await svc.fetchWorkspaceFiles("w1");
    await svc.deleteWorkspaceFile("f1");
    expect(client.get).toHaveBeenCalledWith("/praxis/workspaces/files");
    expect(client.get).toHaveBeenCalledWith("/praxis/workspaces/files?workspaceId=w%201");
    expect(client.get).toHaveBeenCalledWith("/praxis/workspaces/w1/files");
    expect(client.delete).toHaveBeenCalledWith("/praxis/workspaces/files/f1");
  });

  it("agent-learning endpoints build correct URLs", async () => {
    vi.mocked(client.get).mockResolvedValue([] as never);
    vi.mocked(client.put).mockResolvedValue({} as never);
    vi.mocked(client.delete).mockResolvedValue(undefined as never);
    await svc.fetchAgentLearnings();
    await svc.fetchAgentLearnings("Analyzer", 2, 10);
    await svc.fetchAgentLearningStats();
    await svc.updateAgentLearning("l1", { content: "c", tags: "t" });
    await svc.deleteAgentLearning("l1");
    expect(client.get).toHaveBeenCalledWith("/praxis/agent-learnings?page=1&pageSize=20");
    expect(client.get).toHaveBeenCalledWith("/praxis/agent-learnings?page=2&pageSize=10&agentName=Analyzer");
    expect(client.get).toHaveBeenCalledWith("/praxis/agent-learnings/stats");
    expect(client.put).toHaveBeenCalledWith("/praxis/agent-learnings/l1", { content: "c", tags: "t" });
  });

  it("knowledge-base and knowledge-map endpoints build correct URLs", async () => {
    vi.mocked(client.get).mockResolvedValue([] as never);
    vi.mocked(client.post).mockResolvedValue({} as never);
    vi.mocked(client.delete).mockResolvedValue(undefined as never);
    await svc.fetchKnowledgeBaseStats();
    await svc.deleteKnowledgeMapCollection("user memories");
    await svc.fetchKnowledgeMapNodes(100, "knowledge_base");
    await svc.searchKnowledgeMap("query", 5);
    expect(client.get).toHaveBeenCalledWith("/codex/knowledge-base/stats");
    expect(client.delete).toHaveBeenCalledWith("/codex/knowledge-base/collections/user%20memories");
    expect(client.get).toHaveBeenCalledWith("/codex/knowledge-map/nodes?limit=100&collection=knowledge_base");
    expect(client.post).toHaveBeenCalledWith("/codex/knowledge-map/search", { query: "query", topK: 5 });
  });

  it("skill CRUD hits the right endpoints", async () => {
    vi.mocked(client.get).mockResolvedValue([] as never);
    vi.mocked(client.post).mockResolvedValue({} as never);
    vi.mocked(client.put).mockResolvedValue(undefined as never);
    vi.mocked(client.delete).mockResolvedValue(undefined as never);
    await svc.fetchBuiltInSkills();
    await svc.fetchSkillsPaged();
    await svc.createSkill({ name: "s" } as never);
    await svc.updateSkill("s1", { name: "s" } as never);
    await svc.deleteSkill("s1");
    expect(client.get).toHaveBeenCalledWith("/praxis/skills/built-in");
    expect(client.put).toHaveBeenCalledWith("/praxis/skills/s1", { name: "s" });
    expect(client.delete).toHaveBeenCalledWith("/praxis/skills/s1");
  });
});

describe("agentRuntime.service fetch-based endpoints", () => {
  it("pullModel posts to the pull endpoint", async () => {
    vi.mocked(fetch as unknown as ReturnType<typeof vi.fn>).mockResolvedValueOnce(okResponse({}));
    await svc.pullModel("gemma");
    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining("/praxis/models/pull"),
      expect.objectContaining({ method: "POST" }),
    );
  });

  it("sendChatMessage and approveToolCall post to the assistant endpoints", async () => {
    vi.mocked(fetch as unknown as ReturnType<typeof vi.fn>).mockResolvedValue(okResponse({}));
    await svc.sendChatMessage({ messages: [] } as never);
    await svc.approveToolCall("req-1", true);
    expect(fetch).toHaveBeenCalledWith(expect.stringContaining("/assistant/chat"), expect.any(Object));
    expect(fetch).toHaveBeenCalledWith(expect.stringContaining("/assistant/chat/approve"), expect.any(Object));
  });

  it("uploadWorkspaceFile returns the created file on success", async () => {
    const file = new File(["x"], "a.txt");
    vi.mocked(fetch as unknown as ReturnType<typeof vi.fn>).mockResolvedValueOnce(okResponse({ id: "f1" }));
    const result = await svc.uploadWorkspaceFile(file, "w1");
    expect(result).toEqual({ id: "f1" });
  });

  it("uploadWorkspaceFile throws the API error message on failure", async () => {
    vi.mocked(fetch as unknown as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: false,
      status: 500,
      json: async () => ({ error: "disk full" }),
    } as unknown as Response);
    await expect(svc.uploadWorkspaceFile(new File(["x"], "a.txt"))).rejects.toThrow("disk full");
  });

  it("uploadSkillFile redirects to login on 401", async () => {
    vi.mocked(fetch as unknown as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: false,
      status: 401,
      json: async () => ({}),
    } as unknown as Response);
    await expect(svc.uploadSkillFile(new File(["x"], "s.md"))).rejects.toThrow("Session expired.");
    expect(window.location.href).toContain("/bff/login");
  });

  it("uploadSkillFile returns the created skill on success", async () => {
    vi.mocked(fetch as unknown as ReturnType<typeof vi.fn>).mockResolvedValueOnce(okResponse({ id: "s1" }));
    expect(await svc.uploadSkillFile(new File(["x"], "s.md"))).toEqual({ id: "s1" });
  });
});
