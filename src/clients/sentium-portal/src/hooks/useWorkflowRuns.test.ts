import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import useWorkflowRuns from "./useWorkflowRuns";
import { useWorkflowRun } from "./useWorkflowRuns";
import * as agentRuntimeService from "../services/agentRuntime.service";
import type { WorkflowRun } from "../types/workflows";

vi.mock("../services/agentRuntime.service", () => ({
  fetchWorkflowRuns: vi.fn(),
  fetchWorkflowRun: vi.fn(),
}));

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const mockRun: WorkflowRun = {
  id: "run-1",
  triggerType: "Manual",
  triggerPayload: "{}",
  explanation: "Ran network analysis",
  risk: "Low",
  recommendation: "No action needed",
  startedAt: "2025-01-01T00:00:00Z",
  completedAt: "2025-01-01T00:01:00Z",
  logs: [],
};

beforeEach(() => {
  vi.mocked(agentRuntimeService.fetchWorkflowRuns).mockResolvedValue([mockRun]);
  vi.mocked(agentRuntimeService.fetchWorkflowRun).mockResolvedValue(mockRun);
});

describe("useWorkflowRuns fetching", () => {
  it("starts with an empty runs array while loading", () => {
    const { result } = renderHook(() => useWorkflowRuns(), { wrapper: createWrapper() });
    expect(result.current.runs).toEqual([]);
    expect(result.current.isLoading).toBe(true);
  });

  it("populates runs after fetch resolves", async () => {
    const { result } = renderHook(() => useWorkflowRuns(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.runs).toEqual([mockRun]);
  });

  it("calls fetchWorkflowRuns with default count of 15", async () => {
    const spy = vi.spyOn(agentRuntimeService, "fetchWorkflowRuns");
    const { result } = renderHook(() => useWorkflowRuns(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(spy).toHaveBeenCalledWith(15);
  });

  it("calls fetchWorkflowRuns with a custom count", async () => {
    const spy = vi.spyOn(agentRuntimeService, "fetchWorkflowRuns");
    const { result } = renderHook(() => useWorkflowRuns(30), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(spy).toHaveBeenCalledWith(30);
  });

  it("falls back to empty array when fetch fails", async () => {
    vi.mocked(agentRuntimeService.fetchWorkflowRuns).mockRejectedValueOnce(new Error("API error"));
    const { result } = renderHook(() => useWorkflowRuns(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.runs).toEqual([]);
  });
});

describe("useWorkflowRun fetching", () => {
  it("does not fetch when runId is undefined", async () => {
    const spy = vi.spyOn(agentRuntimeService, "fetchWorkflowRun");
    const { result } = renderHook(() => useWorkflowRun(undefined), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.run).toBeUndefined();
    expect(spy).not.toHaveBeenCalled();
  });

  it("fetches and returns run when runId is provided", async () => {
    const spy = vi.spyOn(agentRuntimeService, "fetchWorkflowRun");
    const { result } = renderHook(() => useWorkflowRun("run-1"), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(spy).toHaveBeenCalledWith("run-1");
    expect(result.current.run).toEqual(mockRun);
  });
});
