import { useContext } from "react";
import {
  ConversationContext,
  type ConversationContextValue,
} from "../providers/conversation-context";

const useConversation = (): ConversationContextValue => {
  const context = useContext(ConversationContext);
  if (!context) {
    throw new Error("useConversation must be used within ConversationProvider");
  }
  return context;
};

export default useConversation;
