import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import useWorkspaceFiles from "./useWorkspaceFiles";
import * as agentRuntimeService from "../services/agentRuntime.service";

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const file = { id: "f1", name: "doc.txt", processingStatus: "Completed" } as never;

beforeEach(() => {
  vi.spyOn(agentRuntimeService, "fetchWorkspaceFiles").mockResolvedValue([file]);
  vi.spyOn(agentRuntimeService, "uploadWorkspaceFile").mockResolvedValue(file);
  vi.spyOn(agentRuntimeService, "deleteWorkspaceFile").mockResolvedValue(undefined);
});

describe("useWorkspaceFiles", () => {
  it("does not fetch when no workspace id is provided", () => {
    const spy = vi.spyOn(agentRuntimeService, "fetchWorkspaceFiles");
    const { result } = renderHook(() => useWorkspaceFiles(undefined), { wrapper: createWrapper() });
    expect(result.current.files).toEqual([]);
    expect(spy).not.toHaveBeenCalled();
  });

  it("fetches files for the given workspace", async () => {
    const { result } = renderHook(() => useWorkspaceFiles("w1"), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isFilesLoading).toBe(false));
    expect(agentRuntimeService.fetchWorkspaceFiles).toHaveBeenCalledWith("w1");
    expect(result.current.files).toEqual([file]);
  });

  it("uploads a file scoped to the workspace", async () => {
    const { result } = renderHook(() => useWorkspaceFiles("w1"), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isFilesLoading).toBe(false));
    const upload = new File(["x"], "x.txt");
    act(() => {
      result.current.uploadFile(upload);
    });
    await waitFor(() => expect(agentRuntimeService.uploadWorkspaceFile).toHaveBeenCalledWith(upload, "w1"));
  });

  it("deletes a file", async () => {
    const { result } = renderHook(() => useWorkspaceFiles("w1"), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isFilesLoading).toBe(false));
    act(() => {
      result.current.deleteFile("f1");
    });
    await waitFor(() => expect(agentRuntimeService.deleteWorkspaceFile).toHaveBeenCalledWith("f1"));
  });
});
