import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { createFrameBatcher, mergeTextFrames } from "./frame-batcher";

beforeEach(() => vi.useFakeTimers());
afterEach(() => vi.useRealTimers());

describe("createFrameBatcher", () => {
  it("hands frames over together once the batch window ends", () => {
    const apply = vi.fn();
    const batch = createFrameBatcher<number>(apply, 50);

    batch.push(1);
    batch.push(2);
    expect(apply).not.toHaveBeenCalled();

    vi.advanceTimersByTime(50);
    expect(apply).toHaveBeenCalledExactlyOnceWith([1, 2]);
  });

  it("flushes at once, and cancel drops what waits", () => {
    const apply = vi.fn();
    const batch = createFrameBatcher<number>(apply, 50);

    batch.push(1);
    batch.flush();
    batch.push(2);
    batch.cancel();
    vi.advanceTimersByTime(100);

    expect(apply).toHaveBeenCalledExactlyOnceWith([1]);
  });
});

describe("mergeTextFrames", () => {
  it("joins runs of the same mergeable type and keeps everything else in order", () => {
    const merged = mergeTextFrames(
      [
        { type: "thought", text: "a" },
        { type: "thought", text: "b" },
        { type: "tool_call", text: "{}" },
        { type: "tool_call", text: "{}" },
        { type: "message", text: "c" },
        { type: "message", text: "d" },
      ],
      new Set(["message", "thought"]),
    );

    expect(merged).toEqual([
      { type: "thought", text: "ab" },
      { type: "tool_call", text: "{}" },
      { type: "tool_call", text: "{}" },
      { type: "message", text: "cd" },
    ]);
  });
});
