import { createConversationStore } from "@/features/assistant/state/assistant-conversation-store";

/**
 * The Companion's quick chat: its own store, so talking to the assistant from any page never aborts or replaces
 * the conversation open on the assistant page. Module scope keeps it alive across navigation.
 */
export const useQuickChatStore = createConversationStore();
