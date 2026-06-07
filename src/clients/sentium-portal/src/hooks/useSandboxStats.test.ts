import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import { useSandboxStats } from "./useSandboxStats";
import * as sandboxService from "../services/sandbox.service";

const createWrapper = () => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const stats = { total: 3 } as never;

beforeEach(() => {
  vi.spyOn(sandboxService, "fetchSandboxStats").mockResolvedValue(stats);
});

describe("useSandboxStats", () => {
  it("returns null while loading", () => {
    const { result } = renderHook(() => useSandboxStats(), { wrapper: createWrapper() });
    expect(result.current.stats).toBeNull();
    expect(result.current.isLoading).toBe(true);
  });

  it("returns stats after a successful fetch", async () => {
    const { result } = renderHook(() => useSandboxStats(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.stats).toEqual(stats);
  });

  it("keeps stats null on error", async () => {
    vi.spyOn(sandboxService, "fetchSandboxStats").mockRejectedValueOnce(new Error("x"));
    const { result } = renderHook(() => useSandboxStats(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.stats).toBeNull();
  });
});
