import { act, renderHook } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createPersistedStore, createStore } from "./create-store";

interface Counter {
  count: number;
  label: string;
  increment: () => void;
}

const counter = () =>
  createStore<Counter>((set) => ({
    count: 0,
    label: "a",
    increment: () => set((s) => ({ count: s.count + 1 })),
  }));

describe("createStore", () => {
  it("merges partial updates and keeps actions", () => {
    const store = counter();
    store.getState().increment();
    store.setState({ label: "b" });

    expect(store.getState()).toMatchObject({ count: 1, label: "b" });
    expect(typeof store.getState().increment).toBe("function");
  });

  it("notifies subscribers with the previous state until they unsubscribe", () => {
    const store = counter();
    const listener = vi.fn();
    const unsubscribe = store.subscribe(listener);

    store.getState().increment();
    expect(listener).toHaveBeenCalledWith(expect.objectContaining({ count: 1 }), expect.objectContaining({ count: 0 }));

    unsubscribe();
    store.getState().increment();
    expect(listener).toHaveBeenCalledTimes(1);
  });

  it("re-renders hook consumers with the selected slice", () => {
    const store = counter();
    const { result } = renderHook(() => store((s) => s.count));

    act(() => store.getState().increment());

    expect(result.current).toBe(1);
  });
});

describe("createPersistedStore", () => {
  const key = "test-store";

  afterEach(() => window.localStorage.clear());

  const persisted = (onRehydrate?: (state: Counter) => void) =>
    createPersistedStore<Counter, { count: number }>(
      (set) => ({ count: 0, label: "a", increment: () => set((s) => ({ count: s.count + 1 })) }),
      { name: key, partialize: (s) => ({ count: s.count }), onRehydrate },
    );

  it("loads values written in the zustand persist envelope", () => {
    window.localStorage.setItem(key, JSON.stringify({ state: { count: 5 }, version: 0 }));

    expect(persisted().getState().count).toBe(5);
  });

  it("writes only the partialized state", () => {
    persisted().getState().increment();

    expect(JSON.parse(window.localStorage.getItem(key)!)).toEqual({ state: { count: 1 }, version: 0 });
  });

  it("ignores corrupt storage and still runs the rehydrate callback", () => {
    window.localStorage.setItem(key, "{not json");
    const onRehydrate = vi.fn();

    expect(persisted(onRehydrate).getState().count).toBe(0);
    expect(onRehydrate).toHaveBeenCalledWith(expect.objectContaining({ count: 0 }));
  });
});
