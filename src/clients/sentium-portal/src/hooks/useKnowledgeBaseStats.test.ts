import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import { useKnowledgeBaseStats } from "./useKnowledgeBaseStats";
import * as agentRuntimeService from "../services/agentRuntime.service";
import type { KnowledgeBaseCollectionStats } from "../types/agentConfig";

vi.mock("../services/agentRuntime.service", () => ({
  fetchKnowledgeBaseStats: vi.fn(),
}));

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false, retryDelay: 0 }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const mockStats: KnowledgeBaseCollectionStats[] = [
  { collectionName: "sentium_rag", pointCount: 1240, vectorSize: 768, distanceMetric: "Cosine" },
  { collectionName: "sentium_agents", pointCount: 85, vectorSize: 768, distanceMetric: "Cosine" },
];

beforeEach(() => {
  vi.mocked(agentRuntimeService.fetchKnowledgeBaseStats).mockResolvedValue(mockStats);
});

describe("useKnowledgeBaseStats fetching", () => {
  it("starts with an empty collections array while loading", () => {
    const { result } = renderHook(() => useKnowledgeBaseStats(), { wrapper: createWrapper() });
    expect(result.current.collections).toEqual([]);
    expect(result.current.isLoading).toBe(true);
  });

  it("populates collections after fetch resolves", async () => {
    const { result } = renderHook(() => useKnowledgeBaseStats(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.collections).toEqual(mockStats);
    expect(result.current.collections).toHaveLength(2);
  });

  it("calls fetchKnowledgeBaseStats exactly once on mount", async () => {
    const spy = vi.spyOn(agentRuntimeService, "fetchKnowledgeBaseStats");
    const { result } = renderHook(() => useKnowledgeBaseStats(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(spy).toHaveBeenCalledTimes(1);
  });

  it("exposes error when fetch fails and falls back to empty array", async () => {
    vi.mocked(agentRuntimeService.fetchKnowledgeBaseStats).mockRejectedValue(new Error("Qdrant unavailable"));
    const { result } = renderHook(() => useKnowledgeBaseStats(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.error).not.toBeNull());
    expect(result.current.collections).toEqual([]);
  });

  it("exposes a refetch function", () => {
    const { result } = renderHook(() => useKnowledgeBaseStats(), { wrapper: createWrapper() });
    expect(typeof result.current.refetch).toBe("function");
  });

  it("exposes correct pointCount values for each collection", async () => {
    const { result } = renderHook(() => useKnowledgeBaseStats(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    const rag = result.current.collections.find((c) => c.collectionName === "sentium_rag");
    expect(rag?.pointCount).toBe(1240);
  });
});
