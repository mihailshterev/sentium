import { describe, expect, it } from "vitest";
import { createConversationStore, useConversationStore } from "./assistant-conversation-store";

describe("createConversationStore", () => {
  it("creates stores that never share conversations", () => {
    const drawer = createConversationStore();

    drawer
      .getState()
      .setActiveConversation("drawer-conv", [{ id: "m1", role: "user", content: "hi", timestamp: new Date() }], "m");

    expect(drawer.getState().activeConversationId).toBe("drawer-conv");
    expect(useConversationStore.getState().activeConversationId).not.toBe("drawer-conv");
    expect(useConversationStore.getState().messages).not.toEqual(drawer.getState().messages);
  });
});
