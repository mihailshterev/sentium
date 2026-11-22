import { createStore } from "@/shared/state/create-store";
import type { ConversationMessage, MessageAttachment } from "@/features/assistant/types/assistant";
import { DEFAULT_ASSISTANT_MODEL } from "@/shared/utils/constants";
import {
  approveToolCall,
  cancelChatTurn,
  chatTurnStreamUrl,
  fetchConversation,
  startChatTurn,
} from "@/features/assistant/api/assistant.service";
import { messageFromDto } from "@/features/assistant/utils/message-from-dto";
import { createFrameBatcher, mergeTextFrames, openEventStream } from "@/shared/realtime";
import {
  appendContent as appendAnswer,
  appendThought,
  decideApproval,
  finalizeTurn,
  finishTool,
  legacyToolName,
  parseJson,
  requestApproval,
  startTool,
  type ActivityTurn,
  type ApprovalFrame,
  type ToolCallFrame,
  type ToolResultFrame,
} from "@/shared/agent-activity";

export type MessageUpdateKind = "content" | "thought" | "tool" | "tool_call" | "tool_result" | "approval";

interface SendMessageArgs {
  conversationId: string | null;
  model: string;
  userContent: string;
  /** Workspace files attached to this message; their text is put in front of the agent for this turn. */
  attachments?: MessageAttachment[];
}

interface RespondToApprovalArgs {
  aiMsgId: string;
  requestId: string;
  approved: boolean;
  conversationId: string | null;
}

/** One relay frame of a turn: `{ type, text }`, or a terminal `{ type, done }` / `{ type: "error", message }`. */
interface TurnFrame {
  type?: string;
  text?: string;
  message?: string;
}

/** The turn being followed, kept for this tab's session so a reload re-attaches to it. */
interface StoredTurn {
  turnId: string;
  conversationId: string;
  /** The user message of a chat turn: it is only saved with the answer, so a reload must show it again. */
  userContent?: string;
  attachments?: MessageAttachment[];
}

interface ActiveTurn {
  turnId: string;
  /** Stop following: `stop` waits briefly for the server's "cancelled" frame, `abandon` lets go at once. */
  stop: () => void;
  abandon: () => void;
}

interface CreateConversationStoreOptions {
  /** A sessionStorage key: set it on the one store whose turns should survive a reload. */
  resumeKey?: string;
}

const STOP_FALLBACK_MS = 8_000;
/** Failed connections in a row before the turn is given up on (the backoff makes this about a minute). */
const MAX_STREAM_ATTEMPTS = 6;
const LOST_CONNECTION_MESSAGE = "Lost the connection to the assistant. Please try again.";
const ACTIVITY_TYPES = new Set<string>(["thought", "tool", "tool_call", "tool_result"]);
/** Frames whose text simply concatenates, so a batch of them applies as one update. */
const MERGEABLE_TYPES = new Set<string>(["message", "thought"]);

const streamErrorMessage = (err: unknown, fallback: string): string =>
  err instanceof Error && err.message.startsWith("Session expired") ? err.message : fallback;

const turnStorage = (key: string | undefined) => ({
  read: (): StoredTurn | null => {
    if (!key) {
      return null;
    }
    try {
      const raw = sessionStorage.getItem(key);
      return raw ? (JSON.parse(raw) as StoredTurn) : null;
    } catch {
      return null;
    }
  },
  write: (turn: StoredTurn) => {
    if (!key) {
      return;
    }
    try {
      sessionStorage.setItem(key, JSON.stringify(turn));
    } catch {
      // Storage unavailable: the turn just cannot be re-attached after a reload.
    }
  },
  clear: () => {
    if (!key) {
      return;
    }
    try {
      sessionStorage.removeItem(key);
    } catch {
      // Nothing to clear.
    }
  },
});

/** `msg` with one streamed update applied, or null when the update is malformed. */
const withUpdate = (
  msg: ConversationMessage,
  appendContent: string,
  type: MessageUpdateKind,
  streamingConversationId: string | null,
): ConversationMessage | null => {
  const turn: ActivityTurn = { content: msg.content, steps: msg.steps ?? [] };
  let next: ActivityTurn;
  let pendingApproval = msg.pendingApproval;

  switch (type) {
    case "thought":
      next = appendThought(turn, appendContent);
      break;
    case "tool_call": {
      const frame = parseJson<ToolCallFrame>(appendContent);
      if (!frame) return null;
      next = startTool(turn, frame);
      break;
    }
    case "tool_result": {
      const frame = parseJson<ToolResultFrame>(appendContent);
      if (!frame) return null;
      next = finishTool(turn, frame);
      break;
    }
    case "tool":
      next = startTool(turn, { callId: "", name: legacyToolName(appendContent) });
      break;
    case "approval": {
      const frame = parseJson<ApprovalFrame>(appendContent);
      if (!frame) return null;
      next = requestApproval(turn, frame);
      pendingApproval = {
        requestId: frame.requestId,
        toolName: frame.toolName,
        arguments: frame.arguments ?? {},
        callId: frame.callId ?? undefined,
        conversationId: streamingConversationId ?? undefined,
      };
      break;
    }
    default:
      next = appendAnswer(turn, appendContent);
  }

  return { ...msg, content: next.content, steps: next.steps, pendingApproval };
};

