import { describe, it, expect, vi, beforeEach } from "vitest";
import * as agentsService from "./agents.service";
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

describe("agents.service", () => {
  it("fetchAgentsPaged calls client.get with correct path", async () => {
    vi.mocked(client.get).mockResolvedValueOnce({ items: [], totalCount: 0, page: 1, pageSize: 100, totalPages: 0 });
    await agentsService.fetchAgentsPaged();
    expect(client.get).toHaveBeenCalledWith("/praxis/agents?page=1&pageSize=100");
  });

  it("createAgent posts payload to correct path", async () => {
    const payload = { name: "Bot", description: "AI bot", model: "llama3" };
    vi.mocked(client.post).mockResolvedValueOnce({});
    await agentsService.createAgent(payload);
    expect(client.post).toHaveBeenCalledWith("/praxis/agents", payload);
  });

  it("updateAgent puts to /agents/:id", async () => {
    const payload = { id: "a-1", name: "Bot v2", description: "Updated", model: "llama3" };
    vi.mocked(client.put).mockResolvedValueOnce({});
    await agentsService.updateAgent(payload);
    expect(client.put).toHaveBeenCalledWith("/praxis/agents/a-1", expect.objectContaining({ id: "a-1" }));
  });

  it("deleteAgent calls client.delete with id", async () => {
    vi.mocked(client.delete).mockResolvedValueOnce(undefined);
    await agentsService.deleteAgent("a-1");
    expect(client.delete).toHaveBeenCalledWith("/praxis/agents/a-1");
  });
});
