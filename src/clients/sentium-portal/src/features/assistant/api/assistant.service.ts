import { BASE_URL, client } from "@/shared/api/client";
import type {
  ChatPayload,
  ConversationDetail,
  ConversationSummary,
  CreateConversationPayload,
  CreateConversationResult,
  EarlierMessagesPage,
} from "../types/assistant";
import type { PagedResponse } from "@/shared/types/pagination";

const BASE = "/praxis";

const searchParam = (search?: string) => (search?.trim() ? `&search=${encodeURIComponent(search.trim())}` : "");

/** Pinned first, then by latest activity; `archived` lists the archived conversations instead. */
export const fetchConversations = (
  page = 1,
  pageSize = 20,
  search?: string,
  archived = false,
): Promise<PagedResponse<ConversationSummary>> =>
  client.get<PagedResponse<ConversationSummary>>(
    `${BASE}/conversations?page=${page}&pageSize=${pageSize}${searchParam(search)}${archived ? "&archived=true" : ""}`,
  );

/** The conversation with its newest messages; see {@link fetchEarlierMessages} for the rest. */
export const fetchConversation = (id: string) => client.get<ConversationDetail>(`${BASE}/conversations/${id}`);

export const fetchEarlierMessages = (id: string, beforeMessageId: string, limit = 100) =>
  client.get<EarlierMessagesPage>(
    `${BASE}/conversations/${id}/messages?before=${encodeURIComponent(beforeMessageId)}&limit=${limit}`,
  );

export const setConversationPinned = (id: string, pinned: boolean) =>
  client.put<void>(`${BASE}/conversations/${id}/pinned`, { pinned });

/** Archiving also unpins; a new message brings the conversation back. */
export const setConversationArchived = (id: string, archived: boolean) =>
  client.put<void>(`${BASE}/conversations/${id}/archived`, { archived });

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
