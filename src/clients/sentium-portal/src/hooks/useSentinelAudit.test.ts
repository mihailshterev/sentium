import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import { useSentinelAudit, useSentinelStats } from "./useSentinelAudit";
import * as sentinelService from "../services/sentinel.service";

const createWrapper = () => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const record = { id: "r1" } as never;
const stats = { total: 1 } as never;

beforeEach(() => {
  vi.spyOn(sentinelService, "fetchAuditLog").mockResolvedValue([record]);
  vi.spyOn(sentinelService, "fetchAuditStats").mockResolvedValue(stats);
});

describe("useSentinelAudit", () => {
  it("requests the audit log with the provided count", async () => {
    const { result } = renderHook(() => useSentinelAudit(42), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(sentinelService.fetchAuditLog).toHaveBeenCalledWith(42);
    expect(result.current.records).toEqual([record]);
  });

  it("defaults records to an empty array while loading", () => {
    const { result } = renderHook(() => useSentinelAudit(), { wrapper: createWrapper() });
    expect(result.current.records).toEqual([]);
  });
});

describe("useSentinelStats", () => {
  it("returns stats after a successful fetch", async () => {
    const { result } = renderHook(() => useSentinelStats(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.stats).toEqual(stats);
  });
});
