import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import {
  startRealtime,
  stopRealtime,
  subscribeTopic,
  useRealtimeStatusStore,
  type RealtimeMessage,
} from "./realtime-client";
import { REALTIME_TOPICS } from "./topics";
import { FakeEventSource } from "@/test/fake-event-source";

const frame = (topic: string, data?: unknown) => JSON.stringify({ topic, data });

beforeEach(() => {
  FakeEventSource.install();
});

afterEach(() => {
  stopRealtime();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe("realtime client without cross-tab support", () => {
  beforeEach(() => {
    vi.stubGlobal("BroadcastChannel", undefined);
  });

  it("opens one stream however often it is started", () => {
    startRealtime();
    startRealtime();

    expect(FakeEventSource.instances).toHaveLength(1);
    expect(FakeEventSource.last().url).toMatch(/\/api\/realtime\/stream$/);
  });

  it("delivers events only to the topic's subscribers", () => {
    const vault: RealtimeMessage[] = [];
    const other: RealtimeMessage[] = [];
    subscribeTopic(REALTIME_TOPICS.vault, (m) => vault.push(m));
    subscribeTopic(REALTIME_TOPICS.evaluations, (m) => other.push(m));
    startRealtime();

    FakeEventSource.last().emit(frame(REALTIME_TOPICS.vault, { id: 1 }));

    expect(vault).toEqual([{ kind: "event", topic: REALTIME_TOPICS.vault, data: { id: 1 } }]);
    expect(other).toEqual([]);
  });

  it("asks every subscriber to resync once when the stream (re)connects, whatever its topic count", () => {
    const messages: RealtimeMessage[] = [];
    const unsubscribe = subscribeTopic([REALTIME_TOPICS.vault, REALTIME_TOPICS.evaluations], (m) => messages.push(m));
    startRealtime();

    FakeEventSource.last().emit(frame(REALTIME_TOPICS.connected));
    unsubscribe();
    FakeEventSource.last().emit(frame(REALTIME_TOPICS.vault));

    expect(messages).toEqual([{ kind: "resync" }]);
  });

  it("keeps delivering to the others when one subscriber throws", () => {
    const received = vi.fn();
    subscribeTopic(REALTIME_TOPICS.vault, () => {
      throw new Error("broken");
    });
    subscribeTopic(REALTIME_TOPICS.vault, received);
    startRealtime();

    FakeEventSource.last().emit(frame(REALTIME_TOPICS.vault));

    expect(received).toHaveBeenCalledTimes(1);
  });

  it("reports the connection status, and offline once stopped", () => {
    startRealtime();
    FakeEventSource.last().open();
    expect(useRealtimeStatusStore.getState().status).toBe("live");

    stopRealtime();
    expect(useRealtimeStatusStore.getState().status).toBe("offline");
    expect(FakeEventSource.last().closed).toBe(true);
  });
});

describe("realtime client shared across tabs", () => {
  class FakeChannel {
    static channels: FakeChannel[] = [];
    onmessage: ((event: MessageEvent) => void) | null = null;
    closed = false;

    readonly name: string;

    constructor(name: string) {
      this.name = name;
      FakeChannel.channels.push(this);
    }

    postMessage(data: unknown) {
      for (const channel of FakeChannel.channels) {
        if (channel !== this && !channel.closed && channel.name === this.name) {
          channel.onmessage?.({ data } as MessageEvent);
        }
      }
    }

    close() {
      this.closed = true;
    }
  }

  let grantLock: () => void;

  beforeEach(() => {
    FakeChannel.channels = [];
    vi.stubGlobal("BroadcastChannel", FakeChannel);
    vi.stubGlobal("navigator", {
      locks: {
        request: (_name: string, _options: unknown, callback: () => Promise<void> | undefined) =>
          new Promise<void>((resolve) => {
            grantLock = () => void Promise.resolve(callback()).then(resolve);
          }),
      },
    });
  });

  it("connects only once it holds the leader lock", () => {
    startRealtime();
    expect(FakeEventSource.instances).toHaveLength(0);
    expect(useRealtimeStatusStore.getState().status).toBe("connecting");

    grantLock();

    expect(FakeEventSource.instances).toHaveLength(1);
  });

  it("relays the leader's frames and status to the other tabs", () => {
    startRealtime();
    grantLock();
    const otherTab = new FakeChannel("sentium-realtime");
    const relayed: unknown[] = [];
    otherTab.onmessage = (event) => relayed.push(event.data);

    FakeEventSource.last().open();
    FakeEventSource.last().emit(frame(REALTIME_TOPICS.vault));

    expect(relayed).toEqual([
      { type: "status", status: "live" },
      { type: "frame", data: frame(REALTIME_TOPICS.vault) },
    ]);
  });

  it("as a follower, takes the leader's frames and status", () => {
    const received = vi.fn();
    subscribeTopic(REALTIME_TOPICS.vault, received);
    startRealtime();
    const leaderTab = new FakeChannel("sentium-realtime");

    leaderTab.postMessage({ type: "status", status: "live" });
    leaderTab.postMessage({ type: "frame", data: frame(REALTIME_TOPICS.vault) });

    expect(useRealtimeStatusStore.getState().status).toBe("live");
    expect(received).toHaveBeenCalledTimes(1);
    expect(FakeEventSource.instances).toHaveLength(0);
  });
});
