import { BASE_URL, client } from "@/shared/api/client";
import type {
  ChatPayload,
  ConversationDetail,
  ConversationSummary,
  CreateConversationPayload,
  CreateConversationResult,
} from "../types/assistant";
import type { PagedResponse } from "@/shared/types/pagination";

const BASE = "/agent-runtime";

export const fetchConversations = (page = 1, pageSize = 20): Promise<PagedResponse<ConversationSummary>> =>
  client.get<PagedResponse<ConversationSummary>>(`${BASE}/conversations?page=${page}&pageSize=${pageSize}`);

export const fetchConversation = (id: string) => client.get<ConversationDetail>(`${BASE}/conversations/${id}`);

export const createConversation = (payload: CreateConversationPayload) =>
  client.post<CreateConversationResult>(`${BASE}/conversations`, payload);

export const deleteConversation = (id: string) => client.delete<void>(`${BASE}/conversations/${id}`);

export const sendChatMessage = (payload: ChatPayload, signal?: AbortSignal): Promise<Response> =>
  fetch(`${BASE_URL}${BASE}/assistant/chat`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
    credentials: "include",
    signal,
  });

export const approveToolCall = (requestId: string, approved: boolean, signal?: AbortSignal): Promise<Response> =>
  fetch(`${BASE_URL}${BASE}/assistant/chat/approve`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ requestId, approved }),
    credentials: "include",
    signal,
  });
