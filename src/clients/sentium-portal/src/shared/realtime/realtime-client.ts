import { BASE_URL } from "@/shared/api/config";
import { createStore } from "@/shared/state/create-store";
import { openEventStream, type EventStreamHandle, type StreamStatus } from "./event-stream";
import { REALTIME_TOPICS } from "./topics";

export type RealtimeStatus = "offline" | "connecting" | "live" | "reconnecting";

/**
 * An event on a subscribed topic, or `resync`: the connection was (re)established, so anything shown may be stale
 * and should be refetched.
 */
export type RealtimeMessage = { kind: "event"; topic: string; data: unknown } | { kind: "resync" };

export type RealtimeHandler = (message: RealtimeMessage) => void;

type TabMessage = { type: "frame"; data: string } | { type: "status"; status: RealtimeStatus } | { type: "hello" };

const STREAM_URL = `${BASE_URL}/realtime/stream`;
const LEADER_LOCK = "sentium-realtime-leader";
const TAB_CHANNEL = "sentium-realtime";

export const useRealtimeStatusStore = createStore<{ status: RealtimeStatus }>(() => ({ status: "offline" }));

const handlers = new Map<string, Set<RealtimeHandler>>();

let session: { stop: () => void } | null = null;

const setStatus = (status: RealtimeStatus) => useRealtimeStatusStore.setState({ status });

const toRealtimeStatus = (status: StreamStatus): RealtimeStatus => {
  switch (status) {
    case "open":
      return "live";
    case "closed":
      return "offline";
    default:
      return status;
  }
};

const notify = (handler: RealtimeHandler, message: RealtimeMessage) => {
  try {
    handler(message);
  } catch {
    // One broken subscriber must not starve the others.
  }
};

const dispatchFrame = (raw: string) => {
  let frame: { topic?: string; data?: unknown };
  try {
    frame = JSON.parse(raw);
  } catch {
    return;
  }

  if (frame.topic === REALTIME_TOPICS.connected) {
    // A handler subscribed to several topics is in several sets, but must refetch once per reconnect.
    const unique = new Set<RealtimeHandler>();
    handlers.forEach((set) => set.forEach((handler) => unique.add(handler)));
    unique.forEach((handler) => notify(handler, { kind: "resync" }));
    return;
  }

  if (frame.topic) {
    const message: RealtimeMessage = { kind: "event", topic: frame.topic, data: frame.data };
    handlers.get(frame.topic)?.forEach((handler) => notify(handler, message));
  }
};

/** Calls `handler` for every event on `topics`, and with `resync` after every (re)connect. Returns the unsubscribe. */
export function subscribeTopic(topics: string | readonly string[], handler: RealtimeHandler): () => void {
  const list = typeof topics === "string" ? [topics] : topics;
  for (const topic of list) {
    let set = handlers.get(topic);
    if (!set) {
      set = new Set();
      handlers.set(topic, set);
    }
    set.add(handler);
  }

  return () => {
    for (const topic of list) {
      const set = handlers.get(topic);
      set?.delete(handler);
      if (set?.size === 0) {
        handlers.delete(topic);
      }
    }
  };
}

/**
 * Opens the signed-in user's realtime stream, once per browser: tabs elect a leader through the Web Locks API, the
 * leader holds the only connection and relays every frame to the other tabs over a BroadcastChannel, and when the
 * leader tab closes the next one takes over (its own "connected" frame makes every tab refetch). Without those APIs
 * each tab connects by itself.
 */
export function startRealtime(): void {
  // Without EventSource (an old browser, a test environment) the pages fall back to their own polling.
  if (session || typeof EventSource === "undefined") {
    return;
  }
  session = canShareAcrossTabs() ? shareAcrossTabs() : connectAlone();
}

export function stopRealtime(): void {
  session?.stop();
  session = null;
  setStatus("offline");
}

const canShareAcrossTabs = () =>
  typeof BroadcastChannel !== "undefined" && typeof navigator !== "undefined" && !!navigator.locks?.request;

const openStream = (onStatus: (status: RealtimeStatus) => void, onFrame: (data: string) => void): EventStreamHandle =>
  openEventStream(STREAM_URL, {
    onFrame: (data) => onFrame(data),
    onStatus: (status) => onStatus(toRealtimeStatus(status)),
  });

function connectAlone(): { stop: () => void } {
  const stream = openStream(setStatus, dispatchFrame);
  return { stop: () => stream.close() };
}

function shareAcrossTabs(): { stop: () => void } {
  const channel = new BroadcastChannel(TAB_CHANNEL);
  const abort = new AbortController();
  let stream: EventStreamHandle | null = null;
  let releaseLock: (() => void) | null = null;

  const post = (message: TabMessage) => {
    try {
      channel.postMessage(message);
    } catch {
      // The channel closed while stopping.
    }
  };

  const lead = () => {
    stream = openStream(
      (status) => {
        setStatus(status);
        post({ type: "status", status });
      },
      (data) => {
        dispatchFrame(data);
        post({ type: "frame", data });
      },
    );
  };

  channel.onmessage = (event: MessageEvent<TabMessage>) => {
    const message = event.data;
    if (stream) {
      if (message.type === "hello") {
        post({ type: "status", status: useRealtimeStatusStore.getState().status });
      }
      return;
    }
    if (message.type === "frame") {
      dispatchFrame(message.data);
    } else if (message.type === "status") {
      setStatus(message.status);
    }
  };

  setStatus("connecting");
  post({ type: "hello" });

  navigator.locks
    .request(LEADER_LOCK, { signal: abort.signal }, () => {
      if (abort.signal.aborted) {
        return undefined;
      }
      lead();
      return new Promise<void>((resolve) => {
        releaseLock = resolve;
      });
    })
    .catch(() => {
      // Aborted while waiting to lead: this tab is stopping.
    });

  return {
    stop: () => {
      abort.abort();
      stream?.close();
      stream = null;
      releaseLock?.();
      channel.close();
    },
  };
}
