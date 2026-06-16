import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import useWorkflows from "./useWorkflows";
import * as agentRuntimeService from "../services/agentRuntime.service";
import type { WorkflowRecord } from "../types/workflows";

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const mockWorkflow: WorkflowRecord = {
  id: "wf-1",
  name: "Data Pipeline",
  description: "Processes data end-to-end",
  createdAt: "2025-01-01T00:00:00Z",
  updatedAt: "2025-01-01T00:00:00Z",
  agents: [{ agentId: "agent-1", order: 0 }],
};

beforeEach(() => {
  vi.spyOn(agentRuntimeService, "fetchWorkflowsPaged").mockResolvedValue({
    items: [mockWorkflow],
    totalCount: 1,
    page: 1,
    pageSize: 100,
    totalPages: 1,
  });
  vi.spyOn(agentRuntimeService, "createWorkflow").mockResolvedValue(mockWorkflow);
  vi.spyOn(agentRuntimeService, "updateWorkflow").mockResolvedValue({ ...mockWorkflow, name: "Updated" });
  vi.spyOn(agentRuntimeService, "deleteWorkflow").mockResolvedValue(undefined);
});

describe("useWorkflows fetching", () => {
  it("initially returns an empty array while loading", () => {
    const { result } = renderHook(() => useWorkflows(), { wrapper: createWrapper() });
    expect(result.current.workflows).toEqual([]);
    expect(result.current.isLoading).toBe(true);
  });

  it("returns workflows after successful fetch", async () => {
    const { result } = renderHook(() => useWorkflows(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.workflows).toEqual([mockWorkflow]);
  });

  it("calls fetchWorkflows exactly once on mount", async () => {
    const spy = vi.spyOn(agentRuntimeService, "fetchWorkflowsPaged");
    const { result } = renderHook(() => useWorkflows(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);
    expect(spy).toHaveBeenCalledTimes(1);
  });

  it("falls back to empty array on fetch error", async () => {
    vi.spyOn(agentRuntimeService, "fetchWorkflowsPaged").mockRejectedValueOnce(new Error("Network error"));
    const { result } = renderHook(() => useWorkflows(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.workflows).toEqual([]);
  });
});

describe("useWorkflows mutation states", () => {
  it("all pending flags are false on initial mount", async () => {
    const { result } = renderHook(() => useWorkflows(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);

    expect(result.current.isCreatingWorkflow).toBe(false);
    expect(result.current.isUpdatingWorkflow).toBe(false);
    expect(result.current.isDeletingWorkflow).toBe(false);
  });

  it("isCreateSuccess and isCreateError are false initially", async () => {
    const { result } = renderHook(() => useWorkflows(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);

    expect(result.current.isCreateSuccess).toBe(false);
    expect(result.current.isCreateError).toBe(false);
  });

  it("isUpdateSuccess and isUpdateError are false initially", async () => {
    const { result } = renderHook(() => useWorkflows(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);

    expect(result.current.isUpdateSuccess).toBe(false);
    expect(result.current.isUpdateError).toBe(false);
  });

  it("createWorkflowError and updateWorkflowError are null initially", async () => {
    const { result } = renderHook(() => useWorkflows(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);

    expect(result.current.createWorkflowError).toBeNull();
    expect(result.current.updateWorkflowError).toBeNull();
  });
});

describe("useWorkflows reset functions", () => {
  it("resetCreate is a function", async () => {
    const { result } = renderHook(() => useWorkflows(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);
    expect(typeof result.current.resetCreate).toBe("function");
  });

  it("resetUpdate is a function", async () => {
    const { result } = renderHook(() => useWorkflows(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);
    expect(typeof result.current.resetUpdate).toBe("function");
  });
});

describe("useWorkflows mutation calls", () => {
  it("calls createWorkflow service and reaches success state", async () => {
    const { result } = renderHook(() => useWorkflows(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);
    act(() => {
      result.current.createWorkflow({ name: "New WF", description: "", agents: [] });
    });
    await waitFor(() => expect(result.current.isCreateSuccess).toBe(true));
    expect(agentRuntimeService.createWorkflow).toHaveBeenCalled();
  });

  it("calls updateWorkflow service and settles", async () => {
    const { result } = renderHook(() => useWorkflows(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);
    act(() => {
      result.current.updateWorkflow({ id: "wf-1", name: "Updated", description: "", agents: [] });
    });
    await waitFor(() => expect(result.current.isUpdatingWorkflow).toBe(false));
    expect(agentRuntimeService.updateWorkflow).toHaveBeenCalled();
  });

  it("calls deleteWorkflow service and settles", async () => {
    const { result } = renderHook(() => useWorkflows(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);
    act(() => {
      result.current.deleteWorkflow("wf-1");
    });
    await waitFor(() => expect(result.current.isDeletingWorkflow).toBe(false));
    expect(agentRuntimeService.deleteWorkflow).toHaveBeenCalledWith("wf-1");
  });

  it("resetCreate resets the create state", async () => {
    const { result } = renderHook(() => useWorkflows(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);
    act(() => {
      result.current.createWorkflow({ name: "New WF", description: "", agents: [] });
    });
    await waitFor(() => expect(result.current.isCreateSuccess).toBe(true));
    act(() => {
      result.current.resetCreate();
    });
    await waitFor(() => expect(result.current.isCreateSuccess).toBe(false));
  });

  it("resetUpdate resets the update state", async () => {
    const { result } = renderHook(() => useWorkflows(), { wrapper: createWrapper() });
    await waitFor(() => !result.current.isLoading);
    act(() => {
      result.current.updateWorkflow({ id: "wf-1", name: "Updated", description: "", agents: [] });
    });
    await waitFor(() => expect(result.current.isUpdateSuccess).toBe(true));
    act(() => {
      result.current.resetUpdate();
    });
    await waitFor(() => expect(result.current.isUpdateSuccess).toBe(false));
  });
});
