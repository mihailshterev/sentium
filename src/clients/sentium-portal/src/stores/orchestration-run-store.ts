import { create } from "zustand";
import type { LogEntry, Phase } from "../types/orchestration";
import { runWorkflowPipeline, runDynamicWorkflow, cancelOrchestrationRun } from "../services/agentRuntime.service";
import { BASE_URL } from "../api/client";

interface StartPredefinedArgs {
  workflowId: string;
  scenario: string;
  workspaceId?: string;
}

interface StartDynamicArgs {
  activity: string;
  workspaceId?: string;
}

export type RunConnection = "idle" | "starting" | "connecting" | "waiting" | "streaming" | "stopping";

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

let eventSource: EventSource | null = null;
let currentStreamId: string | null = null;
let stopFallbackTimer: ReturnType<typeof setTimeout> | null = null;

const STOP_FALLBACK_MS = 8_000;

const clearStopFallback = () => {
  if (stopFallbackTimer) {
    clearTimeout(stopFallbackTimer);
    stopFallbackTimer = null;
  }
};

const closeStream = () => {
  clearStopFallback();
  eventSource?.close();
  eventSource = null;
  currentStreamId = null;
};

export const useOrchestrationRunStore = create<OrchestrationRunState>((set, get) => {
  const finalize = (outcome: RunOutcome, extra?: Partial<OrchestrationRunState>) => {
    closeStream();
    set({ phase: "COMPLETE", isRunning: false, connection: "idle", lastOutcome: outcome, ...extra });
  };

  const stoppedNote = (): LogEntry[] => [...get().logs, { author: "System", text: "Run stopped.", type: "status" }];

  const openStream = (eventId: string) => {
    closeStream();
    currentStreamId = eventId;

    let reconnectCount = 0;
    const MAX_RECONNECTS = 4;

    const source = new EventSource(`${BASE_URL}/agent-runtime/orchestration/stream/${eventId}`, {
      withCredentials: true,
    });
    eventSource = source;

    source.onopen = () => {
      reconnectCount = 0;
      if (get().connection !== "streaming" && get().connection !== "stopping") {
        set({ connection: "waiting" });
      }
    };

    source.onmessage = (e) => {
      reconnectCount = 0;

      if (!e.data || e.data === "null") {
        return;
      }

      let data: {
        type?: string;
        Author?: string;
        author?: string;
        Text?: string;
        text?: string;
        Type?: string;
        message?: string;
        Message?: string;
      };
      try {
        data = JSON.parse(e.data);
      } catch {
        return;
      }

      const type = data.Type ?? data.type ?? "message";

      if (type === "done" || type === "cancelled") {
        const stopped = type === "cancelled" || get().connection === "stopping";
        if (stopped) {
          finalize("stopped", { logs: stoppedNote() });
        } else {
          finalize("completed");
        }
        return;
      }

      if (type === "error") {
        const errorText = data.message ?? data.Message ?? data.text ?? data.Text ?? "The run ended with an error.";
        finalize("error", { logs: [...get().logs, { author: "System", text: errorText, type: "error" }] });
        return;
      }

      const author = data.Author ?? data.author ?? "Agent";
      const text = data.Text ?? data.text ?? "";
      const entryType = type as LogEntry["type"];

      if (entryType === "message") {
        const a = author.toLowerCase();
        if (a.includes("orchestrator") || a.includes("planner")) {
          set({ phase: "PLANNING" });
        } else if (a.includes("validator")) {
          set({ phase: "VALIDATING" });
        } else {
          set({ phase: "SQUAD" });
        }
      } else if (entryType === "status") {
        const t = text.toLowerCase();
        if (t.includes("validat") || t.includes("review")) {
          set({ phase: "VALIDATING" });
        } else if (t.includes("squad") || t.includes("rewriting")) {
          set({ phase: "SQUAD" });
        }
      }

      if (!text) {
        return;
      }

      if (
        (entryType === "message" || entryType === "thought" || entryType === "tool") &&
        (get().connection === "waiting" || get().connection === "connecting")
      ) {
        set({ connection: "streaming" });
      }

      set((state) => {
        const last = state.logs.length - 1;
        if (
          (entryType === "message" || entryType === "thought") &&
          last >= 0 &&
          state.logs[last].author === author &&
          state.logs[last].type === entryType
        ) {
          const updated = [...state.logs];
          updated[last] = { ...updated[last], text: updated[last].text + text };
          return { logs: updated };
        }
        return { logs: [...state.logs, { author, text, type: entryType }] };
      });
    };

    source.onerror = () => {
      reconnectCount++;
      if (reconnectCount > MAX_RECONNECTS) {
        finalize("error");
      }
    };
  };

  const beginRun = () => {
    closeStream();
    set({
      logs: [],
      phase: "PLANNING",
      isRunning: true,
      connection: "starting",
      runStartedAt: Date.now(),
      lastOutcome: null,
    });
  };

  return {
    logs: [],
    phase: "IDLE",
    isRunning: false,
    isDynamicRun: false,
    connection: "idle",
    runStartedAt: null,
    lastOutcome: null,

    startPredefined: async ({ workflowId, scenario, workspaceId }) => {
      beginRun();
      set({ isDynamicRun: false });
      try {
        const { eventId } = await runWorkflowPipeline({
          workflowId,
          scenario,
          ...(workspaceId && { workspaceId }),
        });
        set({ connection: "connecting" });
        openStream(eventId);
      } catch {
        finalize("error");
      }
    },

    startDynamic: async ({ activity, workspaceId }) => {
      beginRun();
      set({ isDynamicRun: true });
      try {
        const { eventId } = await runDynamicWorkflow({
          activity,
          ...(workspaceId && { workspaceId }),
        });
        set({ connection: "connecting" });
        openStream(eventId);
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

      if (streamId) {
        void cancelOrchestrationRun(streamId).catch(() => {});
      }

      clearStopFallback();
      stopFallbackTimer = setTimeout(() => {
        finalize("stopped", { logs: stoppedNote() });
      }, STOP_FALLBACK_MS);

      if (!streamId) {
        finalize("stopped", { logs: stoppedNote() });
      }
    },

    resetRun: () => {
      if (get().isRunning) {
        return;
      }
      set({ logs: [], phase: "IDLE", isDynamicRun: false, connection: "idle", runStartedAt: null, lastOutcome: null });
    },
  };
});
