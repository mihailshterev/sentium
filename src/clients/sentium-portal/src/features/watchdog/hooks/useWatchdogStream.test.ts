import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { renderHook, act, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import useWatchdogStream from "./useWatchdogStream";
import { SERVICE_HEALTH_KEY } from "./useServiceHealth";
import { INCIDENTS_KEY } from "./useIncidents";

class FakeEventSource {
  static instances: FakeEventSource[] = [];
  url: string;
  onopen: (() => void) | null = null;
  onerror: (() => void) | null = null;
  onmessage: ((e: { data: string }) => void) | null = null;
  closed = false;

  constructor(url: string) {
    this.url = url;
    FakeEventSource.instances.push(this);
  }
  close() {
    this.closed = true;
  }
  static last() {
    return FakeEventSource.instances[FakeEventSource.instances.length - 1];
  }
}

let qc: QueryClient;

const wrapper = ({ children }: { children: React.ReactNode }) =>
  React.createElement(QueryClientProvider, { client: qc }, children);

beforeEach(() => {
  FakeEventSource.instances = [];
  vi.stubGlobal("EventSource", FakeEventSource as unknown as typeof EventSource);
  qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("useWatchdogStream", () => {
  it("opens a stream and reports live status on open/error", () => {
    const { result } = renderHook(() => useWatchdogStream(), { wrapper });
    expect(result.current.isLive).toBe(false);

    act(() => FakeEventSource.last().onopen?.());
    expect(result.current.isLive).toBe(true);

    act(() => FakeEventSource.last().onerror?.());
    expect(result.current.isLive).toBe(false);
  });

  it("ignores blank or comment SSE frames", () => {
    renderHook(() => useWatchdogStream(), { wrapper });
    const src = FakeEventSource.last();
    expect(() => {
      act(() => src.onmessage?.({ data: "" }));
      act(() => src.onmessage?.({ data: ":keep-alive" }));
      act(() => src.onmessage?.({ data: "not json" }));
    }).not.toThrow();
  });

  it("merges a status update into the service-health cache", () => {
    qc.setQueryData(SERVICE_HEALTH_KEY, [
      { serviceName: "Gateway", status: "Healthy", latencyMs: 1, uptimePercent: 100, checkedAt: "t0", details: null },
    ]);

    renderHook(() => useWatchdogStream(), { wrapper });
    const src = FakeEventSource.last();

    act(() =>
      src.onmessage?.({
        data: JSON.stringify({
          type: "status",
          data: {
            serviceName: "Gateway",
            kind: "Service",
            status: "Degraded",
            latencyMs: 42,
            uptimePercent: 98,
            timestamp: "t1",
            details: "slow",
          },
        }),
      }),
    );

    const cached = qc.getQueryData<Array<Record<string, unknown>>>(SERVICE_HEALTH_KEY);
    expect(cached?.[0]).toMatchObject({ status: "Degraded", latencyMs: 42, checkedAt: "t1", details: "slow" });
  });

  it("invalidates incident queries on incident events", async () => {
    const invalidateSpy = vi.spyOn(qc, "invalidateQueries");
    renderHook(() => useWatchdogStream(), { wrapper });
    const src = FakeEventSource.last();

    act(() => src.onmessage?.({ data: JSON.stringify({ type: "incident.opened" }) }));

    await waitFor(() =>
      expect(invalidateSpy).toHaveBeenCalledWith(expect.objectContaining({ queryKey: INCIDENTS_KEY })),
    );
  });

  it("closes the stream on unmount", () => {
    const { unmount } = renderHook(() => useWatchdogStream(), { wrapper });
    const src = FakeEventSource.last();
    unmount();
    expect(src.closed).toBe(true);
  });
});
