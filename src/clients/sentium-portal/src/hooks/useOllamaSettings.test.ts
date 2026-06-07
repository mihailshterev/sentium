import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import { useOllamaSettings } from "./useOllamaSettings";
import * as registryService from "../services/registry.service";

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const envelope = {
  key: "ollama",
  value: { defaultModel: "gemma", agentTemperature: 0.7, agentContextWindow: 4096 },
  updatedAt: "2025-01-01T00:00:00Z",
  updatedBy: null,
} as never;

beforeEach(() => {
  vi.spyOn(registryService, "fetchOllamaSettings").mockResolvedValue(envelope);
  vi.spyOn(registryService, "updateOllamaSettings").mockResolvedValue(envelope);
});

describe("useOllamaSettings", () => {
  it("does not fetch when disabled", () => {
    const spy = vi.spyOn(registryService, "fetchOllamaSettings");
    renderHook(() => useOllamaSettings(false), { wrapper: createWrapper() });
    expect(spy).not.toHaveBeenCalled();
  });

  it("loads the settings envelope when enabled", async () => {
    const { result } = renderHook(() => useOllamaSettings(true), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.settings).toEqual(envelope);
  });

  it("saves settings and transitions to a success state", async () => {
    const { result } = renderHook(() => useOllamaSettings(true), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    act(() => {
      result.current.save({ defaultModel: "qwen", agentTemperature: 0.5, agentContextWindow: 2048 });
    });

    await waitFor(() => expect(result.current.isSaveSuccess).toBe(true));
    expect(registryService.updateOllamaSettings).toHaveBeenCalled();
  });
});
