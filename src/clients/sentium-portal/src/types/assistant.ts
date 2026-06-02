export interface ConversationSummary {
  id: string;
  title: string;
  model: string;
  createdAt: string;
}

export interface PendingApproval {
  requestId: string;
  toolName: string;
  arguments: Record<string, unknown>;
}

export interface ConversationMessage {
  id: string;
  role: "user" | "assistant";
  content: string;
  thought?: string;
  toolCalls?: string[];
  timestamp: Date;
  pendingApproval?: PendingApproval;
  enhancedPrompt?: string;
}

export interface ConversationDetail {
  id: string;
  title: string;
  model: string;
  messages: { id: string; role: "user" | "assistant"; content: string; timestamp: string }[];
}

export interface CreateConversationPayload {
  title: string;
  model: string;
}

export interface CreateConversationResult {
  id: string;
}

export interface ChatMessage {
  role: string;
  content: string;
}

export interface ChatPayload {
  conversationId?: string;
  model: string;
  messages: ChatMessage[];
  stream: boolean;
}
