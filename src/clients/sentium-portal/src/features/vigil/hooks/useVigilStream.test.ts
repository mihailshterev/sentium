import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import { useVigilStream } from "./useVigilStream";
import { SERVICE_HEALTH_KEY } from "./useServiceHealth";
import { INCIDENTS_KEY } from "./useIncidents";
import { DEVICE_STATUSES_KEY } from "@/features/devices";
import { startRealtime, stopRealtime } from "@/shared/realtime";
import { FakeEventSource } from "@/test/fake-event-source";

let qc: QueryClient;

const wrapper = ({ children }: { children: React.ReactNode }) =>
  React.createElement(QueryClientProvider, { client: qc }, children);

const push = (topic: string, data?: unknown) => act(() => FakeEventSource.last().emit({ topic, data }));

beforeEach(() => {
  FakeEventSource.install();
  vi.stubGlobal("BroadcastChannel", undefined);
  qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  startRealtime();
});

afterEach(() => {
  stopRealtime();
  vi.unstubAllGlobals();
});

describe("useVigilStream", () => {
  it("is live while the realtime stream is", () => {
    const { result } = renderHook(() => useVigilStream(), { wrapper });
    expect(result.current.isLive).toBe(false);

    act(() => FakeEventSource.last().open());
    expect(result.current.isLive).toBe(true);

    act(() => FakeEventSource.last().drop());
    expect(result.current.isLive).toBe(false);
  });

  it("merges a status update into the service-health cache", () => {
    qc.setQueryData(SERVICE_HEALTH_KEY, [
      { serviceName: "Praxis", status: "Healthy", latencyMs: 1, uptimePercent: 100, checkedAt: "t0", details: null },
    ]);
    renderHook(() => useVigilStream(), { wrapper });

    push("vigil.status", {
      serviceName: "Praxis",
      kind: "Service",
      status: "Degraded",
      latencyMs: 42,
      uptimePercent: 98,
      timestamp: "t1",
      details: "slow",
    });

    const cached = qc.getQueryData<Array<Record<string, unknown>>>(SERVICE_HEALTH_KEY);
    expect(cached?.[0]).toMatchObject({ status: "Degraded", latencyMs: 42, checkedAt: "t1", details: "slow" });
  });

  it("refetches incidents on incident events", () => {
    const invalidate = vi.spyOn(qc, "invalidateQueries");
    renderHook(() => useVigilStream(), { wrapper });

    push("vigil.incident.opened", { id: "i1" });

    expect(invalidate).toHaveBeenCalledWith(expect.objectContaining({ queryKey: INCIDENTS_KEY }), {
      cancelRefetch: false,
    });
  });

  it("drops a removed Device from the cache", () => {
    qc.setQueryData(DEVICE_STATUSES_KEY, [{ id: "d1" }, { id: "d2" }]);
    renderHook(() => useVigilStream(), { wrapper });

    push("vigil.device.removed", { id: "d1" });

    expect(qc.getQueryData(DEVICE_STATUSES_KEY)).toEqual([{ id: "d2" }]);
  });

  it("refetches everything it shows after a (re)connect", () => {
    const invalidate = vi.spyOn(qc, "invalidateQueries");
    renderHook(() => useVigilStream(), { wrapper });

    push("connected");

    expect(invalidate).toHaveBeenCalledWith(expect.objectContaining({ queryKey: SERVICE_HEALTH_KEY }), {
      cancelRefetch: false,
    });
    expect(invalidate).toHaveBeenCalledWith(expect.objectContaining({ queryKey: DEVICE_STATUSES_KEY }), {
      cancelRefetch: false,
    });
  });
});
