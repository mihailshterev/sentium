import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import useWorkspaces from "./useWorkspaces";
import * as agentRuntimeService from "../services/agentRuntime.service";

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const workspace = { id: "w1", name: "Default" } as never;

beforeEach(() => {
  vi.spyOn(agentRuntimeService, "fetchWorkspacesPaged").mockResolvedValue({
    items: [workspace],
    totalCount: 1,
    page: 1,
    pageSize: 100,
    totalPages: 1,
  });
  vi.spyOn(agentRuntimeService, "createWorkspace").mockResolvedValue(workspace);
  vi.spyOn(agentRuntimeService, "updateWorkspace").mockResolvedValue(workspace);
  vi.spyOn(agentRuntimeService, "deleteWorkspace").mockResolvedValue(undefined);
});

describe("useWorkspaces", () => {
  it("returns workspaces after a successful fetch", async () => {
    const { result } = renderHook(() => useWorkspaces(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.workspaces).toEqual([workspace]);
  });

  it("creates a workspace", async () => {
    const { result } = renderHook(() => useWorkspaces(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    act(() => {
      result.current.createWorkspace({ name: "New" } as never);
    });
    await waitFor(() => expect(agentRuntimeService.createWorkspace).toHaveBeenCalled());
  });

  it("updates a workspace, splitting id from the payload", async () => {
    const { result } = renderHook(() => useWorkspaces(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    act(() => {
      result.current.updateWorkspace({ id: "w1", name: "Renamed" } as never);
    });
    await waitFor(() => expect(agentRuntimeService.updateWorkspace).toHaveBeenCalledWith("w1", { name: "Renamed" }));
  });

  it("deletes a workspace", async () => {
    const { result } = renderHook(() => useWorkspaces(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    act(() => {
      result.current.deleteWorkspace("w1");
    });
    await waitFor(() => expect(agentRuntimeService.deleteWorkspace).toHaveBeenCalledWith("w1"));
  });
});
