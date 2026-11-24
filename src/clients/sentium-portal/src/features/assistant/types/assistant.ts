import type { ActivityStep, MessageStepDto } from "@/shared/agent-activity";

export interface ConversationSummary {
  id: string;
  title: string;
  model: string;
  createdAt: string;
  agentName?: string | null;
  workspaceId?: string | null;
  workspaceName?: string | null;
  /** When the last message was added (the creation time for older conversations). */
  lastActivityAt?: string | null;
  isPinned?: boolean;
  isArchived?: boolean;
}

/** A workspace file attached to a user message. */
export interface MessageAttachment {
  fileId: string;
  fileName: string;
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
  attachments?: MessageAttachment[];
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
  attachments?: MessageAttachment[] | null;
}

export interface ConversationDetail {
  id: string;
  title: string;
  model: string;
  /** The newest messages, oldest first; older ones come from {@link EarlierMessagesPage}. */
  messages: ConversationMessageDto[];
  agentName?: string | null;
  workspaceId?: string | null;
  workspaceName?: string | null;
  hasEarlierMessages?: boolean;
  isPinned?: boolean;
  isArchived?: boolean;
}

export interface EarlierMessagesPage {
  /** Oldest first. */
  messages: ConversationMessageDto[];
  hasEarlierMessages: boolean;
}

export interface CreateConversationPayload {
  title?: string;
  model: string;
  agentName?: string;
  workspaceId?: string | null;
}

export interface CreateConversationResult {
  id: string;
  workspaceId?: string | null;
  workspaceName?: string | null;
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
  /** Workspace files attached to the latest user message. */
  fileIds?: string[];
}
