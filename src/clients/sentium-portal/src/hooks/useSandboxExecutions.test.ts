import { describe, it, expect, vi, beforeEach } from "vitest";
import { act, renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import { fetchExecutions } from "../services/sandbox.service";
import { useSandboxExecutions } from "./useSandboxExecutions";
import type { SandboxExecutionLog } from "../types/sandbox";

vi.mock("../services/sandbox.service", () => ({
  fetchExecutions: vi.fn(),
}));

const createWrapper = () => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const run = (overrides: Partial<SandboxExecutionLog> = {}): SandboxExecutionLog => ({
  jobId: "job-1",
  executedAt: new Date().toISOString(),
  agentId: "agent-7",
  correlationId: "corr-1",
  language: "Python",
  code: "print('hi')",
  fileContext: [],
  succeeded: true,
  exitCode: 0,
  output: "hi",
  error: "",
  timedOut: false,
  policyDenied: false,
  sentinelAuditId: "audit-1",
  durationMs: 42,
  artifacts: [],
  ...overrides,
});

beforeEach(() => {
  vi.mocked(fetchExecutions).mockResolvedValue({
    items: [run()],
    totalCount: 1,
    page: 1,
    pageSize: 20,
    totalPages: 1,
  });
});

describe("useSandboxExecutions fetching", () => {
  it("starts empty while loading", () => {
    const { result } = renderHook(() => useSandboxExecutions(), { wrapper: createWrapper() });
    expect(result.current.executions).toEqual([]);
    expect(result.current.isLoading).toBe(true);
  });

  it("populates executions and paging metadata after fetch resolves", async () => {
    const { result } = renderHook(() => useSandboxExecutions(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.executions).toHaveLength(1);
    expect(result.current.totalCount).toBe(1);
    expect(result.current.hasMore).toBe(false);
  });

  it("requests page 1 with default page size initially", async () => {
    renderHook(() => useSandboxExecutions(), { wrapper: createWrapper() });
    await waitFor(() =>
      expect(fetchExecutions).toHaveBeenCalledWith(expect.objectContaining({ page: 1, pageSize: 20 })),
    );
  });
});

describe("useSandboxExecutions filtering", () => {
  it("changing the status filter starts a fresh list and forwards the status", async () => {
    const { result } = renderHook(() => useSandboxExecutions(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    act(() => result.current.setStatus("Failed"));
    expect(result.current.status).toBe("Failed");

    await waitFor(() =>
      expect(fetchExecutions).toHaveBeenCalledWith(expect.objectContaining({ page: 1, status: "Failed" })),
    );
  });

  it("changing the language filter forwards the language", async () => {
    const { result } = renderHook(() => useSandboxExecutions(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    act(() => result.current.setLanguage("Node"));
    await waitFor(() => expect(fetchExecutions).toHaveBeenCalledWith(expect.objectContaining({ language: "Node" })));
  });
});
