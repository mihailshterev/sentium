import { describe, it, expect, vi, beforeEach } from "vitest";
import * as semanticMapService from "./semantic-map.service";
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

describe("semantic-map.service", () => {
  it("fetchKnowledgeMapNodes passes the limit and collection", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([] as never);
    await semanticMapService.fetchKnowledgeMapNodes(100, "knowledge_base");
    expect(client.get).toHaveBeenCalledWith("/codex/knowledge-map/nodes?limit=100&collection=knowledge_base");
  });

  it("searchKnowledgeMap posts the query and topK", async () => {
    vi.mocked(client.post).mockResolvedValueOnce({} as never);
    await semanticMapService.searchKnowledgeMap("query", 5);
    expect(client.post).toHaveBeenCalledWith("/codex/knowledge-map/search", { query: "query", topK: 5 });
  });
});
