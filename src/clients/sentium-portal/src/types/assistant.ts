export interface ConversationSummary {
  id: string;
  title: string;
  model: string;
  createdAt: string;
}

export interface ConversationMessage {
  id: string;
  role: "user" | "assistant";
  content: string;
  thought?: string;
  toolCalls?: string[];
  timestamp: Date;
}
