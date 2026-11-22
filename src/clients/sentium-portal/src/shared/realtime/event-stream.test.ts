import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { openEventStream as openStream, type EventStreamHandle, type StreamStatus } from "./event-stream";
import { FakeEventSource } from "@/test/fake-event-source";
import * as unauthorized from "@/shared/auth/unauthorized";

const URL = "/api/stream";

const handles: EventStreamHandle[] = [];

const openEventStream = (...args: Parameters<typeof openStream>) => {
  const handle = openStream(...args);
  handles.push(handle);
  return handle;
};

beforeEach(() => {
  vi.useFakeTimers();
  FakeEventSource.install();
});

afterEach(() => {
  handles.splice(0).forEach((handle) => handle.close());
  vi.useRealTimers();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe("openEventStream", () => {
  it("hands every frame over and reports the connection as open", () => {
    const frames: string[] = [];
    const statuses: StreamStatus[] = [];
    const handle = openEventStream(URL, { onFrame: (data) => frames.push(data), onStatus: (s) => statuses.push(s) });

    const source = FakeEventSource.last();
    source.open();
    source.emit("hello", "1");

    expect(frames).toEqual(["hello"]);
    expect(statuses).toEqual(["connecting", "open"]);
    handle.close();
    expect(source.closed).toBe(true);
  });

  it("reopens a stream the browser gave up on, resuming after the last event id", async () => {
    openEventStream(URL, { onFrame: () => {}, resume: true });
    const first = FakeEventSource.last();
    first.open();
    first.emit("a", "7");

    first.fail();
    await vi.advanceTimersByTimeAsync(1_000);

    expect(FakeEventSource.instances).toHaveLength(2);
    expect(FakeEventSource.last().url).toBe(`${URL}?after=7`);
  });

  it("leaves a network error to the browser's own reconnect", async () => {
    const statuses: StreamStatus[] = [];
    openEventStream(URL, { onFrame: () => {}, onStatus: (s) => statuses.push(s) });
    const source = FakeEventSource.last();
    source.open();

    source.drop();
    await vi.advanceTimersByTimeAsync(5_000);

    expect(FakeEventSource.instances).toHaveLength(1);
    expect(statuses.at(-1)).toBe("reconnecting");
  });

  it("treats missing heartbeats as a dead connection", async () => {
    openEventStream(URL, { onFrame: () => {}, heartbeatTimeoutMs: 1_000 });
    const source = FakeEventSource.last();
    source.open();

    await vi.advanceTimersByTimeAsync(900);
    source.ping();
    await vi.advanceTimersByTimeAsync(900);
    expect(source.closed).toBe(false);

    await vi.advanceTimersByTimeAsync(200);
    expect(source.closed).toBe(true);
    await vi.advanceTimersByTimeAsync(1_000);
    expect(FakeEventSource.instances).toHaveLength(2);
  });

  it("backs off further after every failure in a row", async () => {
    vi.spyOn(Math, "random").mockReturnValue(1);
    openEventStream(URL, { onFrame: () => {} });

    FakeEventSource.last().fail();
    await vi.advanceTimersByTimeAsync(1_000);
    expect(FakeEventSource.instances).toHaveLength(2);

    FakeEventSource.last().fail();
    await vi.advanceTimersByTimeAsync(1_999);
    expect(FakeEventSource.instances).toHaveLength(2);
    await vi.advanceTimersByTimeAsync(1);
    expect(FakeEventSource.instances).toHaveLength(3);
  });

  it("gives up after maxAttempts failed connections", async () => {
    const onGiveUp = vi.fn();
    openEventStream(URL, { onFrame: () => {}, maxAttempts: 2, onGiveUp });

    for (let i = 0; i < 3; i++) {
      FakeEventSource.last().fail();
      await vi.advanceTimersByTimeAsync(10_000);
    }

    expect(onGiveUp).toHaveBeenCalledTimes(1);
    expect(FakeEventSource.instances).toHaveLength(3);
  });

  it("hands an expired session to the login flow instead of retrying", async () => {
    FakeEventSource.install(401);
    const handleUnauthorized = vi.spyOn(unauthorized, "handleUnauthorized").mockImplementation(() => {
      throw new Error("Session expired.");
    });
    const statuses: StreamStatus[] = [];
    openEventStream(URL, { onFrame: () => {}, onStatus: (s) => statuses.push(s) });

    FakeEventSource.last().fail();
    await vi.advanceTimersByTimeAsync(60_000);

    expect(handleUnauthorized).toHaveBeenCalledTimes(1);
    expect(FakeEventSource.instances).toHaveLength(1);
    expect(statuses.at(-1)).toBe("closed");
  });

  it("reconnects at once when the browser comes back online", async () => {
    vi.spyOn(Math, "random").mockReturnValue(1);
    openEventStream(URL, { onFrame: () => {} });
    for (let i = 0; i < 4; i++) {
      FakeEventSource.last().fail();
      await vi.advanceTimersByTimeAsync(100);
    }
    const before = FakeEventSource.instances.length;

    window.dispatchEvent(new Event("online"));

    expect(FakeEventSource.instances).toHaveLength(before + 1);
  });

  it("never reconnects after close", async () => {
    const handle = openEventStream(URL, { onFrame: () => {} });
    const source = FakeEventSource.last();
    handle.close();

    source.fail();
    window.dispatchEvent(new Event("online"));
    await vi.advanceTimersByTimeAsync(60_000);

    expect(FakeEventSource.instances).toHaveLength(1);
  });
});
