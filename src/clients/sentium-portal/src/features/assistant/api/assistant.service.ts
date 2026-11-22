import { BASE_URL, client } from "@/shared/api/client";
import type {
  ChatPayload,
  ConversationDetail,
  ConversationSummary,
  CreateConversationPayload,
  CreateConversationResult,
} from "../types/assistant";
import type { PagedResponse } from "@/shared/types/pagination";

const BASE = "/praxis";

const searchParam = (search?: string) => (search?.trim() ? `&search=${encodeURIComponent(search.trim())}` : "");

export const fetchConversations = (
  page = 1,
  pageSize = 20,
  search?: string,
): Promise<PagedResponse<ConversationSummary>> =>
  client.get<PagedResponse<ConversationSummary>>(
    `${BASE}/conversations?page=${page}&pageSize=${pageSize}${searchParam(search)}`,
  );

export const fetchConversation = (id: string) => client.get<ConversationDetail>(`${BASE}/conversations/${id}`);

export const createConversation = (payload: CreateConversationPayload) =>
  client.post<CreateConversationResult>(`${BASE}/conversations`, payload);

export const deleteConversation = (id: string) => client.delete<void>(`${BASE}/conversations/${id}`);

export const renameConversation = (id: string, title: string) =>
  client.put<void>(`${BASE}/conversations/${id}/title`, { title });

/** Binds the conversation to a workspace, or unbinds it with null. */
export const setConversationWorkspace = (id: string, workspaceId: string | null) =>
  client.put<void>(`${BASE}/conversations/${id}/workspace`, { workspaceId });

/** A queued assistant turn; follow it on {@link chatTurnStreamUrl}. */
export interface ChatTurnAccepted {
  turnId: string;
}

export const startChatTurn = (payload: ChatPayload) => client.post<ChatTurnAccepted>(`${BASE}/assistant/chat`, payload);

export const approveToolCall = (requestId: string, approved: boolean) =>
  client.post<ChatTurnAccepted>(`${BASE}/assistant/chat/approve`, { requestId, approved });

/** Stops a turn; it keeps its partial answer and its stream ends with a "cancelled" frame. */
export const cancelChatTurn = (turnId: string) => client.post<void>(`${BASE}/assistant/turns/${turnId}/cancel`, {});

export const chatTurnStreamUrl = (turnId: string) => `${BASE_URL}${BASE}/assistant/turns/${turnId}/stream`;
