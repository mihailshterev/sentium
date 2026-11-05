import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import { useWorkspaceFiles } from "./useWorkspaceFiles";
import * as workspacesService from "@/features/workspaces/api/workspaces.service";

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const file = { id: "f1", name: "doc.txt", processingStatus: "Completed" } as never;

beforeEach(() => {
  vi.spyOn(workspacesService, "fetchWorkspaceFiles").mockResolvedValue([file]);
  vi.spyOn(workspacesService, "uploadWorkspaceFile").mockResolvedValue(file);
  vi.spyOn(workspacesService, "deleteWorkspaceFile").mockResolvedValue(undefined);
});

describe("useWorkspaceFiles", () => {
  it("does not fetch when no workspace id is provided", () => {
    const spy = vi.spyOn(workspacesService, "fetchWorkspaceFiles");
    const { result } = renderHook(() => useWorkspaceFiles(undefined), { wrapper: createWrapper() });
    expect(result.current.files).toEqual([]);
    expect(spy).not.toHaveBeenCalled();
  });

  it("fetches files for the given workspace", async () => {
    const { result } = renderHook(() => useWorkspaceFiles("w1"), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isFilesLoading).toBe(false));
    expect(workspacesService.fetchWorkspaceFiles).toHaveBeenCalledWith("w1");
    expect(result.current.files).toEqual([file]);
  });

  it("uploads a file scoped to the workspace", async () => {
    const { result } = renderHook(() => useWorkspaceFiles("w1"), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isFilesLoading).toBe(false));
    const upload = new File(["x"], "x.txt");
    act(() => {
      result.current.uploadFile(upload);
    });
    await waitFor(() => expect(workspacesService.uploadWorkspaceFile).toHaveBeenCalledWith(upload, "w1"));
  });

  it("deletes a file", async () => {
    const { result } = renderHook(() => useWorkspaceFiles("w1"), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isFilesLoading).toBe(false));
    act(() => {
      result.current.deleteFile("f1");
    });
    await waitFor(() => expect(workspacesService.deleteWorkspaceFile).toHaveBeenCalledWith("f1"));
  });
});
