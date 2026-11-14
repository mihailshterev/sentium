import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import { useNotificationStream } from "./useNotificationStream";
import { notificationKeys } from "./useInbox";

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

describe("useNotificationStream", () => {
  it("subscribes to the nuntius stream and closes it on unmount", () => {
    const { unmount } = renderHook(() => useNotificationStream(), { wrapper });
    const source = FakeEventSource.last();

    expect(source.url).toMatch(/\/nuntius\/stream$/);

    unmount();
    expect(source.closed).toBe(true);
  });

  it.each(["connected", "notification", "read"])("refreshes the inbox on a %s frame", (type) => {
    renderHook(() => useNotificationStream(), { wrapper });
    const invalidate = vi.spyOn(qc, "invalidateQueries");

    act(() => FakeEventSource.last().onmessage?.({ data: JSON.stringify({ type }) }));

    expect(invalidate).toHaveBeenCalledWith({ queryKey: notificationKeys.all });
  });

  it("ignores frames it cannot parse", () => {
    renderHook(() => useNotificationStream(), { wrapper });
    const invalidate = vi.spyOn(qc, "invalidateQueries");

    act(() => FakeEventSource.last().onmessage?.({ data: "not json" }));
    act(() => FakeEventSource.last().onmessage?.({ data: JSON.stringify({ type: "unknown" }) }));

    expect(invalidate).not.toHaveBeenCalled();
  });
});
