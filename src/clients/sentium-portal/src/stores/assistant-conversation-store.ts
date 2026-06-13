import { create } from "zustand";
import type { ConversationMessage } from "../types/assistant";
import { DEFAULT_ASSISTANT_MODEL } from "../utils/constants";
import { sendChatMessage, approveToolCall } from "../services/agentRuntime.service";
import { handleUnauthorized } from "../api/client";

interface SendMessageArgs {
  conversationId: string | null;
  model: string;
  userContent: string;
}

interface RespondToApprovalArgs {
  aiMsgId: string;
  requestId: string;
  approved: boolean;
  conversationId: string | null;
}

class StreamStallError extends Error {
  constructor() {
    super("Response timed out.");
    this.name = "StreamStallError";
  }
}

const isAbortError = (err: unknown): boolean =>
  typeof err === "object" && err !== null && (err as { name?: string }).name === "AbortError";

const streamErrorMessage = (err: unknown, fallback: string): string => {
  if (err instanceof StreamStallError) {
    return "Response timed out. Please try again.";
  }
  if (err instanceof Error && err.message.startsWith("Session expired")) {
    return err.message;
  }
  return fallback;
};

interface ConversationState {
  activeConversationId: string | null;
  messages: ConversationMessage[];
  model: string;
  isStreaming: boolean;
  streamingConversationId: string | null;

  setActiveConversation: (id: string | null, messages: ConversationMessage[], model: string) => void;
  appendMessage: (message: ConversationMessage) => void;
  updateLastMessage: (id: string, appendContent: string, type?: "content" | "thought" | "tool" | "approval") => void;
  setEnhancedPrompt: (id: string, prompt: string) => void;
  setMessageError: (id: string, error: string) => void;
  clearPendingApproval: (id: string) => void;
  setModel: (model: string) => void;
  clearConversation: () => void;
  sendMessage: (args: SendMessageArgs) => Promise<void>;
  retryLastMessage: () => void;
  respondToApproval: (args: RespondToApprovalArgs) => Promise<void>;
  stopStreaming: () => void;
}

const controllers = new Map<string, AbortController>();

const STREAM_KEY = "active";

