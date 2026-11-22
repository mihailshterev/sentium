import { createStore } from "@/shared/state/create-store";
import type { LogEntry, Phase } from "@/features/orchestration/types/orchestration";
import {
  runWorkflowPipeline,
  runDynamicWorkflow,
  cancelOrchestrationRun,
} from "@/features/orchestration/api/orchestration.service";
import { BASE_URL } from "@/shared/api/client";
import { createFrameBatcher, openEventStream, type EventStreamHandle } from "@/shared/realtime";

interface StartPredefinedArgs {
  workflowId: string;
  scenario: string;
  workspaceId?: string;
  fileIds?: string[];
}

interface StartDynamicArgs {
  activity: string;
  workspaceId?: string;
  fileIds?: string[];
}

export type RunConnection = "idle" | "starting" | "connecting" | "waiting" | "streaming" | "reconnecting" | "stopping";

export type RunOutcome = "completed" | "stopped" | "error";

interface OrchestrationRunState {
  logs: LogEntry[];
  phase: Phase;
  isRunning: boolean;
  isDynamicRun: boolean;
  connection: RunConnection;
  runStartedAt: number | null;
  lastOutcome: RunOutcome | null;

  startPredefined: (args: StartPredefinedArgs) => Promise<void>;
  startDynamic: (args: StartDynamicArgs) => Promise<void>;
  stopRun: () => void;
  resetRun: () => void;
}

/** One relay frame: `{ author, text, type }`, or a terminal `{ type, done }` / `{ type: "error", message }`. */
interface RunFrame {
  type?: string;
  author?: string;
  text?: string;
  message?: string;
}

/** The run being followed, kept for this tab's session so a reload re-attaches to it. */
interface StoredRun {
  eventId: string;
  isDynamicRun: boolean;
  runStartedAt: number;
}

const ACTIVE_RUN_KEY = "sentium.orchestration.activeRun";
const STOP_FALLBACK_MS = 8_000;
/** Failed connections in a row before the run is given up on (the backoff makes this about a minute). */
const MAX_STREAM_ATTEMPTS = 6;
const STREAMING_TYPES = new Set(["message", "thought", "tool_call"]);
const COALESCED_TYPES = new Set(["message", "thought"]);
const TERMINAL_TYPES = new Set(["done", "cancelled", "gone", "error"]);

let stream: EventStreamHandle | null = null;
let currentStreamId: string | null = null;
let stopFallbackTimer: ReturnType<typeof setTimeout> | null = null;

const storage = {
  read: (): StoredRun | null => {
    try {
      const raw = sessionStorage.getItem(ACTIVE_RUN_KEY);
      return raw ? (JSON.parse(raw) as StoredRun) : null;
    } catch {
      return null;
    }
  },
  write: (run: StoredRun) => {
    try {
      sessionStorage.setItem(ACTIVE_RUN_KEY, JSON.stringify(run));
    } catch {
      // Storage unavailable (private mode, quota): the run just cannot be re-attached after a reload.
    }
  },
  clear: () => {
    try {
      sessionStorage.removeItem(ACTIVE_RUN_KEY);
    } catch {
      // Nothing to clear.
    }
  },
};

const clearStopFallback = () => {
  if (stopFallbackTimer) {
    clearTimeout(stopFallbackTimer);
    stopFallbackTimer = null;
  }
};

const closeStream = () => {
  clearStopFallback();
  stream?.close();
  stream = null;
  currentStreamId = null;
};

/** The phases a run announces in its `phase` frames. */
const RUN_PHASES = new Set<Phase>(["PLANNING", "SQUAD", "VALIDATING"]);