/** A conversation left while its turn streams: its messages, kept up to date until it is shown again. */
interface ParkedThread {
  conversationId: string;
  messages: ConversationMessage[];
}

interface ConversationState {
  activeConversationId: string | null;
  messages: ConversationMessage[];
  model: string;
  isStreaming: boolean;
  streamingConversationId: string | null;
  conversationTitles: Record<string, string>;

  setActiveConversation: (id: string | null, messages: ConversationMessage[], model: string) => void;
  appendMessage: (message: ConversationMessage) => void;
  updateLastMessage: (id: string, appendContent: string, type?: MessageUpdateKind) => void;
  finalizeMessage: (id: string) => void;
  setEnhancedPrompt: (id: string, prompt: string) => void;
  setMessageError: (id: string, error: string) => void;
  clearPendingApproval: (id: string, approved?: boolean) => void;
  setModel: (model: string) => void;
  setConversationTitle: (id: string, title: string) => void;
  clearConversationTitle: (id: string) => void;
  clearConversation: () => void;
  sendMessage: (args: SendMessageArgs) => Promise<void>;
  retryLastMessage: () => void;
  respondToApproval: (args: RespondToApprovalArgs) => Promise<void>;
  stopStreaming: () => void;
}

/**
 * Creates an independent conversation store: its own active conversation, messages and turn. A turn runs on the
 * server on its own; the store follows its stream, which survives dropped connections (it resumes where it left off)
 * and, for a store with a `resumeKey`, a reload. The assistant page uses {@link useConversationStore}; other surfaces
 * (e.g. the timeline's agent drawer) create their own so they never take over the assistant's chat.
 */
