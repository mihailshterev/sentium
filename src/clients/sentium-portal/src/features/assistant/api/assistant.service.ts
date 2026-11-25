import { BASE_URL, client } from "@/shared/api/client";
import { pathIn, withQuery } from "@/shared/api/url";
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
const url = pathIn(BASE);

/** Pinned first, then by latest activity; `archived` lists the archived conversations instead. */
export const fetchConversations = (
  page = 1,
  pageSize = 20,
  search?: string,
  archived = false,
): Promise<PagedResponse<ConversationSummary>> =>
  client.get<PagedResponse<ConversationSummary>>(
    withQuery(`${BASE}/conversations`, { page, pageSize, search: search?.trim(), archived: archived || undefined }),
  );

/** The conversation with its newest messages; see {@link fetchEarlierMessages} for the rest. */
export const fetchConversation = (id: string) => client.get<ConversationDetail>(url`/conversations/${id}`);

export const fetchEarlierMessages = (id: string, beforeMessageId: string, limit = 100) =>
  client.get<EarlierMessagesPage>(withQuery(url`/conversations/${id}/messages`, { before: beforeMessageId, limit }));

export const setConversationPinned = (id: string, pinned: boolean) =>
  client.put<void>(url`/conversations/${id}/pinned`, { pinned });

/** Archiving also unpins; a new message brings the conversation back. */
export const setConversationArchived = (id: string, archived: boolean) =>
  client.put<void>(url`/conversations/${id}/archived`, { archived });

export const createConversation = (payload: CreateConversationPayload) =>
  client.post<CreateConversationResult>(`${BASE}/conversations`, payload);

export const deleteConversation = (id: string) => client.delete<void>(url`/conversations/${id}`);

export const renameConversation = (id: string, title: string) =>
  client.put<void>(url`/conversations/${id}/title`, { title });

/** Binds the conversation to a workspace, or unbinds it with null. */
export const setConversationWorkspace = (id: string, workspaceId: string | null) =>
  client.put<void>(url`/conversations/${id}/workspace`, { workspaceId });

/** A queued assistant turn; follow it on {@link chatTurnStreamUrl}. */
export interface ChatTurnAccepted {
  turnId: string;
}

export const startChatTurn = (payload: ChatPayload) => client.post<ChatTurnAccepted>(`${BASE}/assistant/chat`, payload);

export const approveToolCall = (requestId: string, approved: boolean) =>
  client.post<ChatTurnAccepted>(`${BASE}/assistant/chat/approve`, { requestId, approved });

/** Stops a turn; it keeps its partial answer and its stream ends with a "cancelled" frame. */
export const cancelChatTurn = (turnId: string) => client.post<void>(url`/assistant/turns/${turnId}/cancel`, {});

export const chatTurnStreamUrl = (turnId: string) => `${BASE_URL}${url`/assistant/turns/${turnId}/stream`}`;
