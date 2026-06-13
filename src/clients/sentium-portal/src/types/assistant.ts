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
  conversationId?: string;
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
  error?: string;
}

export interface ConversationMessageDto {
  id: string;
  role: "user" | "assistant";
  content: string;
  timestamp: string;
  enhancedPrompt?: string;
  thought?: string;
  toolCalls?: string[];
}

export interface ConversationDetail {
  id: string;
  title: string;
  model: string;
  messages: ConversationMessageDto[];
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
