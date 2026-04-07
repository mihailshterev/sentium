import { useCallback, useState, type ReactNode } from "react";
import { ConversationContext, INITIAL_MESSAGE, type ConversationMessage } from "./conversation-context";

const ConversationProvider = ({ children }: { children: ReactNode }) => {
  const [activeConversationId, setActiveConversationId] = useState<string | null>(null);
  const [messages, setMessages] = useState<ConversationMessage[]>([INITIAL_MESSAGE]);
  const [model, setModelState] = useState<string>("gemma4:e4b");

  const setActiveConversation = useCallback((id: string | null, msgs: ConversationMessage[], m: string) => {
    setActiveConversationId(id);
    setMessages(msgs.length > 0 ? msgs : [INITIAL_MESSAGE]);
    setModelState(m);
  }, []);

  const appendMessage = useCallback((message: ConversationMessage) => {
    setMessages((prev) => [...prev, message]);
  }, []);

  const updateLastMessage = useCallback((id: string, appendContent: string) => {
    setMessages((prev) => prev.map((msg) => (msg.id === id ? { ...msg, content: msg.content + appendContent } : msg)));
  }, []);

  const setModel = useCallback((m: string) => setModelState(m), []);

  const clearConversation = useCallback(() => {
    setActiveConversationId(null);
    setMessages([INITIAL_MESSAGE]);
  }, []);

  return (
    <ConversationContext.Provider
      value={{
        activeConversationId,
        messages,
        model,
        setActiveConversation,
        appendMessage,
        updateLastMessage,
        setModel,
        clearConversation,
      }}
    >
      {children}
    </ConversationContext.Provider>
  );
};

export default ConversationProvider;