export const useConversationStore = create<ConversationState>((set, get) => {
  const STALL_TIMEOUT_MS = 120_000;

  const consumeStream = async (
    response: Response,
    aiMsgId: string,
    controller: AbortController,
    userMsgId?: string,
  ): Promise<boolean> => {
    if (response.status === 401) {
      handleUnauthorized();
    }
    if (!response.ok) {
      throw new Error(`Assistant request failed (${response.status})`);
    }

    const reader = response.body?.getReader();
    const decoder = new TextDecoder("utf-8");
    if (!reader) {
      throw new Error("Failed to read stream");
    }

    const { updateLastMessage, setEnhancedPrompt } = get();
    let leftover = "";

    let stalled = false;
    const abortOnStall = () => {
      stalled = true;
      controller.abort();
    };
    let stallTimer = setTimeout(abortOnStall, STALL_TIMEOUT_MS);
    const resetStallTimer = () => {
      clearTimeout(stallTimer);
      stallTimer = setTimeout(abortOnStall, STALL_TIMEOUT_MS);
    };

    try {
      while (true) {
        const { done, value } = await reader.read();
        if (done) {
          break;
        }

        resetStallTimer();

        const chunk = leftover + decoder.decode(value, { stream: true });
        const lines = chunk.split("\n");
        leftover = lines.pop() || "";

        for (const line of lines) {
          let trimmed = line.trim();
          if (!trimmed || trimmed.startsWith(":")) {
            continue;
          }
          if (trimmed.startsWith("data: ")) {
            trimmed = trimmed.slice(6).trim();
          }
          if (!trimmed) {
            continue;
          }

          let parsed: { type?: string; message?: string | { content?: string } };
          try {
            parsed = JSON.parse(trimmed);
          } catch {
            continue;
          }

          if (parsed.type === "done") {
            return false;
          }
          if (parsed.type === "error") {
            const errorText = typeof parsed.message === "string" ? parsed.message : parsed.message?.content;
            throw new Error(errorText || "Connection to AI node failed.");
          }

          const content = typeof parsed.message === "string" ? undefined : parsed.message?.content;

          if (parsed.type === "enhanced_prompt") {
            if (content && userMsgId) {
              setEnhancedPrompt(userMsgId, content);
            }
            continue;
          }
          if (parsed.type === "approval_request") {
            updateLastMessage(aiMsgId, content ?? "", "approval");
            return true;
          }
          if (content) {
            if (parsed.type === "thought") {
              updateLastMessage(aiMsgId, content, "thought");
            } else if (parsed.type === "tool") {
              updateLastMessage(aiMsgId, content, "tool");
            } else {
              updateLastMessage(aiMsgId, content, "content");
            }
          }
        }
      }
    } catch (err) {
      if (stalled) {
        throw new StreamStallError();
      }
      throw err;
    } finally {
      clearTimeout(stallTimer);
    }

    return false;
  };

  return {
    activeConversationId: null,
    messages: [],
    model: DEFAULT_ASSISTANT_MODEL,
    isStreaming: false,
    streamingConversationId: null,

    setActiveConversation: (id, messages, model) =>
      set({
        activeConversationId: id,
        messages: messages.length > 0 ? messages : [],
        model,
      }),

    appendMessage: (message) =>
      set((state) => ({
        messages: [...state.messages, message],
      })),

    updateLastMessage: (id, appendContent, type = "content") =>
      set((state) => {
        const idx = state.messages.findLastIndex((m) => m.id === id);
        if (idx === -1) {
          return state;
        }

        const msg = state.messages[idx];
        let updated: ConversationMessage;

        if (type === "thought") {
          updated = { ...msg, thought: (msg.thought || "") + appendContent };
        } else if (type === "tool") {
          updated = { ...msg, toolCalls: [...(msg.toolCalls || []), appendContent] };
        } else if (type === "approval") {
          try {
            updated = {
              ...msg,
              pendingApproval: {
                ...JSON.parse(appendContent),
                conversationId: state.streamingConversationId ?? undefined,
              },
            };
          } catch {
            return state;
          }
        } else {
          updated = { ...msg, content: msg.content + appendContent };
        }

        const next = state.messages.slice();
        next[idx] = updated;
        return { messages: next };
      }),

    setEnhancedPrompt: (id, prompt) =>
      set((state) => ({
        messages: state.messages.map((msg) => (msg.id === id ? { ...msg, enhancedPrompt: prompt } : msg)),
      })),

    setMessageError: (id, error) =>
      set((state) => ({
        messages: state.messages.map((msg) => (msg.id === id ? { ...msg, error } : msg)),
      })),

    clearPendingApproval: (id) =>
      set((state) => ({
        messages: state.messages.map((msg) => (msg.id === id ? { ...msg, pendingApproval: undefined } : msg)),
      })),

    setModel: (model) => set({ model }),

    clearConversation: () =>
      set({
        activeConversationId: null,
        messages: [],
        model: DEFAULT_ASSISTANT_MODEL,
      }),

    sendMessage: async ({ conversationId, model, userContent }) => {
      const { appendMessage } = get();

      const userMsg: ConversationMessage = {
        id: crypto.randomUUID(),
        role: "user",
        content: userContent,
        timestamp: new Date(),
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

      controllers.get(STREAM_KEY)?.abort();
      const controller = new AbortController();
      controllers.set(STREAM_KEY, controller);
      set({ isStreaming: true, streamingConversationId: conversationId });

      try {
        const response = await sendChatMessage(
          {
            conversationId: conversationId ?? undefined,
            model,
            messages: chatHistory,
            stream: true,
          },
          controller.signal,
        );

        await consumeStream(response, aiMsgId, controller, userMsg.id);
      } catch (err) {
        if (!isAbortError(err)) {
          get().setMessageError(aiMsgId, streamErrorMessage(err, "Connection to AI node failed."));
        }
      } finally {
        if (controllers.get(STREAM_KEY) === controller) {
          controllers.delete(STREAM_KEY);
          set({ isStreaming: false, streamingConversationId: null });
        }
      }
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
      void get().sendMessage({ conversationId: activeConversationId, model, userContent: lastUser.content });
    },

    respondToApproval: async ({ aiMsgId, requestId, approved, conversationId }) => {
      const { clearPendingApproval } = get();
      clearPendingApproval(aiMsgId);

      controllers.get(STREAM_KEY)?.abort();
      const controller = new AbortController();
      controllers.set(STREAM_KEY, controller);
      set({ isStreaming: true, streamingConversationId: conversationId });

      try {
        const response = await approveToolCall(requestId, approved, controller.signal);
        await consumeStream(response, aiMsgId, controller);
      } catch (err) {
        if (!isAbortError(err)) {
          get().setMessageError(aiMsgId, streamErrorMessage(err, "Failed to process approval. Please try again."));
        }
      } finally {
        if (controllers.get(STREAM_KEY) === controller) {
          controllers.delete(STREAM_KEY);
          set({ isStreaming: false, streamingConversationId: null });
        }
      }
    },

    stopStreaming: () => {
      controllers.get(STREAM_KEY)?.abort();
    },
  };
});
