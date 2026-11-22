import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import * as assistantService from "./assistant.service";
import { client } from "@/shared/api/client";

vi.mock("@/shared/api/client", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/shared/api/client")>();
  return {
    ...actual,
    client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  };
});

beforeEach(() => {
  vi.clearAllMocks();
  vi.stubGlobal("fetch", vi.fn());
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("assistant.service conversations", () => {
  it("fetchConversations calls client.get with correct path", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([]);
    await assistantService.fetchConversations();
    expect(client.get).toHaveBeenCalledWith("/praxis/conversations?page=1&pageSize=20");
  });

  it("fetchConversations adds an encoded search term", async () => {
    vi.mocked(client.get).mockResolvedValueOnce([]);
    await assistantService.fetchConversations(2, 10, "  trip & tax ");
    expect(client.get).toHaveBeenCalledWith("/praxis/conversations?page=2&pageSize=10&search=trip%20%26%20tax");
  });

  it("fetchConversation calls client.get with id in path", async () => {
    vi.mocked(client.get).mockResolvedValueOnce({});
    await assistantService.fetchConversation("conv-1");
    expect(client.get).toHaveBeenCalledWith("/praxis/conversations/conv-1");
  });

  it("createConversation posts payload", async () => {
    const payload = { title: "New Chat", model: "llama3" };
    vi.mocked(client.post).mockResolvedValueOnce({ id: "c-1" });
    await assistantService.createConversation(payload);
    expect(client.post).toHaveBeenCalledWith("/praxis/conversations", payload);
  });

  it("deleteConversation deletes by id", async () => {
    vi.mocked(client.delete).mockResolvedValueOnce(undefined);
    await assistantService.deleteConversation("conv-1");
    expect(client.delete).toHaveBeenCalledWith("/praxis/conversations/conv-1");
  });

  it("renameConversation puts the title to the conversation's title path", async () => {
    vi.mocked(client.put).mockResolvedValueOnce(undefined);
    await assistantService.renameConversation("conv-1", "Trip planning");
    expect(client.put).toHaveBeenCalledWith("/praxis/conversations/conv-1/title", { title: "Trip planning" });
  });
});

describe("assistant.service chat turns", () => {
  it("startChatTurn posts the chat to the assistant/chat endpoint", async () => {
    vi.mocked(client.post).mockResolvedValueOnce({ turnId: "t1" });
    const payload = { conversationId: "c-1", model: "llama3", messages: [{ role: "user", content: "Hi" }], stream: true };

    await expect(assistantService.startChatTurn(payload)).resolves.toEqual({ turnId: "t1" });
    expect(client.post).toHaveBeenCalledWith("/praxis/assistant/chat", payload);
  });

  it("approveToolCall posts the decision to the approval endpoint", async () => {
    vi.mocked(client.post).mockResolvedValueOnce({ turnId: "t2" });
    await assistantService.approveToolCall("req-1", true);
    expect(client.post).toHaveBeenCalledWith("/praxis/assistant/chat/approve", { requestId: "req-1", approved: true });
  });

  it("cancelChatTurn posts to the turn's cancel endpoint", async () => {
    vi.mocked(client.post).mockResolvedValueOnce(undefined);
    await assistantService.cancelChatTurn("t1");
    expect(client.post).toHaveBeenCalledWith("/praxis/assistant/turns/t1/cancel", {});
  });

  it("chatTurnStreamUrl points at the turn's stream", () => {
    expect(assistantService.chatTurnStreamUrl("t1")).toBe("/api/praxis/assistant/turns/t1/stream");
  });
});
