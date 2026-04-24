import { create } from "zustand";
import type { ConversationMessage } from "../types/assistant";
import { DEFAULT_ASSISTANT_MODEL } from "../utils/constants";

export const INITIAL_MESSAGE: ConversationMessage = {
  id: "init",
  role: "assistant",
  content: "Local Ollama node initialized. Awaiting secure input.",
  timestamp: new Date(),
};

interface ConversationState {
  activeConversationId: string | null;
  messages: ConversationMessage[];
  model: string;

  setActiveConversation: (id: string | null, messages: ConversationMessage[], model: string) => void;
  appendMessage: (message: ConversationMessage) => void;
  updateLastMessage: (id: string, appendContent: string) => void;
  setModel: (model: string) => void;
  clearConversation: () => void;
}

export const useConversationStore = create<ConversationState>((set) => ({
  activeConversationId: null,
  messages: [INITIAL_MESSAGE],
  model: DEFAULT_ASSISTANT_MODEL,

  setActiveConversation: (id, messages, model) =>
    set({
      activeConversationId: id,
      messages: messages.length > 0 ? messages : [INITIAL_MESSAGE],
      model,
    }),

  appendMessage: (message) =>
    set((state) => ({
      messages: [...state.messages, message],
    })),

  updateLastMessage: (id, appendContent) =>
    set((state) => ({
      messages: state.messages.map((msg) => (msg.id === id ? { ...msg, content: msg.content + appendContent } : msg)),
    })),

  setModel: (model) => set({ model }),

  clearConversation: () =>
    set({
      activeConversationId: null,
      messages: [INITIAL_MESSAGE],
      model: DEFAULT_ASSISTANT_MODEL,
    }),
}));
