import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import { useModelCatalog } from "./useModelCatalog";
import * as modelsService from "@/features/models/api/models.service";
import { ApiError } from "@/shared/api/client";
import type { ModelsResponse } from "@/features/models/types/models";

vi.mock("@/features/models/api/models.service", () => ({
  fetchModelCatalog: vi.fn(),
  loadModel: vi.fn(),
  unloadModel: vi.fn(),
}));

const catalog: ModelsResponse = {
  defaultModel: "gemma-4-e4b",
  models: [
    { id: "gemma-4-e4b", status: "loaded", contextLength: 16384 },
    { id: "gemma-4-e2b", status: "unloaded", contextLength: null },
  ],
};

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(modelsService.fetchModelCatalog).mockResolvedValue(catalog);
  vi.mocked(modelsService.loadModel).mockResolvedValue(undefined);
  vi.mocked(modelsService.unloadModel).mockResolvedValue(undefined);
});

describe("useModelCatalog", () => {
  it("exposes the models and the default model", async () => {
    const { result } = renderHook(() => useModelCatalog(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.models).toEqual(catalog.models);
    expect(result.current.defaultModel).toBe("gemma-4-e4b");
    expect(result.current.isError).toBe(false);
  });

  it("reports an error when the inference server is unavailable", async () => {
    vi.mocked(modelsService.fetchModelCatalog).mockRejectedValueOnce(
      new ApiError(503, "The inference server is unavailable."),
    );
    const { result } = renderHook(() => useModelCatalog(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.models).toEqual([]);
  });

  it("loads a model and refreshes the catalog", async () => {
    const { result } = renderHook(() => useModelCatalog(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    const fetches = vi.mocked(modelsService.fetchModelCatalog).mock.calls.length;

    act(() => result.current.load("gemma-4-e2b"));

    await waitFor(() => expect(modelsService.loadModel).toHaveBeenCalledWith("gemma-4-e2b"));
    await waitFor(() => expect(vi.mocked(modelsService.fetchModelCatalog).mock.calls.length).toBeGreaterThan(fetches));
    expect(result.current.pendingModel).toBeNull();
  });

  it("unloads a model", async () => {
    const { result } = renderHook(() => useModelCatalog(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    act(() => result.current.unload("gemma-4-e4b"));

    await waitFor(() => expect(modelsService.unloadModel).toHaveBeenCalledWith("gemma-4-e4b"));
  });

  it("tracks the pending model while a request is in flight", async () => {
    let finish: () => void = () => {};
    vi.mocked(modelsService.unloadModel).mockReturnValueOnce(new Promise<void>((resolve) => (finish = resolve)));
    const { result } = renderHook(() => useModelCatalog(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    act(() => result.current.unload("gemma-4-e4b"));
    await waitFor(() => expect(result.current.pendingModel).toBe("gemma-4-e4b"));

    act(() => finish());
    await waitFor(() => expect(result.current.pendingModel).toBeNull());
  });

  it("surfaces a failed action and clears it on request", async () => {
    vi.mocked(modelsService.loadModel).mockRejectedValueOnce(
      new ApiError(409, "Model 'gemma-4-e2b' is still changing state."),
    );
    const { result } = renderHook(() => useModelCatalog(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    act(() => result.current.load("gemma-4-e2b"));
    await waitFor(() => expect(result.current.actionError?.message).toMatch(/still changing state/));

    act(() => result.current.clearActionError());
    await waitFor(() => expect(result.current.actionError).toBeNull());
  });
});
