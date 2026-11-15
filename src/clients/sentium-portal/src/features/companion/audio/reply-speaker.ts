import type { ConversationStore } from "@/features/assistant/state/assistant-conversation-store";
import { useCompanionStore } from "../state/companion-store";
import { createSpeechChunker, type SpeechChunker } from "../utils/speakable-text";
import { speechPlayer } from "./speech-player";

const spokenTurns = new WeakSet<ConversationStore>();

/** The next message sent through `store` was spoken, so its reply is read aloud. */
export function markSpokenTurn(store: ConversationStore): void {
  spokenTurns.add(store);
}

interface Session {
  messageId: string;
  chunker: SpeechChunker;
  /** How much of the message's content has been handed to the chunker. */
  consumed: number;
}

/**
 * Reads a conversation store's streaming reply aloud, sentence by sentence, while it is still arriving. Driven
 * entirely by the store's existing state: the assistant stream carries no voice frames.
 */
export function attachReplySpeaker(store: ConversationStore): () => void {
  let session: Session | null = null;
  let lastSpokenMessageId: string | null = null;

  const speak = (pieces: string[]) => {
    const { voice, rate } = useCompanionStore.getState().voice;
    pieces.forEach((piece) => speechPlayer.enqueue(piece, { voice, speed: rate }));
  };

  return store.subscribe((state, previous) => {
    const reply = state.messages.at(-1);

    if (state.isStreaming && !previous.isStreaming) {
      // A new turn (or an approval answer) always silences whatever was still being said.
      speechPlayer.stop();
      session = null;

      const policy = useCompanionStore.getState().voice;
      const wasSpoken = spokenTurns.delete(store);
      // An approved tool call continues the same message: keep speaking it if it was being spoken.
      const continues = reply?.id === lastSpokenMessageId;

      if (reply?.role === "assistant" && policy.canSpeak && (wasSpoken || continues || policy.speakTypedReplies)) {
        session = { messageId: reply.id, chunker: createSpeechChunker(), consumed: reply.content.length };
        lastSpokenMessageId = reply.id;
      } else {
        lastSpokenMessageId = null;
      }
    }

    if (!session) {
      return;
    }

    if (reply?.id === session.messageId && reply.content.length > session.consumed) {
      const delta = reply.content.slice(session.consumed);
      session.consumed = reply.content.length;
      speak(session.chunker.push(delta));
    }

    if (!state.isStreaming) {
      speak(session.chunker.flush());
      session = null;
    }
  });
}
