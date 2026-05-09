import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import useAgents from "./useAgents";
import * as agentRuntimeService from "../services/agentRuntime.service";
import type { AgentRecord } from "../types/agents";

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const mockAgent: AgentRecord = {
  id: "agent-1",
  name: "Analyzer",
  description: "Analyzes data",
  model: "llama3",
  createdAt: "2025-01-01T00:00:00Z",
  updatedAt: "2025-01-01T00:00:00Z",
};

beforeEach(() => {
  vi.spyOn(agentRuntimeService, "fetchAgents").mockResolvedValue([mockAgent]);
  vi.spyOn(agentRuntimeService, "createAgent").mockResolvedValue(mockAgent);
  vi.spyOn(agentRuntimeService, "updateAgent").mockResolvedValue({ ...mockAgent, name: "Updated" });
  vi.spyOn(agentRuntimeService, "deleteAgent").mockResolvedValue(undefined);
});

describe("useAgents fetching", () => {
  it("initially returns an empty array while loading", () => {
    const { result } = renderHook(() => useAgents(), { wrapper: createWrapper() });
    expect(result.current.agents).toEqual([]);
    expect(result.current.isLoading).toBe(true);
  });

  it("returns agents after successful fetch", async () => {
    const { result } = renderHook(() => useAgents(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.agents).toEqual([mockAgent]);
  });

  it("calls fetchAgents exactly once", async () => {
    const spy = vi.spyOn(agentRuntimeService, "fetchAgents");
    const { result } = renderHook(() => useAgents(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);
    expect(spy).toHaveBeenCalledTimes(1);
  });

  it("handles fetch errors gracefully (returns empty array)", async () => {
    vi.spyOn(agentRuntimeService, "fetchAgents").mockRejectedValueOnce(new Error("Server error"));
    const { result } = renderHook(() => useAgents(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.agents).toEqual([]);
  });
});

describe("useAgents mutation states exposed", () => {
  it("exposes isCreatingAgent, isUpdatingAgent, isDeletingAgent as false initially", async () => {
    const { result } = renderHook(() => useAgents(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);

    expect(result.current.isCreatingAgent).toBe(false);
    expect(result.current.isUpdatingAgent).toBe(false);
    expect(result.current.isDeletingAgent).toBe(false);
  });

  it("exposes isCreateSuccess/isCreateError as false initially", async () => {
    const { result } = renderHook(() => useAgents(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);

    expect(result.current.isCreateSuccess).toBe(false);
    expect(result.current.isCreateError).toBe(false);
  });
});

describe("useAgents mutations", () => {
  it("calls createAgent and transitions to success state", async () => {
    const { result } = renderHook(() => useAgents(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);
    act(() => {
      result.current.createAgent({ name: "New Agent", description: "", model: "llama3" });
    });
    await waitFor(() => expect(result.current.isCreateSuccess).toBe(true));
    expect(agentRuntimeService.createAgent).toHaveBeenCalled();
  });

  it("calls updateAgent and settles", async () => {
    const { result } = renderHook(() => useAgents(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);
    act(() => {
      result.current.updateAgent({ id: "agent-1", name: "Updated", description: "", model: "llama3.2" });
    });
    await waitFor(() => expect(result.current.isUpdatingAgent).toBe(false));
    expect(agentRuntimeService.updateAgent).toHaveBeenCalled();
  });

  it("calls deleteAgent and settles", async () => {
    const { result } = renderHook(() => useAgents(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);
    act(() => {
      result.current.deleteAgent("agent-1");
    });
    await waitFor(() => expect(result.current.isDeletingAgent).toBe(false));
    expect(agentRuntimeService.deleteAgent).toHaveBeenCalledWith("agent-1");
  });

  it("resetCreate resets the create mutation state", async () => {
    const { result } = renderHook(() => useAgents(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);
    act(() => {
      result.current.createAgent({ name: "New Agent", description: "", model: "llama3" });
    });
    await waitFor(() => expect(result.current.isCreateSuccess).toBe(true));
    act(() => {
      result.current.resetCreate();
    });
    await waitFor(() => expect(result.current.isCreateSuccess).toBe(false));
  });
});
