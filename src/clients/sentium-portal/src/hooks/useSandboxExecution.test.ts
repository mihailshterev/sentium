import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import { useSandboxExecution } from "./useSandboxExecution";
import * as sandboxService from "../services/sandbox.service";

const createWrapper = () => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const log = { jobId: "job-1", status: "Succeeded" } as never;

beforeEach(() => {
  vi.spyOn(sandboxService, "fetchExecution").mockResolvedValue(log);
});

describe("useSandboxExecution", () => {
  it("is disabled (no fetch) when jobId is undefined", () => {
    const spy = vi.spyOn(sandboxService, "fetchExecution");
    const { result } = renderHook(() => useSandboxExecution(undefined), { wrapper: createWrapper() });
    expect(result.current.execution).toBeNull();
    expect(spy).not.toHaveBeenCalled();
  });

  it("fetches and returns the execution when a jobId is given", async () => {
    const { result } = renderHook(() => useSandboxExecution("job-1"), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.execution).toEqual(log));
    expect(sandboxService.fetchExecution).toHaveBeenCalledWith("job-1");
  });
});
