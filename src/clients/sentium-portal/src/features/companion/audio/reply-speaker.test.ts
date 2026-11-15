import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { createConversationStore } from "@/features/assistant/state/assistant-conversation-store";
import type { ConversationMessage } from "@/features/assistant/types/assistant";
import { resetCompanionStore, useCompanionStore, type VoicePolicy } from "../state/companion-store";
import { attachReplySpeaker, markSpokenTurn } from "./reply-speaker";
import { speechPlayer } from "./speech-player";

const policy = (overrides: Partial<VoicePolicy> = {}): VoicePolicy => ({
  canListen: true,
  canSpeak: true,
  voice: "af_heart",
  rate: 1.2,
  reviewBeforeSending: false,
  speakTypedReplies: false,
  ...overrides,
});

const message = (id: string, role: ConversationMessage["role"], content = ""): ConversationMessage => ({
  id,
  role,
  content,
  timestamp: new Date(0),
});

describe("attachReplySpeaker", () => {
  let store: ReturnType<typeof createConversationStore>;
  let detach: () => void;
  let enqueue: ReturnType<typeof vi.spyOn>;
  let stop: ReturnType<typeof vi.spyOn>;

  /** Mirrors what the store does for a turn: both messages first, then the streaming flag. */
  const startTurn = (replyId = "reply") => {
    store.setState((s) => ({ messages: [...s.messages, message("question", "user", "hi"), message(replyId, "assistant")] }));
    store.setState({ isStreaming: true });
  };
  const stream = (replyId: string, text: string) => store.getState().updateLastMessage(replyId, text);
  const endTurn = () => store.setState({ isStreaming: false });
  const spoken = () => enqueue.mock.calls.map(([text]: [string]) => text);

  beforeEach(() => {
    resetCompanionStore();
    useCompanionStore.getState().setVoice(policy());
    enqueue = vi.spyOn(speechPlayer, "enqueue").mockImplementation(() => {});
    stop = vi.spyOn(speechPlayer, "stop").mockImplementation(() => {});
    store = createConversationStore();
    detach = attachReplySpeaker(store);
  });

  afterEach(() => {
    detach();
    resetCompanionStore();
  });

  it("speaks the reply to a spoken message sentence by sentence while it streams", () => {
    markSpokenTurn(store);
    startTurn();

    stream("reply", "The drill is in the garage ");
    expect(spoken()).toEqual([]);

    stream("reply", "on the second shelf. It sits ");
    expect(spoken()).toEqual(["The drill is in the garage on the second shelf."]);
    expect(enqueue).toHaveBeenLastCalledWith(expect.any(String), { voice: "af_heart", speed: 1.2 });

    stream("reply", "next to the saw");
    endTurn();
    expect(spoken()).toEqual(["The drill is in the garage on the second shelf.", "It sits next to the saw"]);
  });

  it("stays silent for a typed message unless typed replies are spoken too", () => {
    startTurn();
    stream("reply", "The drill is in the garage on the second shelf. ");
    endTurn();
    expect(spoken()).toEqual([]);

    useCompanionStore.getState().setVoice(policy({ speakTypedReplies: true }));
    startTurn("second");
    stream("second", "The saw hangs on the back wall of the shed. ");
    endTurn();
    expect(spoken()).toEqual(["The saw hangs on the back wall of the shed."]);
  });

  it("stays silent when speech is unavailable, even for a spoken message", () => {
    useCompanionStore.getState().setVoice(policy({ canSpeak: false }));
    markSpokenTurn(store);
    startTurn();
    stream("reply", "The drill is in the garage on the second shelf. ");
    endTurn();

    expect(spoken()).toEqual([]);
  });

  it("only the next turn after a spoken message is read aloud", () => {
    markSpokenTurn(store);
    startTurn();
    stream("reply", "The drill is in the garage on the second shelf. ");
    endTurn();
    enqueue.mockClear();

    startTurn("typed");
    stream("typed", "This one was typed, so it is not spoken at all. ");
    endTurn();

    expect(spoken()).toEqual([]);
  });

  it("silences whatever was being said the moment a new message is sent", () => {
    startTurn();
    expect(stop).toHaveBeenCalledTimes(1);
  });

  it("never reads thoughts or tool activity, only the answer", () => {
    markSpokenTurn(store);
    startTurn();
    store.getState().updateLastMessage("reply", "I should look in the inventory first. ", "thought");
    stream("reply", "The drill is in the garage on the second shelf. ");
    endTurn();

    expect(spoken()).toEqual(["The drill is in the garage on the second shelf."]);
  });

  it("keeps speaking the same message after a tool approval, without repeating what was said", () => {
    markSpokenTurn(store);
    startTurn();
    stream("reply", "Let me check the inventory for you now. ");
    endTurn();
    enqueue.mockClear();

    store.setState({ isStreaming: true });
    stream("reply", "The drill is in the garage on the second shelf. ");
    endTurn();

    expect(spoken()).toEqual(["The drill is in the garage on the second shelf."]);
  });
});