export const useOrchestrationRunStore = createStore<OrchestrationRunState>((set, get) => {
  const finalize = (outcome: RunOutcome, extra?: Partial<OrchestrationRunState>) => {
    batch.flush();
    closeStream();
    storage.clear();
    set({ phase: "COMPLETE", isRunning: false, connection: "idle", lastOutcome: outcome, ...extra });
  };

  const withNote = (text: string, type: LogEntry["type"] = "status"): LogEntry[] => [
    ...get().logs,
    { author: "System", text, type },
  ];

  const handleTerminal = (frame: RunFrame): boolean => {
    switch (frame.type) {
      case "done":
      case "cancelled": {
        const stopped = frame.type === "cancelled" || get().connection === "stopping";
        if (stopped) {
          finalize("stopped", { logs: withNote("Run stopped.") });
        } else {
          finalize("completed");
        }
        return true;
      }
      case "gone":
        finalize("completed", {
          logs: withNote("This run ended while the page was away; its result is in the history."),
        });
        return true;
      case "error":
        finalize("error", { logs: withNote(frame.message ?? frame.text ?? "The run ended with an error.", "error") });
        return true;
      default:
        return false;
    }
  };

  const appendFrame = (frame: RunFrame) => {
    if (frame.type === "phase") {
      if (RUN_PHASES.has(frame.text as Phase)) {
        set({ phase: frame.text as Phase });
      }
      return;
    }

    const type = (frame.type ?? "message") as LogEntry["type"];
    const author = frame.author ?? "Agent";
    const text = frame.text ?? "";

    if (!text) {
      return;
    }

    const connection = get().connection;
    if (STREAMING_TYPES.has(type) && (connection === "waiting" || connection === "connecting")) {
      set({ connection: "streaming" });
    }

    set((state) => {
      const last = state.logs.length - 1;
      if (
        COALESCED_TYPES.has(type) &&
        last >= 0 &&
        state.logs[last].author === author &&
        state.logs[last].type === type
      ) {
        const updated = [...state.logs];
        updated[last] = { ...updated[last], text: updated[last].text + text };
        return { logs: updated };
      }
      return { logs: [...state.logs, { author, text, type }] };
    });
  };

  // A re-attach replays the whole run at once; batching renders it a handful of times instead of once per token.
  const batch = createFrameBatcher<RunFrame>((frames) => frames.forEach(appendFrame));

  const onFrame = (data: string) => {
    if (!data || data === "null") {
      return;
    }

    let frame: RunFrame;
    try {
      frame = JSON.parse(data);
    } catch {
      return;
    }

    if (TERMINAL_TYPES.has(frame.type ?? "")) {
      batch.flush();
      handleTerminal(frame);
    } else {
      batch.push(frame);
    }
  };

  const onStreamStatus = (status: string) => {
    const connection = get().connection;
    if (connection === "stopping") {
      return;
    }
    if (status === "open" && connection !== "streaming") {
      set({ connection: "waiting" });
    } else if (status === "reconnecting") {
      set({ connection: "reconnecting" });
    } else if (status === "open" && connection === "reconnecting") {
      set({ connection: "streaming" });
    }
  };

  /** Follows the run from its first frame: the relay replays what was missed, so a re-attach rebuilds the log. */
  const openStream = (eventId: string) => {
    closeStream();
    currentStreamId = eventId;
    stream = openEventStream(`${BASE_URL}/praxis/orchestration/stream/${eventId}`, {
      resume: true,
      maxAttempts: MAX_STREAM_ATTEMPTS,
      onFrame,
      onStatus: onStreamStatus,
      onGiveUp: () => finalize("error", { logs: withNote("Lost the connection to the run.", "error") }),
    });
  };

  const beginRun = (isDynamicRun: boolean) => {
    batch.cancel();
    closeStream();
    set({
      logs: [],
      phase: "PLANNING",
      isRunning: true,
      isDynamicRun,
      connection: "starting",
      runStartedAt: Date.now(),
      lastOutcome: null,
    });
  };

  const follow = (eventId: string) => {
    const { isDynamicRun, runStartedAt } = get();
    storage.write({ eventId, isDynamicRun, runStartedAt: runStartedAt ?? Date.now() });
    set({ connection: "connecting" });
    openStream(eventId);
  };

  const stored = typeof window !== "undefined" ? storage.read() : null;
  if (stored) {
    // A reload while a run was going: pick it up again once the store exists.
    queueMicrotask(() => openStream(stored.eventId));
  }

  return {
    logs: [],
    phase: stored ? "PLANNING" : "IDLE",
    isRunning: !!stored,
    isDynamicRun: stored?.isDynamicRun ?? false,
    connection: stored ? "connecting" : "idle",
    runStartedAt: stored?.runStartedAt ?? null,
    lastOutcome: null,

    startPredefined: async ({ workflowId, scenario, workspaceId, fileIds }) => {
      beginRun(false);
      try {
        const { eventId } = await runWorkflowPipeline({
          workflowId,
          scenario,
          ...(workspaceId && { workspaceId }),
          ...(fileIds?.length && { fileIds }),
        });
        follow(eventId);
      } catch {
        finalize("error");
      }
    },

    startDynamic: async ({ activity, workspaceId, fileIds }) => {
      beginRun(true);
      try {
        const { eventId } = await runDynamicWorkflow({
          activity,
          ...(workspaceId && { workspaceId }),
          ...(fileIds?.length && { fileIds }),
        });
        follow(eventId);
      } catch {
        finalize("error");
      }
    },

    stopRun: () => {
      if (!get().isRunning) {
        return;
      }

      const streamId = currentStreamId;
      set({ connection: "stopping" });

      if (!streamId) {
        finalize("stopped", { logs: withNote("Run stopped.") });
        return;
      }

      void cancelOrchestrationRun(streamId).catch(() => {});

      clearStopFallback();
      stopFallbackTimer = setTimeout(() => {
        finalize("stopped", { logs: withNote("Run stopped.") });
      }, STOP_FALLBACK_MS);
    },

    resetRun: () => {
      if (get().isRunning) {
        return;
      }
      set({ logs: [], phase: "IDLE", isDynamicRun: false, connection: "idle", runStartedAt: null, lastOutcome: null });
    },
  };
});
