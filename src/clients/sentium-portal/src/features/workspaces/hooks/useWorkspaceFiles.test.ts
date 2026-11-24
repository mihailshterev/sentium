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
const page = { items: [file], totalCount: 1, page: 1, pageSize: 50, totalPages: 1 };

beforeEach(() => {
  vi.spyOn(workspacesService, "fetchWorkspaceFilesPage").mockResolvedValue(page);
  vi.spyOn(workspacesService, "uploadWorkspaceFile").mockResolvedValue(file);
  vi.spyOn(workspacesService, "deleteWorkspaceFile").mockResolvedValue(undefined);
});

describe("useWorkspaceFiles", () => {
  it("does not fetch when no workspace id is provided", () => {
    const spy = vi.spyOn(workspacesService, "fetchWorkspaceFilesPage");
    const { result } = renderHook(() => useWorkspaceFiles(undefined), { wrapper: createWrapper() });
    expect(result.current.files).toEqual([]);
    expect(spy).not.toHaveBeenCalled();
  });

  it("fetches files for the given workspace", async () => {
    const { result } = renderHook(() => useWorkspaceFiles("w1"), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isFilesLoading).toBe(false));
    expect(workspacesService.fetchWorkspaceFilesPage).toHaveBeenCalledWith("w1", 1, 50, undefined);
    expect(result.current.files).toEqual([file]);
    expect(result.current.totalCount).toBe(1);
    expect(result.current.hasMoreFiles).toBe(false);
  });

  it("searches the workspace's files on the server", async () => {
    const { result } = renderHook(() => useWorkspaceFiles("w1", "  tax "), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isFilesLoading).toBe(false));
    expect(workspacesService.fetchWorkspaceFilesPage).toHaveBeenCalledWith("w1", 1, 50, "tax");
  });

  it("loads the next page", async () => {
    vi.mocked(workspacesService.fetchWorkspaceFilesPage)
      .mockResolvedValueOnce({ ...page, totalCount: 2, totalPages: 2 })
      .mockResolvedValueOnce({ ...page, items: [{ id: "f2" } as never], totalCount: 2, page: 2, totalPages: 2 });
    const { result } = renderHook(() => useWorkspaceFiles("w1"), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.hasMoreFiles).toBe(true));
    act(() => result.current.loadMoreFiles());
    await waitFor(() => expect(result.current.files).toHaveLength(2));
    expect(workspacesService.fetchWorkspaceFilesPage).toHaveBeenLastCalledWith("w1", 2, 50, undefined);
  });

  it("uploads several files one after another, scoped to the workspace", async () => {
    const { result } = renderHook(() => useWorkspaceFiles("w1"), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isFilesLoading).toBe(false));
    const first = new File(["x"], "x.txt");
    const second = new File(["y"], "y.md");
    act(() => {
      result.current.uploadFiles([first, second]);
    });
    await waitFor(() => expect(workspacesService.uploadWorkspaceFile).toHaveBeenCalledTimes(2));
    expect(workspacesService.uploadWorkspaceFile).toHaveBeenNthCalledWith(1, first, "w1", expect.any(Function));
    expect(workspacesService.uploadWorkspaceFile).toHaveBeenNthCalledWith(2, second, "w1", expect.any(Function));
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
