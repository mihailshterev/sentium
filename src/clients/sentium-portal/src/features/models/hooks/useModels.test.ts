import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import { useModels } from "./useModels";
import * as modelsService from "@/features/models/api/models.service";

vi.mock("@/features/models/api/models.service", () => ({
  fetchModelCatalog: vi.fn(),
}));

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

beforeEach(() => {
  vi.mocked(modelsService.fetchModelCatalog).mockResolvedValue({
    defaultModel: "gemma-4-e4b",
    models: [
      { id: "gemma-4-e4b", status: "loaded", contextLength: 16384 },
      { id: "gemma-4-e2b", status: "unloaded", contextLength: null },
    ],
  });
});

describe("useModels", () => {
  it("starts with no models and no default", () => {
    const { result } = renderHook(() => useModels(), { wrapper: createWrapper() });
    expect(result.current.models).toEqual([]);
    expect(result.current.defaultModel).toBeNull();
  });

  it("exposes the model ids and the default model once loaded", async () => {
    const { result } = renderHook(() => useModels(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.models).toEqual(["gemma-4-e4b", "gemma-4-e2b"]));
    expect(result.current.defaultModel).toBe("gemma-4-e4b");
  });

  it("falls back to an empty list when the inference server is unavailable", async () => {
    vi.mocked(modelsService.fetchModelCatalog).mockRejectedValueOnce(new Error("Service down"));
    const { result } = renderHook(() => useModels(), { wrapper: createWrapper() });
    await new Promise((r) => setTimeout(r, 50));
    expect(result.current.models).toEqual([]);
    expect(result.current.defaultModel).toBeNull();
  });
});
