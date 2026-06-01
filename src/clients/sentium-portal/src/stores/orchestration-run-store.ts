import { create } from "zustand";
import type { LogEntry, Phase } from "../types/orchestration";
import { runWorkflowPipeline, runDynamicWorkflow } from "../services/agentRuntime.service";
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

interface OrchestrationRunState {
  logs: LogEntry[];
  phase: Phase;
  isRunning: boolean;

  startPredefined: (args: StartPredefinedArgs) => Promise<void>;
  startDynamic: (args: StartDynamicArgs) => Promise<void>;
  stopRun: () => void;
  resetRun: () => void;
}

let eventSource: EventSource | null = null;

const closeStream = () => {
  eventSource?.close();
  eventSource = null;
};

export const useOrchestrationRunStore = create<OrchestrationRunState>((set, get) => {
  const openStream = (eventId: string) => {
    closeStream();

    const source = new EventSource(`${BASE_URL}/agent-runtime/orchestration/stream/${eventId}`, {
      withCredentials: true,
    });
    eventSource = source;

    source.onmessage = (e) => {
      if (!e.data || e.data === "null") {
        return;
      }

      let data: {
        Author?: string;
        author?: string;
        Text?: string;
        text?: string;
        Type?: string;
        type?: string;
      };
      try {
        data = JSON.parse(e.data);
      } catch {
        return;
      }

      const author = data.Author ?? data.author ?? "Agent";
      const text = data.Text ?? data.text ?? "";
      const type = (data.Type ?? data.type ?? "message") as LogEntry["type"];

      if (type === "message") {
        const a = author.toLowerCase();
        if (a.includes("planner")) {
          set({ phase: "PLANNING" });
        } else if (a.includes("validator")) {
          set({ phase: "VALIDATING" });
        } else {
          set({ phase: "SQUAD" });
        }
      }

      if (!text) {
        return;
      }

      set((state) => {
        const last = state.logs.length - 1;
        if (
          (type === "message" || type === "thought") &&
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

    source.onerror = () => {
      closeStream();
      set({ phase: "COMPLETE", isRunning: false });
    };
  };

  const beginRun = () => {
    closeStream();
    set({ logs: [], phase: "PLANNING", isRunning: true });
  };

  return {
    logs: [],
    phase: "IDLE",
    isRunning: false,

    startPredefined: async ({ workflowId, scenario, workspaceId }) => {
      beginRun();
      try {
        const { eventId } = await runWorkflowPipeline({
          workflowId,
          scenario,
          ...(workspaceId && { workspaceId }),
        });
        openStream(eventId);
      } catch {
        set({ phase: "COMPLETE", isRunning: false });
      }
    },

    startDynamic: async ({ activity, workspaceId }) => {
      beginRun();
      try {
        const { eventId } = await runDynamicWorkflow({
          activity,
          ...(workspaceId && { workspaceId }),
        });
        openStream(eventId);
      } catch {
        set({ phase: "COMPLETE", isRunning: false });
      }
    },

    stopRun: () => {
      closeStream();
      set({ phase: "COMPLETE", isRunning: false });
    },

    resetRun: () => {
      if (get().isRunning) {
        return;
      }
      set({ logs: [], phase: "IDLE" });
    },
  };
});