export const createConversationStore = (options: CreateConversationStoreOptions = {}) => {
  const storage = turnStorage(options.resumeKey);
  let active: ActiveTurn | null = null;
  let generation = 0;
  let parked: ParkedThread | null = null;

  return createStore<ConversationState>((set, get) => {
    /**
     * Applies `update` to message `id` wherever it lives: the shown conversation, or the parked one whose turn is
     * still streaming in the background.
     */
    const patchMessage = (id: string, update: (msg: ConversationMessage) => ConversationMessage | null) => {
      if (get().messages.some((m) => m.id === id)) {
        set((state) => {
          const idx = state.messages.findLastIndex((m) => m.id === id);
          const next = idx === -1 ? null : update(state.messages[idx]);
          if (!next) {
            return state;
          }
          const messages = state.messages.slice();
          messages[idx] = next;
          return { messages };
        });
        return;
      }

      const idx = parked?.messages.findLastIndex((m) => m.id === id) ?? -1;
      const next = parked && idx !== -1 ? update(parked.messages[idx]) : null;
      if (parked && next) {
        const messages = parked.messages.slice();
        messages[idx] = next;
        parked = { ...parked, messages };
      }
    };

    /** Keeps the shown conversation's messages when leaving it while its turn still streams. */
    const parkIfLeavingStream = (nextId: string | null) => {
      const { activeConversationId, streamingConversationId, isStreaming, messages } = get();
      if (isStreaming && activeConversationId && activeConversationId === streamingConversationId && nextId !== activeConversationId) {
        parked = { conversationId: activeConversationId, messages };
      }
    };

    /** The messages to show for `id`: the parked copy while (or since) its turn streamed away from view. */
    const messagesFor = (id: string | null, loaded: ConversationMessage[]): ConversationMessage[] => {
      if (!id || parked?.conversationId !== id) {
        return loaded;
      }
      const thread = parked;
      parked = null;
      // Once the turn has ended its answer is saved, so the loaded transcript is the truth - unless it was read
      // before the answer was saved.
      const stillStreaming = get().isStreaming && get().streamingConversationId === id;
      return stillStreaming || loaded.length < thread.messages.length ? thread.messages : loaded;
    };

    const applyFrame = (frame: TurnFrame, aiMsgId: string, conversationId: string | null, userMsgId?: string) => {
      const { updateLastMessage, setEnhancedPrompt, setConversationTitle } = get();
      const text = frame.text ?? "";

      switch (frame.type) {
        case "title":
          if (text && conversationId) {
            setConversationTitle(conversationId, text);
          }
          return;
        case "enhanced_prompt":
          if (text && userMsgId) {
            setEnhancedPrompt(userMsgId, text);
          }
          return;
        case "approval_request":
          updateLastMessage(aiMsgId, text, "approval");
          return;
        default:
          if (text) {
            const kind = ACTIVITY_TYPES.has(frame.type ?? "") ? (frame.type as MessageUpdateKind) : "content";
            updateLastMessage(aiMsgId, text, kind);
          }
      }
    };

    /** The turn ended while nobody followed it: its answer is in the conversation, so show that instead. */
    const reloadConversation = (conversationId: string) => {
      void fetchConversation(conversationId)
        .then((data) => {
          if (get().activeConversationId === conversationId && !get().isStreaming) {
            set({ messages: (data.messages ?? []).map(messageFromDto) });
          }
        })
        .catch(() => {});
    };

    /** Follows a turn's stream until its terminal frame; resolves then, or when the turn is given up on. */
    const followTurn = (turnId: string, aiMsgId: string, conversationId: string | null, userMsgId?: string) =>
      new Promise<void>((resolve) => {
        let settled = false;
        let stopTimer: ReturnType<typeof setTimeout> | null = null;
        const batch = createFrameBatcher<TurnFrame>((frames) => {
          for (const frame of mergeTextFrames(frames, MERGEABLE_TYPES)) {
            applyFrame(frame, aiMsgId, conversationId, userMsgId);
          }
        });

        const settle = (error?: string) => {
          if (settled) {
            return;
          }
          settled = true;
          if (stopTimer) {
            clearTimeout(stopTimer);
          }
          stream.close();
          batch.flush();
          if (active?.turnId === turnId) {
            active = null;
          }
          storage.clear();
          get().finalizeMessage(aiMsgId);
          if (error) {
            get().setMessageError(aiMsgId, error);
          }
          resolve();
        };

        const stream = openEventStream(chatTurnStreamUrl(turnId), {
          resume: true,
          maxAttempts: MAX_STREAM_ATTEMPTS,
          onGiveUp: () => settle(LOST_CONNECTION_MESSAGE),
          onFrame: (data) => {
            let frame: TurnFrame;
            try {
              frame = JSON.parse(data);
            } catch {
              return;
            }

            switch (frame.type) {
              case "done":
              case "cancelled":
                settle();
                return;
              case "gone":
                settle();
                if (conversationId) {
                  reloadConversation(conversationId);
                }
                return;
              case "error":
                settle(frame.message || "Connection to AI node failed.");
                return;
              default:
                batch.push(frame);
            }
          },
        });

        active = {
          turnId,
          stop: () => {
            if (!settled && !stopTimer) {
              stopTimer = setTimeout(() => settle(), STOP_FALLBACK_MS);
            }
          },
          abandon: () => settle(),
        };
      });

    /** Stops and lets go of the turn this store follows, without waiting for it to end. */
    const abandonActiveTurn = () => {
      const previous = active;
      if (!previous) {
        return;
      }
      void cancelChatTurn(previous.turnId).catch(() => {});
      previous.abandon();
    };

    const runTurn = async (
      start: () => Promise<{ turnId: string }>,
      aiMsgId: string,
      conversationId: string | null,
      fallbackError: string,
      stored: Omit<StoredTurn, "turnId" | "conversationId"> = {},
      userMsgId?: string,
    ) => {
      abandonActiveTurn();
      const mine = ++generation;
      set({ isStreaming: true, streamingConversationId: conversationId });

      try {
        const { turnId } = await start();
        if (mine !== generation) {
          // Superseded while the turn was being accepted.
          void cancelChatTurn(turnId).catch(() => {});
          return;
        }
        if (conversationId) {
          storage.write({ turnId, conversationId, ...stored });
        }
        await followTurn(turnId, aiMsgId, conversationId, userMsgId);
      } catch (err) {
        get().setMessageError(aiMsgId, streamErrorMessage(err, fallbackError));
      } finally {
        if (mine === generation) {
          set({ isStreaming: false, streamingConversationId: null });
        }
      }
    };

    /** Re-attaches to the turn a reload interrupted, once its conversation is shown again. */
    const resumeStoredTurn = (conversationId: string) => {
      const stored = storage.read();
      if (!stored || stored.conversationId !== conversationId || get().isStreaming) {
        return;
      }

      const now = new Date();
      if (stored.userContent !== undefined) {
        get().appendMessage({
          id: crypto.randomUUID(),
          role: "user",
          content: stored.userContent,
          timestamp: now,
          ...(stored.attachments?.length ? { attachments: stored.attachments } : {}),
        });
      }
      const aiMsgId = crypto.randomUUID();
      get().appendMessage({ id: aiMsgId, role: "assistant", content: "", timestamp: now });

      const mine = ++generation;
      set({ isStreaming: true, streamingConversationId: conversationId });
      void followTurn(stored.turnId, aiMsgId, conversationId).finally(() => {
        if (mine === generation) {
          set({ isStreaming: false, streamingConversationId: null });
        }
      });
    };

    return {
      activeConversationId: null,
      messages: [],
      model: DEFAULT_ASSISTANT_MODEL,
      isStreaming: false,
      streamingConversationId: null,
      conversationTitles: {},

      setActiveConversation: (id, messages, model) => {
        parkIfLeavingStream(id);
        set({
          activeConversationId: id,
          messages: messagesFor(id, messages),
          model,
        });
        if (id) {
          resumeStoredTurn(id);
        }
      },

      appendMessage: (message) =>
        set((state) => ({
          messages: [...state.messages, message],
        })),

      updateLastMessage: (id, appendContent, type = "content") =>
        patchMessage(id, (msg) => withUpdate(msg, appendContent, type, get().streamingConversationId)),

      finalizeMessage: (id) =>
        patchMessage(id, (msg) => {
          if (!msg.steps?.length) {
            return null;
          }
          const settledTurn = finalizeTurn({ content: msg.content, steps: msg.steps });
          return { ...msg, content: settledTurn.content, steps: settledTurn.steps };
        }),

      setEnhancedPrompt: (id, prompt) => patchMessage(id, (msg) => ({ ...msg, enhancedPrompt: prompt })),

      setMessageError: (id, error) => patchMessage(id, (msg) => ({ ...msg, error })),

      clearPendingApproval: (id, approved) =>
        patchMessage(id, (msg) => {
          const decided =
            approved !== undefined && msg.pendingApproval && msg.steps
              ? decideApproval({ content: msg.content, steps: msg.steps }, msg.pendingApproval, approved).steps
              : msg.steps;
          return { ...msg, steps: decided, pendingApproval: undefined };
        }),

      setModel: (model) => set({ model }),

      setConversationTitle: (id, title) =>
        set((state) => ({ conversationTitles: { ...state.conversationTitles, [id]: title } })),

      clearConversationTitle: (id) =>
        set((state) => {
          if (!(id in state.conversationTitles)) {
            return state;
          }
          const next = { ...state.conversationTitles };
          delete next[id];
          return { conversationTitles: next };
        }),

      clearConversation: () => {
        parkIfLeavingStream(null);
        set({
          activeConversationId: null,
          messages: [],
          model: DEFAULT_ASSISTANT_MODEL,
        });
      },

      sendMessage: async ({ conversationId, model, userContent, attachments }) => {
        const { appendMessage } = get();

        const userMsg: ConversationMessage = {
          id: crypto.randomUUID(),
          role: "user",
          content: userContent,
          timestamp: new Date(),
          ...(attachments?.length ? { attachments } : {}),
        };

        const chatHistory = [
          ...get().messages.map((msg) => ({ role: msg.role, content: msg.content })),
          { role: userMsg.role, content: userMsg.content },
        ];

        appendMessage(userMsg);

        const aiMsgId = crypto.randomUUID();
        appendMessage({
          id: aiMsgId,
          role: "assistant",
          content: "",
          timestamp: new Date(),
        });

        await runTurn(
          () =>
            startChatTurn({
              conversationId: conversationId ?? undefined,
              model,
              messages: chatHistory,
              stream: true,
              ...(attachments?.length ? { fileIds: attachments.map((a) => a.fileId) } : {}),
            }),
          aiMsgId,
          conversationId,
          "Connection to AI node failed.",
          { userContent, ...(attachments?.length ? { attachments } : {}) },
          userMsg.id,
        );
      },

      retryLastMessage: () => {
        const { messages, activeConversationId, model } = get();
        const lastUserIdx = [...messages]
          .map((m, i) => ({ m, i }))
          .reverse()
          .find(({ m }) => m.role === "user")?.i;
        if (lastUserIdx === undefined) return;
        const lastUser = messages[lastUserIdx];
        set({ messages: messages.slice(0, lastUserIdx) });
        void get().sendMessage({
          conversationId: activeConversationId,
          model,
          userContent: lastUser.content,
          attachments: lastUser.attachments,
        });
      },

      respondToApproval: async ({ aiMsgId, requestId, approved, conversationId }) => {
        get().clearPendingApproval(aiMsgId, approved);

        await runTurn(
          () => approveToolCall(requestId, approved),
          aiMsgId,
          conversationId,
          "Failed to process approval. Please try again.",
        );
      },

      stopStreaming: () => {
        if (active) {
          void cancelChatTurn(active.turnId).catch(() => {});
          active.stop();
        }
      },
    };
  });
};

export type ConversationStore = ReturnType<typeof createConversationStore>;

export const useConversationStore = createConversationStore({ resumeKey: "sentium.assistant.activeTurn" });
