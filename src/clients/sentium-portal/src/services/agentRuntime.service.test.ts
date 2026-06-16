import { describe, it, expect, vi, beforeEach } from "vitest";
import * as service from "./agentRuntime.service";
import { client } from "../api/client";
import { BASE_URL } from "../api/client";

vi.mock("../api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../api/client")>();
  return {
    ...actual,
    client: {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
    },
  };
});

beforeEach(() => {
  vi.stubGlobal("fetch", vi.fn());
});

describe("agentRuntime.service agents", () => {
  it("fetchAgentsPaged calls client.get with correct path", async () => {
    vi.mocked(client.get).mockResolvedValueOnce({ items: [], totalCount: 0, page: 1, pageSize: 100, totalPages: 0 });
    await service.fetchAgentsPaged();
    expect(client.get).toHaveBeenCalledWith("/agent-runtime/agents?page=1&pageSize=100");
  });

  it("createAgent posts payload to correct path", async () => {
    const payload = { name: "Bot", description: "AI bot", model: "llama3" };
    vi.mocked(client.post).mockResolvedValueOnce({});
    await service.createAgent(payload);
    expect(client.post).toHaveBeenCalledWith("/agent-runtime/agents", payload);
  });

  it("updateAgent puts to /agents/:id", async () => {
    const payload = { id: "a-1", name: "Bot v2", description: "Updated", model: "llama3" };
    vi.mocked(client.put).mockResolvedValueOnce({});
    await service.updateAgent(payload);
    expect(client.put).toHaveBeenCalledWith("/agent-runtime/agents/a-1", expect.objectContaining({ id: "a-1" }));
  });

  it("deleteAgent calls client.delete with id", async () => {
    vi.mocked(client.delete).mockResolvedValueOnce(undefined);
    await service.deleteAgent("a-1");
    expect(client.delete).toHaveBeenCalledWith("/agent-runtime/agents/a-1");
  });
});

describe("agentRuntime.service models", () => {
  it("fetchModels maps OllamaModel[] to name strings", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([
      { name: "llama3", modified_at: "", size: 0, digest: "", details: {} },
      { name: "mistral", modified_at: "", size: 0, digest: "", details: {} },
    ]);
    const models = await service.fetchModels();
    expect(models).toEqual(["llama3", "mistral"]);
  });

  it("fetchOllamaModels calls client.get with correct path", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([]);
    await service.fetchOllamaModels();
    expect(client.get).toHaveBeenCalledWith("/agent-runtime/models");
  });

  it("deleteOllamaModel encodes model name in URL", async () => {
    vi.mocked(client.delete).mockResolvedValueOnce({});
    await service.deleteOllamaModel("llama3:latest");
    expect(client.delete).toHaveBeenCalledWith(`/agent-runtime/models?name=${encodeURIComponent("llama3:latest")}`);
  });
});

describe("agentRuntime.service pullModel()", () => {
  it("calls fetch with POST to correct URL", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({ ok: true } as unknown as Response);
    await service.pullModel("llama3:latest");
    expect(fetch).toHaveBeenCalledWith(
      `${BASE_URL}/agent-runtime/models/pull`,
      expect.objectContaining({ method: "POST" }),
    );
  });

  it("serialises model name in body", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({ ok: true } as unknown as Response);
    await service.pullModel("mistral");
    const [, config] = vi.mocked(fetch).mock.calls[0];
    expect((config as RequestInit).body).toBe(JSON.stringify({ name: "mistral" }));
  });

  it("forwards AbortSignal to fetch", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({ ok: true } as unknown as Response);
    const controller = new AbortController();
    await service.pullModel("llama3", controller.signal);
    const [, config] = vi.mocked(fetch).mock.calls[0];
    expect((config as RequestInit).signal).toBe(controller.signal);
  });
});

describe("agentRuntime.service workflows", () => {
  it("fetchWorkflowsPaged calls client.get with correct path", async () => {
    vi.mocked(client.get).mockResolvedValueOnce({ items: [], totalCount: 0, page: 1, pageSize: 100, totalPages: 0 });
    await service.fetchWorkflowsPaged();
    expect(client.get).toHaveBeenCalledWith("/agent-runtime/workflows?page=1&pageSize=100");
  });

  it("createWorkflow posts to /workflows", async () => {
    const payload = { name: "Pipeline", description: "desc", agents: [] };
    vi.mocked(client.post).mockResolvedValueOnce({});
    await service.createWorkflow(payload);
    expect(client.post).toHaveBeenCalledWith("/agent-runtime/workflows", payload);
  });

  it("updateWorkflow puts to /workflows/:id", async () => {
    const payload = { id: "wf-1", name: "Pipeline v2", description: "desc", agents: [] };
    vi.mocked(client.put).mockResolvedValueOnce({});
    await service.updateWorkflow(payload);
    expect(client.put).toHaveBeenCalledWith("/agent-runtime/workflows/wf-1", expect.any(Object));
  });

  it("deleteWorkflow deletes by id", async () => {
    vi.mocked(client.delete).mockResolvedValueOnce(undefined);
    await service.deleteWorkflow("wf-1");
    expect(client.delete).toHaveBeenCalledWith("/agent-runtime/workflows/wf-1");
  });

  it("fetchWorkflowRun calls client.get with run id path", async () => {
    vi.mocked(client.get).mockResolvedValueOnce({});
    await service.fetchWorkflowRun("run-1");
    expect(client.get).toHaveBeenCalledWith("/agent-runtime/workflows/runs/run-1");
  });
});

