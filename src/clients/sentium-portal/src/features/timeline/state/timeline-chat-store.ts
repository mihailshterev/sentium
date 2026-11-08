import { createConversationStore } from "@/features/assistant/state/assistant-conversation-store";

/**
 * The timeline drawer's own chat: a separate store, so talking to an agent here never aborts or replaces the
 * conversation open on the assistant page. Module scope keeps it alive across navigation.
 */
export const useTimelineChatStore = createConversationStore();

const threads = new Map<string, string>();

/** The conversation already started for a task or month in this session, so reopening the drawer continues it. */
export const timelineThreads = {
  get: (contextKey: string) => threads.get(contextKey),
  set: (contextKey: string, conversationId: string) => void threads.set(contextKey, conversationId),
  clear: () => threads.clear(),
};
