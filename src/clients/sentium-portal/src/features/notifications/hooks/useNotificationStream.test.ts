import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import { useNotificationStream } from "./useNotificationStream";
import { notificationKeys } from "./useInbox";
import { startRealtime, stopRealtime } from "@/shared/realtime";
import { FakeEventSource } from "@/test/fake-event-source";

let qc: QueryClient;

const wrapper = ({ children }: { children: React.ReactNode }) =>
  React.createElement(QueryClientProvider, { client: qc }, children);

const push = (frame: object) => act(() => FakeEventSource.last().emit(frame));

beforeEach(() => {
  vi.useFakeTimers();
  FakeEventSource.install();
  vi.stubGlobal("BroadcastChannel", undefined);
  qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  startRealtime();
});

afterEach(() => {
  stopRealtime();
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe("useNotificationStream", () => {
  it.each([
    ["a notification", { topic: "notifications", data: { type: "notification" } }],
    ["a read", { topic: "notifications", data: { type: "read" } }],
    ["a (re)connect", { topic: "connected" }],
  ])("refreshes the inbox on %s", async (_label, frame) => {
    renderHook(() => useNotificationStream(), { wrapper });
    const invalidate = vi.spyOn(qc, "invalidateQueries");

    push(frame);
    await act(() => vi.advanceTimersByTimeAsync(300));

    expect(invalidate).toHaveBeenCalledWith({ queryKey: notificationKeys.all });
  });

  it("refreshes once for a burst of events", async () => {
    renderHook(() => useNotificationStream(), { wrapper });
    const invalidate = vi.spyOn(qc, "invalidateQueries");

    push({ topic: "notifications", data: { type: "notification" } });
    push({ topic: "notifications", data: { type: "notification" } });
    await act(() => vi.advanceTimersByTimeAsync(300));

    expect(invalidate).toHaveBeenCalledTimes(1);
  });

  it("ignores other topics and frames it cannot parse", async () => {
    renderHook(() => useNotificationStream(), { wrapper });
    const invalidate = vi.spyOn(qc, "invalidateQueries");

    act(() => FakeEventSource.last().emit("not json"));
    push({ topic: "vault" });
    await act(() => vi.advanceTimersByTimeAsync(300));

    expect(invalidate).not.toHaveBeenCalled();
  });

  it("stops listening on unmount", async () => {
    const { unmount } = renderHook(() => useNotificationStream(), { wrapper });
    const invalidate = vi.spyOn(qc, "invalidateQueries");

    unmount();
    push({ topic: "notifications", data: { type: "read" } });
    await act(() => vi.advanceTimersByTimeAsync(300));

    expect(invalidate).not.toHaveBeenCalled();
  });
});
