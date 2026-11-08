import type { ActivityStep, MessageStepDto } from "@/shared/agent-activity";

export interface ConversationSummary {
  id: string;
  title: string;
  model: string;
  createdAt: string;
  agentName?: string | null;
}

export interface PendingApproval {
  requestId: string;
  toolName: string;
  arguments: Record<string, unknown>;
  callId?: string | null;
  conversationId?: string;
}

export interface ConversationMessage {
  id: string;
  role: "user" | "assistant";
  content: string;
  steps?: ActivityStep[];
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
  enhancedPrompt?: string | null;
  steps?: MessageStepDto[] | null;
  thought?: string | null;
  toolCalls?: string[] | null;
}

export interface ConversationDetail {
  id: string;
  title: string;
  model: string;
  messages: ConversationMessageDto[];
  agentName?: string | null;
}

export interface CreateConversationPayload {
  title?: string;
  model: string;
  agentName?: string;
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
