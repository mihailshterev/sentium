import { createContext } from "react";

export interface ConversationMessage {
  id: string;
  role: "user" | "assistant";
  content: string;
  timestamp: Date;
}

interface ConversationState {
  activeConversationId: string | null;
  messages: ConversationMessage[];
  model: string;
}

export interface ConversationContextValue extends ConversationState {
  setActiveConversation: (id: string | null, messages: ConversationMessage[], model: string) => void;
  appendMessage: (message: ConversationMessage) => void;
  updateLastMessage: (id: string, appendContent: string) => void;
  setModel: (model: string) => void;
  clearConversation: () => void;
}

export const ConversationContext = createContext<ConversationContextValue | null>(null);

export const INITIAL_MESSAGE: ConversationMessage = {
  id: "init",
  role: "assistant",
  content: "Local Ollama node initialized. Awaiting secure input.",
  timestamp: new Date(),
};
