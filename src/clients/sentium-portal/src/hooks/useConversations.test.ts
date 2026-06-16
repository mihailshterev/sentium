import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import useConversations from "./useConversations";
import * as agentRuntimeService from "../services/agentRuntime.service";
import type { ConversationSummary } from "../types/assistant";

const createWrapper = () => {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: qc }, children);
};

const mockConversation: ConversationSummary = {
  id: "conv-1",
  title: "My First Chat",
  model: "llama3",
  createdAt: "2025-01-01T00:00:00Z",
};

beforeEach(() => {
  vi.spyOn(agentRuntimeService, "fetchConversations").mockResolvedValue({
    items: [mockConversation],
    totalCount: 1,
    page: 1,
    pageSize: 20,
    totalPages: 1,
  });
  vi.spyOn(agentRuntimeService, "createConversation").mockResolvedValue({ id: "conv-2" });
  vi.spyOn(agentRuntimeService, "deleteConversation").mockResolvedValue(undefined);
});

describe("useConversations fetching", () => {
  it("starts with an empty list", () => {
    const { result } = renderHook(() => useConversations(), { wrapper: createWrapper() });
    expect(result.current.conversations).toEqual([]);
  });

  it("returns conversations after fetch resolves", async () => {
    const { result } = renderHook(() => useConversations(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.conversations).toHaveLength(1));
    expect(result.current.conversations[0]).toEqual(mockConversation);
  });

  it("returns empty list on fetch failure", async () => {
    vi.spyOn(agentRuntimeService, "fetchConversations").mockRejectedValueOnce(new Error("fail"));
    const { result } = renderHook(() => useConversations(), { wrapper: createWrapper() });
    // Wait a tick for the query to settle
    await new Promise((r) => setTimeout(r, 50));
    expect(result.current.conversations).toEqual([]);
  });
});

describe("useConversations mutation states", () => {
  it("isCreating and isDeleting start as false", async () => {
    const { result } = renderHook(() => useConversations(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.conversations).toHaveLength(1));
    expect(result.current.isCreating).toBe(false);
    expect(result.current.isDeleting).toBe(false);
  });

  it("createConversation is a function", async () => {
    const { result } = renderHook(() => useConversations(), { wrapper: createWrapper() });
    expect(typeof result.current.createConversation).toBe("function");
  });

  it("deleteConversation is a function", async () => {
    const { result } = renderHook(() => useConversations(), { wrapper: createWrapper() });
    expect(typeof result.current.deleteConversation).toBe("function");
  });
});

describe("useConversations mutation calls", () => {
  it("calls createConversation service and returns the new conversation", async () => {
    const { result } = renderHook(() => useConversations(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.conversations).toHaveLength(1));
    let created: unknown;
    await act(async () => {
      created = await result.current.createConversation({ title: "New Chat", model: "llama3" });
    });
    expect(agentRuntimeService.createConversation).toHaveBeenCalled();
    expect(created).toEqual({ id: "conv-2" });
  });

  it("calls deleteConversation service", async () => {
    const { result } = renderHook(() => useConversations(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.conversations).toHaveLength(1));
    act(() => {
      result.current.deleteConversation("conv-1");
    });
    await waitFor(() => expect(result.current.isDeleting).toBe(false));
    expect(agentRuntimeService.deleteConversation).toHaveBeenCalledWith("conv-1");
  });
});
