import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import { useSchedulerJobs, useDeleteJobMutation } from "./useScheduler";
import * as schedulerService from "../services/scheduler.service";

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const job = { id: "j1", agentId: "a1" } as never;

beforeEach(() => {
  vi.spyOn(schedulerService, "fetchActiveSchedulerJobs").mockResolvedValue([job]);
  vi.spyOn(schedulerService, "deleteScheduledJob").mockResolvedValue(undefined);
});

describe("useSchedulerJobs", () => {
  it("returns an empty list while loading", () => {
    const { result } = renderHook(() => useSchedulerJobs(), { wrapper: createWrapper() });
    expect(result.current.jobs).toEqual([]);
    expect(result.current.isLoading).toBe(true);
  });

  it("returns jobs after a successful fetch", async () => {
    const { result } = renderHook(() => useSchedulerJobs(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.jobs).toEqual([job]);
  });
});

describe("useDeleteJobMutation", () => {
  it("deletes a job and settles successfully", async () => {
    const { result } = renderHook(() => useDeleteJobMutation(), { wrapper: createWrapper() });
    act(() => {
      result.current.mutate({ agentId: "a1", jobId: "j1" });
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(schedulerService.deleteScheduledJob).toHaveBeenCalledWith("a1", "j1");
  });
});
