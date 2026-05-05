import { create } from "zustand";
import type { ConversationMessage } from "../types/assistant";
import { DEFAULT_ASSISTANT_MODEL } from "../utils/constants";

interface ConversationState {
  activeConversationId: string | null;
  messages: ConversationMessage[];
  model: string;

  setActiveConversation: (id: string | null, messages: ConversationMessage[], model: string) => void;
  appendMessage: (message: ConversationMessage) => void;
  updateLastMessage: (id: string, appendContent: string, type?: "content" | "thought" | "tool") => void;
  setModel: (model: string) => void;
  clearConversation: () => void;
}

export const useConversationStore = create<ConversationState>((set) => ({
  activeConversationId: null,
  messages: [],
  model: DEFAULT_ASSISTANT_MODEL,

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

        return { ...msg, content: msg.content + appendContent };
      }),
    })),

  setModel: (model) => set({ model }),

  clearConversation: () =>
    set({
      activeConversationId: null,
      messages: [],
      model: DEFAULT_ASSISTANT_MODEL,
    }),
}));
