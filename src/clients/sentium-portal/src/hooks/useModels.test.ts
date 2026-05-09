import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import useModels from "./useModels";
import * as agentRuntimeService from "../services/agentRuntime.service";

vi.mock("../services/agentRuntime.service", () => ({
  fetchModels: vi.fn(),
}));

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

beforeEach(() => {
  vi.mocked(agentRuntimeService.fetchModels).mockResolvedValue(["llama3", "mistral"]);
});

describe("useModels fetching", () => {
  it("starts with an empty models array", () => {
    const { result } = renderHook(() => useModels(), { wrapper: createWrapper() });
    expect(result.current.models).toEqual([]);
  });

  it("populates models after fetch resolves", async () => {
    const { result } = renderHook(() => useModels(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.models).toEqual(["llama3", "mistral"]));
  });

  it("calls fetchModels exactly once on mount", async () => {
    const spy = vi.spyOn(agentRuntimeService, "fetchModels");
    const { result } = renderHook(() => useModels(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.models.length).toBeGreaterThan(0));
    expect(spy).toHaveBeenCalledTimes(1);
  });

  it("falls back to empty array when fetch fails", async () => {
    vi.mocked(agentRuntimeService.fetchModels).mockRejectedValueOnce(new Error("Service down"));
    const { result } = renderHook(() => useModels(), { wrapper: createWrapper() });
    await new Promise((r) => setTimeout(r, 50));
    expect(result.current.models).toEqual([]);
  });
});