describe("agentRuntime.service conversations", () => {
  it("fetchConversations calls client.get with correct path", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([]);
    await service.fetchConversations();
    expect(client.get).toHaveBeenCalledWith("/agent-runtime/conversations?page=1&pageSize=20");
  });

  it("fetchConversation calls client.get with id in path", async () => {
    vi.mocked(client.get).mockResolvedValueOnce({});
    await service.fetchConversation("conv-1");
    expect(client.get).toHaveBeenCalledWith("/agent-runtime/conversations/conv-1");
  });

  it("createConversation posts payload", async () => {
    const payload = { title: "New Chat", model: "llama3" };
    vi.mocked(client.post).mockResolvedValueOnce({ id: "c-1" });
    await service.createConversation(payload);
    expect(client.post).toHaveBeenCalledWith("/agent-runtime/conversations", payload);
  });

  it("deleteConversation deletes by id", async () => {
    vi.mocked(client.delete).mockResolvedValueOnce(undefined);
    await service.deleteConversation("conv-1");
    expect(client.delete).toHaveBeenCalledWith("/agent-runtime/conversations/conv-1");
  });
});

describe("agentRuntime.service agent learnings", () => {
  it("fetchAgentLearnings calls correct endpoint without agentName filter", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([]);
    await service.fetchAgentLearnings();
    expect(client.get).toHaveBeenCalledWith(
      expect.stringContaining("/agent-runtime/agent-learnings?page=1&pageSize=20"),
    );
  });

  it("fetchAgentLearnings includes agentName param when provided", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([]);
    await service.fetchAgentLearnings("MyAgent");
    expect(client.get).toHaveBeenCalledWith(expect.stringContaining("agentName=MyAgent"));
  });

  it("deleteAgentLearning deletes by id", async () => {
    vi.mocked(client.delete).mockResolvedValueOnce(undefined);
    await service.deleteAgentLearning("learn-1");
    expect(client.delete).toHaveBeenCalledWith("/agent-runtime/agent-learnings/learn-1");
  });

  it("fetchKnowledgeBaseStats calls correct endpoint", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([]);
    await service.fetchKnowledgeBaseStats();
    expect(client.get).toHaveBeenCalledWith("/agent-runtime/knowledge-base/stats");
  });
});

describe("agentRuntime.service uploadWorkspaceFile()", () => {
  it("calls fetch with FormData (not JSON)", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: async () => ({ id: "file-1", name: "doc.pdf" }),
    } as unknown as Response);

    const file = new File(["content"], "doc.pdf", { type: "application/pdf" });
    await service.uploadWorkspaceFile(file);

    const [, config] = vi.mocked(fetch).mock.calls[0];
    expect((config as RequestInit).method).toBe("POST");
    expect((config as RequestInit).body).toBeInstanceOf(FormData);
  });

  it("appends workspaceId to FormData when provided", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: async () => ({}),
    } as unknown as Response);

    const file = new File(["x"], "x.txt");
    await service.uploadWorkspaceFile(file, "ws-1");

    const [, config] = vi.mocked(fetch).mock.calls[0];
    const fd = (config as RequestInit).body as FormData;
    expect(fd.get("workspaceId")).toBe("ws-1");
  });

  it("throws 'Session expired' on 401 from upload endpoint", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({
      ok: false,
      status: 401,
      json: async () => ({}),
    } as unknown as Response);

    const file = new File(["x"], "x.txt");
    await expect(service.uploadWorkspaceFile(file)).rejects.toThrow("Session expired.");
  });
});

describe("agentRuntime.service sendChatMessage()", () => {
  it("calls fetch POST to assistant/chat endpoint", async () => {
    vi.mocked(fetch).mockResolvedValueOnce({ ok: true } as unknown as Response);
    const payload = {
      conversationId: "c-1",
      model: "llama3",
      messages: [{ role: "user", content: "Hi" }],
      stream: true,
    };
    await service.sendChatMessage(payload);
    const [url, config] = vi.mocked(fetch).mock.calls[0];
    expect(url).toContain("/agent-runtime/assistant/chat");
    expect((config as RequestInit).method).toBe("POST");
  });
});
