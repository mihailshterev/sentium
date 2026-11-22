import { BASE_URL } from "@/shared/api/config";
import { handleUnauthorized } from "@/shared/auth/unauthorized";

export type StreamStatus = "connecting" | "open" | "reconnecting" | "closed";

export interface EventStreamOptions {
  /** Every unnamed event's data and id. */
  onFrame: (data: string, id: string) => void;
  onStatus?: (status: StreamStatus) => void;
  /** Called once when reconnecting gives up after `maxAttempts`; never after `close()`. */
  onGiveUp?: () => void;
  /** Reopen after the last event id received (`?after=`), so a stream with ids resumes instead of restarting. */
  resume?: boolean;
  /** The id to resume after on the first connection. */
  after?: string;
  /** Failed connections in a row before giving up; unlimited when omitted. */
  maxAttempts?: number;
  /** Silence (no event and no heartbeat) after which the connection counts as dead. */
  heartbeatTimeoutMs?: number;
}

export interface EventStreamHandle {
  close: () => void;
}

/** The server sends a `ping` event every 15 seconds; three missed ones mean the connection silently died. */
const DEFAULT_HEARTBEAT_TIMEOUT_MS = 45_000;
const BASE_RETRY_DELAY_MS = 1_000;
const MAX_RETRY_DELAY_MS = 30_000;
const PING_EVENT = "ping";
const SESSION_PROBE_URL = `${BASE_URL}/identity/account/session`;

const withAfter = (url: string, after: string) =>
  `${url}${url.includes("?") ? "&" : "?"}after=${encodeURIComponent(after)}`;

/** Exponential with "equal jitter": half fixed, half random, so many tabs never reconnect in lockstep. */
const retryDelay = (failures: number) => {
  const ceiling = Math.min(MAX_RETRY_DELAY_MS, BASE_RETRY_DELAY_MS * 2 ** (failures - 1));
  return ceiling / 2 + Math.random() * (ceiling / 2);
};

/** An EventSource cannot see the status of a failed response, so a cheap request tells whether the session expired. */
const sessionExpired = async (): Promise<boolean> => {
  try {
    const response = await fetch(SESSION_PROBE_URL, { credentials: "include" });
    return response.status === 401;
  } catch {
    return false;
  }
};

/**
 * An EventSource that recovers from everything a plain one does not:
 * - a non-2xx response (a 502 while the server restarts, a 401) closes an EventSource for good; this reopens it with
 *   jittered exponential backoff, or hands a 401 to the session-expired flow;
 * - a connection that silently died (after sleep, a dropped Wi-Fi) is noticed by the missing heartbeat;
 * - coming back online or back to a hidden tab reconnects at once instead of waiting out the backoff.
 * The browser's own reconnects (after a network error) send `Last-Event-ID`; reopening sends `?after=` instead.
 */
export function openEventStream(url: string, options: EventStreamOptions): EventStreamHandle {
  const heartbeatTimeoutMs = options.heartbeatTimeoutMs ?? DEFAULT_HEARTBEAT_TIMEOUT_MS;

  let source: EventSource | null = null;
  let lastEventId = options.after ?? "";
  let lastActivity = Date.now();
  let failures = 0;
  let closed = false;
  let watchdog: ReturnType<typeof setTimeout> | null = null;
  let retryTimer: ReturnType<typeof setTimeout> | null = null;

  const setStatus = (status: StreamStatus) => options.onStatus?.(status);

  const clearTimers = () => {
    if (watchdog) {
      clearTimeout(watchdog);
      watchdog = null;
    }
    if (retryTimer) {
      clearTimeout(retryTimer);
      retryTimer = null;
    }
  };

  const dropSource = () => {
    source?.close();
    source = null;
  };

  /**
   * Fires at the deadline the last activity set. Activity itself only records a timestamp - a streamed answer is many
   * frames a second - and the timer re-arms for whatever time is left when it finds the connection was active.
   */
  const checkHeartbeat = () => {
    const silentFor = Date.now() - lastActivity;
    if (silentFor >= heartbeatTimeoutMs) {
      watchdog = null;
      void recover();
      return;
    }
    watchdog = setTimeout(checkHeartbeat, heartbeatTimeoutMs - silentFor);
  };

  const noteActivity = () => {
    lastActivity = Date.now();
    watchdog ??= setTimeout(checkHeartbeat, heartbeatTimeoutMs);
  };

  const connect = () => {
    clearTimers();
    dropSource();
    if (closed) {
      return;
    }

    setStatus(failures === 0 ? "connecting" : "reconnecting");
    const es = new EventSource(options.resume && lastEventId ? withAfter(url, lastEventId) : url, {
      withCredentials: true,
    });
    source = es;
    noteActivity();

    es.onopen = () => {
      failures = 0;
      noteActivity();
      setStatus("open");
    };

    es.onmessage = (event: MessageEvent<string>) => {
      if (event.lastEventId) {
        lastEventId = event.lastEventId;
      }
      noteActivity();
      options.onFrame(event.data, event.lastEventId);
    };

    es.addEventListener(PING_EVENT, noteActivity);

    es.onerror = () => {
      if (closed || source !== es) {
        return;
      }
      if (es.readyState === EventSource.CLOSED) {
        void recover();
      } else {
        // The browser retries on its own after a network error; the watchdog covers it never succeeding.
        setStatus("reconnecting");
      }
    };
  };

  const recover = async () => {
    clearTimers();
    dropSource();
    if (closed) {
      return;
    }

    failures++;
    if (options.maxAttempts !== undefined && failures > options.maxAttempts) {
      shutDown();
      options.onGiveUp?.();
      return;
    }

    setStatus("reconnecting");
    if (await sessionExpired()) {
      shutDown();
      try {
        handleUnauthorized();
      } catch {
        // handleUnauthorized throws to abort its caller; there is nothing to abort here.
      }
      return;
    }

    if (!closed) {
      retryTimer = setTimeout(connect, retryDelay(failures));
    }
  };

  const reconnectNow = () => {
    if (closed || source?.readyState === EventSource.OPEN) {
      return;
    }
    connect();
  };

  const onVisibilityChange = () => {
    if (document.visibilityState !== "visible" || closed) {
      return;
    }
    // Timers barely run in a hidden tab, so a connection that died meanwhile may not have been noticed yet.
    if (Date.now() - lastActivity > heartbeatTimeoutMs) {
      connect();
    } else {
      reconnectNow();
    }
  };

  const shutDown = () => {
    closed = true;
    clearTimers();
    dropSource();
    window.removeEventListener("online", reconnectNow);
    document.removeEventListener("visibilitychange", onVisibilityChange);
    setStatus("closed");
  };

  window.addEventListener("online", reconnectNow);
  document.addEventListener("visibilitychange", onVisibilityChange);
  connect();

  return {
    close: () => {
      if (!closed) {
        shutDown();
      }
    },
  };
}
