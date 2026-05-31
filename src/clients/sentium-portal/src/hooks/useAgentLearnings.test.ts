import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, act, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import { useAgentLearnings } from "./useAgentLearnings";
import * as agentRuntimeService from "../services/agentRuntime.service";
import type { AgentLearning, AgentLearningStats } from "../types/agentConfig";

vi.mock("../services/agentRuntime.service", () => ({
  fetchAgentLearnings: vi.fn(),
  fetchAgentLearningStats: vi.fn(),
  updateAgentLearning: vi.fn(),
  deleteAgentLearning: vi.fn(),
}));

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false, retryDelay: 0 }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const mockLearning: AgentLearning = {
  id: "learn-1",
  agentName: "SecurityAnalyst",
  content: "Lateral movement typically follows privilege escalation.",
  tags: "threat,lateral-movement",
  conversationId: null,
  capturedAt: "2025-01-01T00:00:00Z",
  isIngested: false,
};

const mockStats: AgentLearningStats = {
  totalLearnings: 5,
  pendingIngestion: 2,
  learningsByAgent: { SecurityAnalyst: 3, Planner: 2 },
};

beforeEach(() => {
  vi.mocked(agentRuntimeService.fetchAgentLearnings).mockResolvedValue([mockLearning]);
  vi.mocked(agentRuntimeService.fetchAgentLearningStats).mockResolvedValue(mockStats);
  vi.mocked(agentRuntimeService.updateAgentLearning).mockResolvedValue(mockLearning);
  vi.mocked(agentRuntimeService.deleteAgentLearning).mockResolvedValue(undefined);
});

describe("useAgentLearnings fetching", () => {
  it("starts with empty learnings while loading", () => {
    const { result } = renderHook(() => useAgentLearnings(), { wrapper: createWrapper() });
    expect(result.current.learnings).toEqual([]);
    expect(result.current.isLoading).toBe(true);
  });

  it("populates learnings after fetch resolves", async () => {
    const { result } = renderHook(() => useAgentLearnings(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.learnings).toEqual([mockLearning]);
  });

  it("passes agentName filter to fetchAgentLearnings", async () => {
    const spy = vi.spyOn(agentRuntimeService, "fetchAgentLearnings");
    const { result } = renderHook(() => useAgentLearnings("SecurityAnalyst"), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(spy).toHaveBeenCalledWith("SecurityAnalyst", 50);
  });

  it("populates stats after fetch resolves", async () => {
    const { result } = renderHook(() => useAgentLearnings(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isStatsLoading).toBe(false));
    expect(result.current.stats).toEqual(mockStats);
  });

  it("exposes error when fetchAgentLearnings rejects", async () => {
    vi.mocked(agentRuntimeService.fetchAgentLearnings).mockRejectedValue(new Error("Service error"));
    const { result } = renderHook(() => useAgentLearnings(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.error).not.toBeNull());
    expect(result.current.learnings).toEqual([]);
  });
});

describe("useAgentLearnings mutation state", () => {
  it("isUpdating is false initially", () => {
    const { result } = renderHook(() => useAgentLearnings(), { wrapper: createWrapper() });
    expect(result.current.isUpdating).toBe(false);
  });

  it("isDeleting is false initially", () => {
    const { result } = renderHook(() => useAgentLearnings(), { wrapper: createWrapper() });
    expect(result.current.isDeleting).toBe(false);
  });
});

describe("useAgentLearnings deleteLearning()", () => {
  it("calls deleteAgentLearning with the learning id", async () => {
    const { result } = renderHook(() => useAgentLearnings(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    act(() => {
      result.current.deleteLearning("learn-1");
    });
    await waitFor(() => expect(agentRuntimeService.deleteAgentLearning).toHaveBeenCalledWith("learn-1"));
  });
});

describe("useAgentLearnings updateLearning()", () => {
  it("calls updateAgentLearning with the correct payload", async () => {
    const { result } = renderHook(() => useAgentLearnings(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    act(() => {
      result.current.updateLearning({ id: "learn-1", content: "Updated", tags: "security" });
    });
    await waitFor(() =>
      expect(agentRuntimeService.updateAgentLearning).toHaveBeenCalledWith("learn-1", {
        content: "Updated",
        tags: "security",
      }),
    );
  });
});

describe("useAgentLearnings with agentName filter", () => {
  it("uses agentName in query key when provided", async () => {
    const { result } = renderHook(() => useAgentLearnings("SecurityAnalyst"), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(agentRuntimeService.fetchAgentLearnings).toHaveBeenCalledWith("SecurityAnalyst", 50);
  });
});
