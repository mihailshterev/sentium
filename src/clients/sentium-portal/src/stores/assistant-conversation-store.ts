import { create } from "zustand";
import type { ConversationMessage } from "../types/assistant";
import { DEFAULT_ASSISTANT_MODEL } from "../utils/constants";
import { sendChatMessage, approveToolCall } from "../services/agentRuntime.service";

interface SendMessageArgs {
  conversationId: string | null;
  model: string;
  userContent: string;
}

interface RespondToApprovalArgs {
  aiMsgId: string;
  requestId: string;
  approved: boolean;
}

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
    if (!response.ok) {
      throw new Error("Network response was not ok");
    }

    const reader = response.body?.getReader();
    const decoder = new TextDecoder("utf-8");
    if (!reader) {
      throw new Error("Failed to read stream");
    }

    const { updateLastMessage, setEnhancedPrompt } = get();
    let leftover = "";

    let stallTimer = setTimeout(() => controller.abort(), STALL_TIMEOUT_MS);
    const resetStallTimer = () => {
      clearTimeout(stallTimer);
      stallTimer = setTimeout(() => controller.abort(), STALL_TIMEOUT_MS);
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
      set((state) => ({
        messages: state.messages.map((msg) => {
          if (msg.id !== id) {
            return msg;
          }

          if (type === "thought") {
            return { ...msg, thought: (msg.thought || "") + appendContent };
          }

          if (type === "tool") {
            return {
              ...msg,
              toolCalls: [...(msg.toolCalls || []), appendContent],
            };
          }

          if (type === "approval") {
            try {
              const pendingApproval = JSON.parse(appendContent);
              return { ...msg, pendingApproval };
            } catch {
              return msg;
            }
          }

          return { ...msg, content: msg.content + appendContent };
        }),
      })),

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
        id: Date.now().toString(),
        role: "user",
        content: userContent,
        timestamp: new Date(),
      };

      const chatHistory = [
        ...get().messages.map((msg) => ({ role: msg.role, content: msg.content })),
        { role: userMsg.role, content: userMsg.content },
      ];

      appendMessage(userMsg);

      const aiMsgId = (Date.now() + 1).toString();
      appendMessage({
        id: aiMsgId,
        role: "assistant",
        content: "",
        timestamp: new Date(),
      });

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
        if (!(err instanceof Error && err.name === "AbortError")) {
          get().setMessageError(aiMsgId, "Connection to AI node failed.");
        }
      } finally {
        controllers.delete(STREAM_KEY);
        set({ isStreaming: false, streamingConversationId: null });
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

    respondToApproval: async ({ aiMsgId, requestId, approved }) => {
      const { clearPendingApproval } = get();
      clearPendingApproval(aiMsgId);

      const controller = new AbortController();
      controllers.set(STREAM_KEY, controller);
      set({ isStreaming: true, streamingConversationId: get().activeConversationId });

      try {
        const response = await approveToolCall(requestId, approved, controller.signal);
        await consumeStream(response, aiMsgId, controller);
      } catch (err) {
        if (!(err instanceof Error && err.name === "AbortError")) {
          get().setMessageError(aiMsgId, "Failed to process approval. Please try again.");
        }
      } finally {
        controllers.delete(STREAM_KEY);
        set({ isStreaming: false, streamingConversationId: null });
      }
    },

    stopStreaming: () => {
      controllers.get(STREAM_KEY)?.abort();
    },
  };
});
